namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// The room of a game (docs/design/room-spec.md §9): the players' phones joined to it, each holding some
/// adventurers. Session metadata next to the game, never part of its state: the engine knows nothing of it.
/// </summary>
public sealed class Room
{
    public required string GameId { get; set; }

    /// <summary>6 letters without 0/O/1/I/L ("KRVAMP"): what the players type or the QR code carries; it lasts as long as the room.</summary>
    public required string Code { get; set; }

    public string Status { get; set; } = RoomStatus.Open;

    public List<Seat> Seats { get; set; } = [];

    /// <summary>
    /// The GM's second screen (a tablet in the middle of the table): the hash of its token, which acts as the GM's
    /// device for this game while the room is open. Null when there is none.
    /// </summary>
    public string? ScreenTokenHash { get; set; }

    /// <summary>What players may do without the GM (room-spec §6.3).</summary>
    public string Autonomy { get; set; } = RoomAutonomy.ValidateAll;

    /// <summary>The players' requests, pending first in the GM's queue, then the last answered ones.</summary>
    public List<PlayerRequest> Requests { get; set; } = [];

    public bool IsOpen => Status == RoomStatus.Open;
}

public static class RoomStatus
{
    public const string Open = "open";
    public const string Closed = "closed";
}

/// <summary>One phone in the room. Its token stays on the phone; only its hash is kept here.</summary>
public sealed class Seat
{
    public required string Id { get; set; }

    public required string DisplayName { get; set; }

    public string Color { get; set; } = "teal";

    public required string TokenHash { get; set; }

    /// <summary>The adventurers the GM gave this seat: an adventurer is held by at most one seat.</summary>
    public List<string> Adventurers { get; set; } = [];

    /// <summary>The adventurer the phone shows now (display only, sent back by the phone).</summary>
    public string? Followed { get; set; }

    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>The room as the GM sees it: seats with their connection, the addresses phones can reach.</summary>
public sealed record RoomView(
    string GameId,
    string Code,
    string Status,
    IReadOnlyList<SeatView> Seats,
    IReadOnlyList<string> LanAddresses,
    bool HasScreen,
    string Autonomy,
    IReadOnlyList<PlayerRequest> Requests);

public sealed record SeatView(string Id, string DisplayName, string Color, IReadOnlyList<string> Adventurers, string? Followed, DateTimeOffset LastSeen, bool Connected);

/// <summary>What a phone sees of a room before joining it.</summary>
public sealed record RoomSummary(string Code, string Scenario, LocalizedText? ScenarioTitle, IReadOnlyList<string> Seats);
