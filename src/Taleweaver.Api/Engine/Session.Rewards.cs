using System.Text.Json;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>Items the scenario gives (play-spec A4): named, at random, to choose, for each, with a fallback.</summary>
internal sealed partial class Session
{
    private const string PickRandom = "random";
    private const string PickChoice = "choice";

    private void GainItem(ScenarioAction action)
    {
        if (action.Each == true)
        {
            Push(EachBut(action).Select(a => action with { Each = null, Who = JsonSerializer.SerializeToElement(a.Name) }));
            return;
        }

        var who = WhoIs(action);
        if (who == AskRole && State.Campaign.Party.Count > 0)
        {
            // Pushed, not asked at once: an Achievement's reward may come while another question waits
            Push([new ScenarioAction
            {
                Do = "askChoice",
                Question = Text("Who gains the Item?", "Qui reçoit l’objet ?"),
                Options = State.Campaign.Party.Select(a => new ActionOption
                {
                    Label = Text(a.Name, a.Name),
                    Actions = [action with { Who = JsonSerializer.SerializeToElement(a.Name) }],
                }).ToList(),
            }]);
            return;
        }

        // Named: theirs, in the Pack (a free Re-Equip follows, p. 70); nobody named: the players choose
        var gainer = Member(who);
        var pick = ExtraString(action, "pick");
        var count = Math.Max(1, action.Count ?? 1);
        if (pick == PickChoice)
        {
            AskItem(action, count);
            return;
        }

        var missing = 0;
        for (var i = 0; i < count; i++)
        {
            var card = Catalog.FindItem(action.Ref);
            if (pick == PickRandom)
            {
                var pool = ItemPool(action);
                card = pool.Count > 0 ? pool[NextRandom(pool.Count)] : null;
                if (card is null)
                {
                    missing++;
                    continue;
                }
            }
            else if (card is not null && IsTaken(card, action.Fallback is { Count: > 0 }))
            {
                missing++;
                continue;
            }

            // The history has every card by its id, and what is still to draw as the reward says it
            if (card is not null)
            {
                State.Campaign.Items.Add(card.Id);
            }
            else if (i == 0)
            {
                State.Campaign.Items.Add(action.Ref ?? $"{count} {string.Join("/", action.Tiers ?? [])} {action.Type}".Trim());
            }

            GainOwned(card?.Id, "scenario", card is null ? action.Tiers : null, card is null ? action.Type : null, gainer);
            if (pick == PickRandom)
            {
                Log("gainItem", args: Args(("item", card!.Id), ("adventurer", gainer?.Name)));
            }
        }

        if (pick != PickRandom && missing < count)
        {
            Log("gainItem", args: Args(("item", action.Ref), ("count", Number(action.Count)), ("tiers", action.Tiers is null ? null : string.Join("/", action.Tiers)), ("type", action.Type),
                ("adventurer", gainer?.Name)));
        }

        if (missing > 0)
        {
            NoCardLeft(action, missing);
        }
    }

    /// <summary>
    /// A Unique card is limited to its copies (p. 57, 70): gained, sold or not, it is out. The packs rarely say how many
    /// copies a Story card has: it is limited when they do, or when the scenario says what happens "otherwise".
    /// </summary>
    private bool IsTaken(Item card, bool hasFallback = true) =>
        (card.Unique || (card.Story && (hasFallback || card.Copies is not null))) && State.Campaign.Items.Count(id => id == card.Id) >= Math.Max(1, card.Copies ?? 1);

    private void NoCardLeft(ScenarioAction action, int missing)
    {
        Log("rewardTaken", args: Args(("item", action.Ref), ("count", Number(missing)), ("tiers", action.Tiers is null ? null : string.Join("/", action.Tiers)), ("type", action.Type)));
        Push(action.Fallback ?? []);
    }

    private void AskItem(ScenarioAction action, int count)
    {
        var pool = ItemPool(action).OrderBy(i => i.Name.GetValueOrDefault("en"), StringComparer.Ordinal).ToList();
        if (pool.Count == 0)
        {
            NoCardLeft(action, count);
            return;
        }

        var choice = new ScenarioAction
        {
            Do = "askChoice",
            Question = action.Question ?? Text("Which Item is gained?", "Quel objet est gagné ?"),
            Options = pool.Select(card => new ActionOption
            {
                Label = card.Name,
                Actions = [With(action, "pick", null) with { Ref = card.Id, Count = 1, Fallback = null }],
            }).ToList(),
        };
        // One at a time: the list loses what was just taken
        Push(count > 1 ? [choice, action with { Count = count - 1 }] : [choice]);
    }

    /// <summary>
    /// The cards a reward may be (p. 13, 17, 70): those listed ("among"), else those of the type and tiers, Unique only
    /// when said. Tiers not said: the Loot LVL's; for a Consumable, that one and every tier below.
    /// </summary>
    private List<Item> ItemPool(ScenarioAction action)
    {
        var cards = Catalog.Packs.SelectMany(p => p.Items).GroupBy(i => i.Id).Select(g => g.Last());
        if (ExtraList(action, "among") is { } among)
        {
            return cards.Where(i => among.Contains(i.Id) && !IsTaken(i)).ToList();
        }

        var unique = ExtraTrue(action, "unique");
        var tiers = action.Tiers?.ToList() ?? [];
        var level = action.LootLevel ?? (State.Encounter is null ? null : EncounterNode.LootLevel);
        if (tiers.Count == 0 && LootLevels.IsKnown(level))
        {
            tiers = action.Type == "consumable" ? LootLevels.Ordered.Take(LootLevels.Rank(level) + 1).ToList() : [level!];
        }

        var left = unique ? UniquesLeftOf(tiers).ToHashSet() : null;
        return cards.Where(i =>
            (action.Type is null || i.Type == action.Type)
            && (left is not null
                ? left.Contains(i.Id)
                : !i.Unique && !i.Story && (tiers.Count > 0 ? i.Tier is { } tier && tiers.Contains(tier) : LootLevels.IsKnown(i.Tier)))).ToList();
    }

    private IEnumerable<string> UniquesLeftOf(List<string> tiers) =>
        (tiers.Count > 0 ? tiers : LootLevels.Ordered.ToList()).SelectMany(UniquesLeft);
}
