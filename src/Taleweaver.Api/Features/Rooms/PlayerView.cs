using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// What a phone receives (room-spec §8), built by a white list: only what is copied here leaves the server. A
/// player never gets the game state itself: no hidden text, no AI, no prompt, no fog, no other inventory.
/// </summary>
public sealed record PlayerView(
    long Version,
    string GameId,
    LocalizedText? ScenarioTitle,
    string? RoomCode,
    PlayerSeat? Seat,
    bool Impersonated,
    IReadOnlyList<AdventurerState> Adventurers,
    IReadOnlyList<PartyMemberView> Party,
    int Gold,
    IReadOnlyList<OwnedItem> Stash,
    IReadOnlyList<OwnedItem> Unassigned,
    PlayerEncounter? Encounter);

/// <summary>The seat as its phone knows it.</summary>
public sealed record PlayerSeat(string Id, string DisplayName, string Color, string? Followed);

/// <summary>A party member as the table sees them: never their items, disciplines or XP.</summary>
public sealed record PartyMemberView(string Name, int Damage, int Health, bool Defeated, bool Injured, bool Unselectable, bool Active, bool Mine);

/// <summary>The encounter as the table sees it (R1: round and Urgency; the track comes with R2).</summary>
public sealed record PlayerEncounter(int Round, int Urgency, bool Ended);

public static class PlayerViewProjector
{
    /// <summary>The view of a seat (or of the GM playing <paramref name="adventurers"/> as a player).</summary>
    public static PlayerView Project(
        string gameId,
        long version,
        GameState state,
        LocalizedText? scenarioTitle,
        IReadOnlyCollection<string> adventurers,
        Room? room = null,
        Seat? seat = null)
    {
        var campaign = state.Campaign;
        var active = campaign.ActiveGroup?.ToHashSet()
            ?? campaign.Party.Where(a => !a.Injured && !a.Unselectable).Select(a => a.Name).ToHashSet();
        var mine = campaign.Party.Where(a => adventurers.Contains(a.Name)).ToList();
        var encounter = state.Encounter is { } e ? new PlayerEncounter(e.Round, e.Urgency, e.Ended) : null;

        return new PlayerView(
            version,
            gameId,
            scenarioTitle,
            room?.Code,
            seat is null ? null : new PlayerSeat(seat.Id, seat.DisplayName, seat.Color, seat.Followed),
            Impersonated: seat is null,
            mine,
            campaign.Party.Select(a => new PartyMemberView(a.Name, a.Damage, a.Health, a.Defeated, a.Injured, a.Unselectable, active.Contains(a.Name), adventurers.Contains(a.Name))).ToList(),
            campaign.Gold,
            campaign.Stash,
            campaign.Unassigned,
            encounter);
    }
}
