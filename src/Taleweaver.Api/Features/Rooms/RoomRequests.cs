using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>How much a player may do without the GM (room-spec §6.3), chosen by the GM for the room.</summary>
public static class RoomAutonomy
{
    /// <summary>Every request waits for the GM (the default).</summary>
    public const string ValidateAll = "validateAll";

    /// <summary>Out of an encounter, moving items between one's own adventurers and the stash, and flipping a card, apply at once.</summary>
    public const string FreeInventory = "freeInventoryOutsideEncounter";

    public static bool IsKnown(string? value) => value is ValidateAll or FreeInventory;
}

public static class RequestStatus
{
    public const string Pending = "pending";

    /// <summary>Accepted by the GM and applied.</summary>
    public const string Accepted = "accepted";

    /// <summary>Applied at once (autonomy): the GM sees it in the journal and may undo it.</summary>
    public const string Applied = "applied";

    public const string Refused = "refused";

    public const string Cancelled = "cancelled";

    /// <summary>Accepted, but it no longer applied (the item was gone, not enough XP…): <see cref="PlayerRequest.Reason"/> says why.</summary>
    public const string Failed = "failed";
}

/// <summary>A player's request (room-spec §6.2): what they asked, and what became of it.</summary>
public sealed class PlayerRequest
{
    public required string Id { get; set; }

    public required string SeatId { get; set; }

    /// <summary>The player's name when they asked (the journal and the GM's queue show it).</summary>
    public required string By { get; set; }

    public required PlayerCommand Command { get; set; }

    public string Status { get; set; } = RequestStatus.Pending;

    /// <summary>Why it was refused or failed: an error code ("Party.NotEnoughXp"…) or the GM's words.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset At { get; set; }
}

/// <summary>
/// What a phone may ask (room-spec §6.2), and nothing else: never a raw engine command. <c>Item</c> is the id of
/// an item the party holds ("item-3"), <c>Owner</c> an adventurer's name or "stash".
/// </summary>
public sealed record PlayerCommand(
    string Type,
    string? Item = null,
    string? Owner = null,
    string? Slot = null,
    bool? Flipped = null,
    bool? Discard = null,
    string? Adventurer = null,
    string? Discipline = null,
    int? Amount = null,
    string? Effect = null,
    bool? Active = null,
    int? Price = null,
    string? Upgrade = null,
    string? Kit = null,
    IReadOnlyList<string>? Items = null,
    IReadOnlyList<string>? Disciplines = null,
    string? Card = null,
    string? Ally = null)
{
    /// <summary>Before the departure (roadmap §5.3): this adventurer for the seat, with a kit and purchases.</summary>
    public const string ChooseAdventurer = "chooseAdventurer";

    /// <summary>Before the departure: the seat lets its adventurer go.</summary>
    public const string ReleaseAdventurer = "releaseAdventurer";

    /// <summary>A Loyal Esper or Enslaved Spirits (<c>Card</c>) for one of the seat's adventurers.</summary>
    public const string Summon = "summon";

    public const string EarnConduit = "earnConduit";

    /// <summary>A Conduit earned (<c>Card</c>), or none: the Adventurer Card again.</summary>
    public const string ConduitForm = "conduitForm";

    /// <summary>One of the seat's Espers (<c>Ally</c>) sent away.</summary>
    public const string DismissAlly = "dismissAlly";

    /// <summary>Damage (<c>Amount</c>) taken by an ally (<c>Ally</c>).</summary>
    public const string AllyDamage = "allyDamage";

    public const string MoveItem = "moveItem";
    public const string FlipItem = "flipItem";
    public const string UseItem = "useItem";
    public const string LearnDiscipline = "learnDiscipline";

    /// <summary>A card of the Store (<c>Item</c> = catalogue id), for <c>Owner</c>; its price is the Store's, set by the server.</summary>
    public const string BuyItem = "buyItem";

    /// <summary>An unlocked Item Upgrade on the card <c>Item</c> held; its price is the tier's, set by the server.</summary>
    public const string AttachUpgrade = "attachUpgrade";

    /// <summary>A free adventurer of the party for this seat (room-spec §4.3): the GM gives it, no engine command.</summary>
    public const string TakeAdventurer = "takeAdventurer";

    /// <summary>Damage taken (<c>Amount</c>, negative: healed) by one of the seat's adventurers.</summary>
    public const string Damage = "damage";

    /// <summary>An Effect gained or lost (<c>Active</c>) by one of the seat's adventurers, in an encounter.</summary>
    public const string SetEffect = "effect";

    /// <summary>The engine command it becomes, naming who asked for it.</summary>
    public GameCommand ToGameCommand(string by) => (Type switch
    {
        MoveItem => GameCommand.MoveItem(Item!, Owner!, Slot),
        FlipItem => GameCommand.FlipItem(Item!, Flipped ?? false),
        UseItem => GameCommand.UseItem(Item!, Discard ?? true),
        BuyItem => GameCommand.AddItem(Item!, Owner!, Owner == "stash" ? null : Slot ?? "pack", Price ?? 0),
        AttachUpgrade => GameCommand.AttachUpgrade(Item!, Upgrade!, Price ?? 0),
        Damage => GameCommand.Damage("adventurer:" + Adventurer, Amount ?? 0),
        Summon => GameCommand.Summon(Adventurer!, Card!),
        EarnConduit => GameCommand.EarnConduit(Adventurer!, Card!),
        ConduitForm => GameCommand.ConduitForm(Adventurer!, Card),
        DismissAlly => GameCommand.DismissAlly(Ally!),
        AllyDamage => GameCommand.Damage(Ally!, Amount ?? 0),
        SetEffect => GameCommand.SetEffect("adventurer:" + Adventurer, Effect!, Active ?? true),
        _ => GameCommand.LearnDiscipline(Adventurer!, Discipline!, 0),
    }) with { By = by };
}

public static class PlayerCommands
{
    public static class Errors
    {
        public static Error NotAllowed(string type) => Error.Validation("Rooms.CommandNotAllowed", $"A player may not ask for '{type}'.");

        public static Error ItemNotYours(string item) => Error.Forbidden("Rooms.ItemNotAccessible", $"Item '{item}' is not held by this seat's adventurers nor in the stash.");

        public static readonly Error RequestNotPending = Error.Conflict("Rooms.RequestNotPending", "This request was already dealt with.");

        public static Error RequestNotFound(string id) => Error.NotFound("Rooms.RequestNotFound", $"No request '{id}'.");

        public static Error NotInStore(string item) => Error.Validation("Rooms.NotInStore", $"'{item}' is not for sale here now.");

        public static Error UpgradeNotOffered(string upgrade) => Error.Validation("Rooms.UpgradeNotOffered", $"'{upgrade}' cannot be put on this card now.");

        public static Error AdventurerTaken(string adventurer) => Error.Conflict("Rooms.AdventurerTaken", $"'{adventurer}' is held by another seat.");

        public static Error AllyNotYours(string ally) => Error.Forbidden("Rooms.AllyNotAccessible", $"'{ally}' is not this seat's to play.");

        public static readonly Error NoGiving =
            Error.Conflict("Rooms.NoGiving", "In an encounter an adventurer takes from an adjacent ally, and never gives.");

        public static readonly Error InvalidAutonomy = Error.Validation("Rooms.InvalidAutonomy", $"Autonomy is \"{RoomAutonomy.ValidateAll}\" or \"{RoomAutonomy.FreeInventory}\".");
    }

    /// <summary>
    /// May this seat ask for it? Its own adventurers' items and the stash's, given to a party member or to the
    /// stash; disciplines for its own adventurers. Whether the engine accepts it is checked apart.
    /// </summary>
    public static Error? Check(PlayerCommand command, IReadOnlyCollection<string> mine, CampaignState campaign, EncounterState? encounter = null)
    {
        switch (command.Type)
        {
            case PlayerCommand.Summon or PlayerCommand.EarnConduit or PlayerCommand.ConduitForm:
                if (string.IsNullOrEmpty(command.Adventurer) || !mine.Contains(command.Adventurer))
                {
                    return RoomService.Errors.AdventurerNotAccessible(command.Adventurer ?? "");
                }

                return command.Type != PlayerCommand.ConduitForm && string.IsNullOrEmpty(command.Card) ? Errors.NotAllowed(command.Type) : null;
            case PlayerCommand.DismissAlly or PlayerCommand.AllyDamage:
                // The story's allies are anybody's who plays
                var ally = encounter?.Allies.FirstOrDefault(a => !a.Defeated && "ally:" + a.Id == command.Ally);
                var theirs = ally is not null && (ally.Owner is { } owner0 ? mine.Contains(owner0) : mine.Count > 0);
                return theirs ? null : Errors.AllyNotYours(command.Ally ?? "");
            case PlayerCommand.MoveItem or PlayerCommand.FlipItem or PlayerCommand.UseItem or PlayerCommand.AttachUpgrade:
                if (string.IsNullOrEmpty(command.Item) || OwnerOf(command.Item, campaign) is not { } owner || (owner != "stash" && !mine.Contains(owner)))
                {
                    return Errors.ItemNotYours(command.Item ?? "");
                }

                if (command.Type == PlayerCommand.MoveItem
                    && (string.IsNullOrEmpty(command.Owner) || (command.Owner != "stash" && campaign.Party.All(a => a.Name != command.Owner))))
                {
                    return RoomService.Errors.NotInParty(command.Owner ?? "");
                }

                if (command.Type == PlayerCommand.MoveItem && encounter is { Ended: false } && owner != "stash" && command.Owner != "stash" && command.Owner != owner)
                {
                    return Errors.NoGiving;
                }

                return null;
            case PlayerCommand.BuyItem:
                if (string.IsNullOrEmpty(command.Owner) || (command.Owner != "stash" && !mine.Contains(command.Owner)))
                {
                    return RoomService.Errors.AdventurerNotAccessible(command.Owner ?? "");
                }

                return null;
            case PlayerCommand.Damage or PlayerCommand.SetEffect:
                if (string.IsNullOrEmpty(command.Adventurer) || !mine.Contains(command.Adventurer))
                {
                    return RoomService.Errors.AdventurerNotAccessible(command.Adventurer ?? "");
                }

                return command.Type == PlayerCommand.SetEffect && string.IsNullOrWhiteSpace(command.Effect) ? Errors.NotAllowed(command.Type) : null;
            case PlayerCommand.TakeAdventurer:
                if (string.IsNullOrEmpty(command.Adventurer) || campaign.Party.All(a => a.Name != command.Adventurer))
                {
                    return RoomService.Errors.NotInParty(command.Adventurer ?? "");
                }

                return mine.Contains(command.Adventurer) ? Errors.NotAllowed(command.Type) : null;
            case PlayerCommand.LearnDiscipline:
                if (string.IsNullOrEmpty(command.Adventurer) || !mine.Contains(command.Adventurer))
                {
                    return RoomService.Errors.AdventurerNotAccessible(command.Adventurer ?? "");
                }

                return string.IsNullOrEmpty(command.Discipline) ? Errors.NotAllowed(command.Type) : null;
            default:
                return Errors.NotAllowed(command.Type);
        }
    }

    /// <summary>
    /// Applies without the GM (autonomy, room-spec §6.3): out of an encounter, a card moved between the seat's own
    /// adventurers and the stash, or flipped. Never what touches the encounter, never a discipline.
    /// </summary>
    public static bool IsFree(PlayerCommand command, string autonomy, IReadOnlyCollection<string> mine, GameState state)
    {
        if (autonomy != RoomAutonomy.FreeInventory || state.Encounter is { Ended: false })
        {
            return false;
        }

        return command.Type switch
        {
            PlayerCommand.FlipItem => true,
            PlayerCommand.MoveItem => command.Owner == "stash" || mine.Contains(command.Owner!),
            _ => false,
        };
    }

    /// <summary>Who holds the item now: an adventurer's name, "stash" or "unassigned"; null when the party has no such item.</summary>
    public static string? OwnerOf(string item, CampaignState campaign)
    {
        if (campaign.Party.FirstOrDefault(a => a.Equipped.Any(i => i.Id == item) || a.Pack.Any(i => i.Id == item)) is { } holder)
        {
            return holder.Name;
        }

        return campaign.Stash.Any(i => i.Id == item) ? "stash" : campaign.Unassigned.Any(i => i.Id == item) ? "unassigned" : null;
    }
}

public sealed partial class RoomService
{
    /// <summary>The requests a room keeps (the GM's queue, each phone's recent answers).</summary>
    public const int RequestsKept = 60;

    /// <summary>A request made by the seat: pending, or already applied (autonomy).</summary>
    public async Task<PlayerRequest> AddRequestAsync(Room room, Seat seat, PlayerCommand command, string status, CancellationToken cancellationToken)
    {
        var request = new PlayerRequest
        {
            Id = "r-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)),
            SeatId = seat.Id,
            By = seat.DisplayName,
            Command = command,
            Status = status,
            At = time.GetUtcNow(),
        };
        var saved = await Locked(rooms =>
        {
            var live = rooms.GetValueOrDefault(room.GameId) ?? room;
            live.Requests.Add(request);
            // The oldest answered requests go first: a pending one stays until the GM deals with it
            while (live.Requests.Count > RequestsKept && live.Requests.FindIndex(r => r.Status != RequestStatus.Pending) is var old and >= 0)
            {
                live.Requests.RemoveAt(old);
            }

            return live;
        });
        await SaveAndNotify(saved, cancellationToken);
        return request;
    }

    /// <summary>
    /// Takes a pending request for the GM to accept or refuse, or for its seat to cancel (<paramref name="seatId"/>):
    /// its status changes at once, so that it is dealt with only once.
    /// </summary>
    public async Task<Result<(Room Room, PlayerRequest Request)>> ResolveAsync(string gameId, string requestId, string status, string? reason, string? seatId, CancellationToken cancellationToken)
    {
        var outcome = await Locked<Result<(Room, PlayerRequest)>>(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return Errors.NoRoom(gameId);
            }

            if (room.Requests.FirstOrDefault(r => r.Id == requestId && (seatId is null || r.SeatId == seatId)) is not { } request)
            {
                return PlayerCommands.Errors.RequestNotFound(requestId);
            }

            if (request.Status != RequestStatus.Pending)
            {
                return PlayerCommands.Errors.RequestNotPending;
            }

            request.Status = status;
            request.Reason = reason;
            return (room, request);
        });
        if (outcome.IsSuccess)
        {
            await SaveAndNotify(outcome.Value.Item1, cancellationToken);
        }

        return outcome;
    }

    /// <summary>An accepted request that the engine refused after all.</summary>
    public async Task FailAsync(Room room, PlayerRequest request, string reason, CancellationToken cancellationToken)
    {
        await Locked(_ =>
        {
            request.Status = RequestStatus.Failed;
            request.Reason = reason;
            return request;
        });
        await SaveAndNotify(room, cancellationToken);
    }

    public async Task<Result<Room>> SetAutonomyAsync(string gameId, string autonomy, CancellationToken cancellationToken)
    {
        if (!RoomAutonomy.IsKnown(autonomy))
        {
            return PlayerCommands.Errors.InvalidAutonomy;
        }

        var room = await Locked(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is { } room)
            {
                room.Autonomy = autonomy;
            }

            return rooms.GetValueOrDefault(gameId);
        });
        if (room is null)
        {
            return Errors.NoRoom(gameId);
        }

        await SaveAndNotify(room, cancellationToken);
        return room;
    }
}
