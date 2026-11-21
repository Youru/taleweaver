using System.Globalization;
using System.Text.RegularExpressions;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The party's items (rulebook ch. 6, p. 25): the Party Stash, what each adventurer has equipped or in their
/// Pack, and what was gained but not handed out yet. The GM moves the cards; the engine only keeps the count
/// (limits are the table's to respect: the front warns, nothing is refused but what cannot be).
/// </summary>
internal sealed partial class Session
{
    public const string StashOwner = "stash";
    public const string UnassignedOwner = "unassigned";
    public const string EquippedSlot = "equipped";
    public const string PackSlot = "pack";

    private static class ItemErrors
    {
        public static Error UnknownItem(string? id) => Error.NotFound("Items.UnknownItem", $"No item '{id}' in the catalogue.");

        public static Error NotOwned(string? id) => Error.NotFound("Items.NotOwned", $"The party holds no item '{id}'.");

        public static Error UnknownOwner(string? owner) =>
            Error.Validation("Items.UnknownOwner", $"'{owner}' is neither an adventurer of the party, \"{StashOwner}\" nor \"{UnassignedOwner}\".");

        public static Error NotEnoughGold(int price, int gold) => Error.Conflict("Items.NotEnoughGold", $"The item costs {price} Gold, the party has {gold}.");

        public static Error NotDrawnYet(string? id) => Error.Conflict("Items.NotDrawnYet", $"Item '{id}' is still to be drawn: choose its card first.");

        public static Error UnknownToken(string? token) => Error.NotFound("Items.UnknownToken", $"No token '{token}' in the catalogue.");

        public static readonly Error TokenNeedsSquare = Error.Validation("Items.TokenNeedsSquare", "Say on which square the token goes.");
    }

    private Result ExecuteItemCommand(GameCommand command) => command.Type switch
    {
        GameCommands.AddItem => AddItem(command),
        GameCommands.MoveItem => MoveItem(command),
        GameCommands.DiscardItem => DiscardItem(command),
        GameCommands.FlipItem => FlipItem(command),
        GameCommands.UseItem => UseItem(command),
        _ => GameEngine.Errors.UnknownCommand(command.Type),
    };

    private Result AddItem(GameCommand command)
    {
        if (Catalog.FindItem(command.Item) is not { } card)
        {
            return ItemErrors.UnknownItem(command.Item);
        }

        if (ItemList(command.Owner, command.Slot) is not { } list)
        {
            return ItemErrors.UnknownOwner(command.Owner);
        }

        var price = Math.Max(0, command.Value ?? 0);
        if (price > State.Campaign.Gold)
        {
            return ItemErrors.NotEnoughGold(price, State.Campaign.Gold);
        }

        // The card drawn for an item still to be drawn (Item Loot…) takes its place in the game.
        OwnedItem? replaced = null;
        if (command.Replaces is { } replaces)
        {
            if (FindOwned(replaces) is not { Owned.Item: null } found)
            {
                return ItemErrors.NotOwned(replaces);
            }

            replaced = found.Owned;
            found.List.Remove(found.Owned);
        }

        State.Campaign.Gold -= price;
        var owned = new OwnedItem
        {
            Id = replaced?.Id ?? NewItemId(),
            Item = card.Id,
            Source = replaced?.Source ?? "gm",
            Flipped = false,
        };
        list.Add(owned);
        State.Campaign.Items.Add(card.Id);
        Log("itemAdded", args: Args(("item", card.Id), ("id", owned.Id), ("owner", command.Owner), ("slot", SlotOf(command.Owner, command.Slot)),
            ("price", price > 0 ? Number(price) : null), ("gold", price > 0 ? Number(State.Campaign.Gold) : null), ("drawn", replaced is null ? null : "true")));
        return Result.Success();
    }

    private Result MoveItem(GameCommand command)
    {
        if (FindOwned(command.Instance) is not { } found)
        {
            return ItemErrors.NotOwned(command.Instance);
        }

        if (ItemList(command.Owner, command.Slot) is not { } list)
        {
            return ItemErrors.UnknownOwner(command.Owner);
        }

        found.List.Remove(found.Owned);
        list.Add(found.Owned);
        Log("itemMoved", args: Args(("item", found.Owned.Item), ("id", found.Owned.Id), ("owner", command.Owner), ("slot", SlotOf(command.Owner, command.Slot))));
        return Result.Success();
    }

    private Result DiscardItem(GameCommand command)
    {
        if (FindOwned(command.Instance) is not { } found)
        {
            return ItemErrors.NotOwned(command.Instance);
        }

        found.List.Remove(found.Owned);
        var gold = Math.Max(0, command.Value ?? 0);
        if (gold > 0)
        {
            State.Campaign.Gold += gold;
            Log("itemSold", args: Args(("item", found.Owned.Item), ("id", found.Owned.Id), ("amount", Number(gold)), ("gold", Number(State.Campaign.Gold))));
        }
        else
        {
            Log("itemDiscarded", args: Args(("item", found.Owned.Item), ("id", found.Owned.Id)));
        }

        return Result.Success();
    }

    private Result FlipItem(GameCommand command)
    {
        if (FindOwned(command.Instance) is not { } found)
        {
            return ItemErrors.NotOwned(command.Instance);
        }

        found.Owned.Flipped = command.Active ?? !found.Owned.Flipped;
        Log("itemFlipped", args: Args(("item", found.Owned.Item), ("id", found.Owned.Id), ("flipped", found.Owned.Flipped ? "true" : "false")));
        return Result.Success();
    }

    /// <summary>An item used: discarded if it says so, and its token placed on the board (an encounter must be in progress).</summary>
    private Result UseItem(GameCommand command)
    {
        if (FindOwned(command.Instance) is not { } found)
        {
            return ItemErrors.NotOwned(command.Instance);
        }

        if (found.Owned.Item is null)
        {
            return ItemErrors.NotDrawnYet(command.Instance);
        }

        BoardToken? placed = null;
        if (command.Token is { } tokenId)
        {
            if (State.Encounter is not { Ended: false } encounter)
            {
                return GameEngine.Errors.NotInEncounter;
            }

            if (Catalog.FindToken(tokenId) is not { } type)
            {
                return ItemErrors.UnknownToken(tokenId);
            }

            if (command.To is not { } at)
            {
                return ItemErrors.TokenNeedsSquare;
            }

            var (w, h) = TokenSize(type.Size);
            placed = new BoardToken
            {
                Id = $"item-token-{encounter.Tokens.Count + 1}",
                Key = TokenType(type.Id),
                Type = type.Id,
                Area = new Rect(at.X, at.Y, w, h),
            };
            encounter.Tokens.Add(placed);
        }

        var discard = command.Active ?? false;
        if (discard)
        {
            found.List.Remove(found.Owned);
        }

        Log("itemUsed", args: Args(("item", found.Owned.Item), ("id", found.Owned.Id), ("owner", OwnerOf(found.List)),
            ("discarded", discard ? "true" : null), ("token", placed?.Type), ("at", placed?.Area is { } a ? $"{a.X},{a.Y}" : null)));
        return Result.Success();
    }

    /// <summary>"2x1" → (2, 1); 1×1 when unknown.</summary>
    private static (int W, int H) TokenSize(string? size)
    {
        var match = size is null ? Match.Empty : Regex.Match(size, @"^(\d+)x(\d+)$", RegexOptions.IgnoreCase);
        return match.Success
            ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            : (1, 1);
    }

    /// <summary>Gained during the game (loot, scenario reward): waits for the GM to hand it out.</summary>
    private OwnedItem GainOwned(string? item, string source, IReadOnlyList<string>? tiers = null, string? type = null)
    {
        var owned = new OwnedItem
        {
            Id = NewItemId(),
            Item = item,
            Tiers = item is null && tiers is { Count: > 0 } ? tiers.ToList() : null,
            Type = item is null ? type : null,
            Source = source,
        };
        State.Campaign.Unassigned.Add(owned);
        return owned;
    }

    private string NewItemId() => $"item-{++State.Campaign.ItemCount}";

    /// <summary>The list an owner and slot designate, null when there is no such owner.</summary>
    private List<OwnedItem>? ItemList(string? owner, string? slot)
    {
        switch (owner)
        {
            case StashOwner:
                return State.Campaign.Stash;
            case UnassignedOwner:
                return State.Campaign.Unassigned;
        }

        var adventurer = State.Campaign.Party.FirstOrDefault(a => string.Equals(a.Name, owner, StringComparison.OrdinalIgnoreCase));
        return adventurer is null ? null : slot == PackSlot ? adventurer.Pack : adventurer.Equipped;
    }

    private static string? SlotOf(string? owner, string? slot) =>
        owner is StashOwner or UnassignedOwner ? null : slot == PackSlot ? PackSlot : EquippedSlot;

    private string? OwnerOf(List<OwnedItem> list) =>
        State.Campaign.Party.FirstOrDefault(a => ReferenceEquals(a.Equipped, list) || ReferenceEquals(a.Pack, list))?.Name;

    private (OwnedItem Owned, List<OwnedItem> List)? FindOwned(string? id)
    {
        if (id is null)
        {
            return null;
        }

        var lists = new[] { State.Campaign.Unassigned, State.Campaign.Stash }
            .Concat(State.Campaign.Party.SelectMany(a => new[] { a.Equipped, a.Pack }));
        foreach (var list in lists)
        {
            if (list.FirstOrDefault(i => i.Id == id) is { } owned)
            {
                return (owned, list);
            }
        }

        return null;
    }
}
