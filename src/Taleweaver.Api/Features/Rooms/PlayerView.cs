using Taleweaver.Api.Content;
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
    PlayerEncounter? Encounter,
    IReadOnlyList<PlayerLogEntry> Log);

/// <summary>The seat as its phone knows it.</summary>
public sealed record PlayerSeat(string Id, string DisplayName, string Color, string? Followed);

/// <summary>A party member as the table sees them: never their items, disciplines or XP.</summary>
public sealed record PartyMemberView(string Name, int Damage, int Health, bool Defeated, bool Injured, bool Unselectable, bool Active, bool Mine);

/// <summary>
/// The encounter as the table sees it: round, Urgency, the initiative track and the figures on the board, with
/// their damage and effects. Never the AI, its steps, the hidden rules nor the end conditions.
/// </summary>
public sealed record PlayerEncounter(
    int Round,
    int Urgency,
    bool Ended,
    IReadOnlyList<string> InitiativeTrack,
    int CurrentTurn,
    IReadOnlyList<string> PlayedCards,
    IReadOnlyList<PlayerCombatant> Combatants,
    PlayerActivation? Activation,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AdventurerEffects);

/// <summary>
/// An opponent on the board: what its card and its tokens show. Name, picture and Health come with it, since a
/// phone may not read the combatant cards (their AI is the GM's).
/// </summary>
public sealed record PlayerCombatant(
    string Instance,
    string Card,
    string Family,
    int Number,
    IReadOnlyList<string> InitiativeCards,
    int Damage,
    IReadOnlyList<string> Effects,
    bool Defeated,
    LocalizedText? Name,
    string? Image,
    int? Health);

/// <summary>The group playing: which figure acts (not the AI step it is on).</summary>
public sealed record PlayerActivation(string InitiativeCard, IReadOnlyList<string> Figures);

/// <summary>A line of the public log, with its place in the GM's log (the phone highlights what is new).</summary>
public sealed record PlayerLogEntry(int Index, LogEntry Entry);

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
        Seat? seat = null,
        Catalog? catalog = null)
    {
        var campaign = state.Campaign;
        var active = campaign.ActiveGroup?.ToHashSet()
            ?? campaign.Party.Where(a => !a.Injured && !a.Unselectable).Select(a => a.Name).ToHashSet();
        var mine = campaign.Party.Where(a => adventurers.Contains(a.Name)).ToList();
        var encounter = state.Encounter is { } e ? EncounterOf(e, catalog) : null;

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
            encounter,
            PublicLog(state.Log));
    }

    /// <summary>
    /// What the table hears (room-spec §8): the scenes read aloud, the rounds and turns, damage, defeats, loot and
    /// rewards. Never the hidden texts and rules, the AI, the GM's answers, the variables nor the spawn checks.
    /// </summary>
    public static readonly IReadOnlySet<string> PublicLogCodes = new HashSet<string>
    {
        "scene", "text", "encounter", "navigate", "revealArea", "roundStart", "roundEnd", "turn", "urgency", "urgencyDefeat",
        "damage", "defeated", "adventurerDamage", "adventurerDefeated", "stamina", "adventurersPrepared", "restoreAdventurers",
        "encounterEnd", "reward", "scenarioEnd", "partyDefeated",
        "gainGold", "gainXp", "gainItem", "itemAdded", "itemMoved", "itemUsed", "itemDiscarded", "itemSold", "itemFlipped", "consumableDiscarded",
        "lootGold", "lootItem", "lootMonster", "lootUnique", "lootNone",
        "upgradeUnlocked", "upgradeAttached", "upgradeDetached", "disciplineLearnt", "disciplineForgotten", "respec",
        "joinParty", "leaveParty", "activeGroup", "adventurerStatus",
        "storyRound", "storyRoundReturn", "storyRoundExplore", "sideQuest", "ventureForth", "storeUnique", "bountyStart", "bountyEnd",
    };

    /// <summary>The last lines a phone keeps (the whole view is sent at each change).</summary>
    public const int LogLength = 80;

    private static List<PlayerLogEntry> PublicLog(IReadOnlyList<LogEntry> log) =>
        log.Select((entry, index) => new PlayerLogEntry(index, entry))
            .Where(e => PublicLogCodes.Contains(e.Entry.Code))
            .TakeLast(LogLength)
            .ToList();

    private static PlayerEncounter EncounterOf(EncounterState e, Catalog? catalog) => new(
        e.Round,
        e.Urgency,
        e.Ended,
        e.InitiativeTrack.ToList(),
        e.CurrentTurn,
        e.PlayedCards.ToList(),
        e.Combatants.Select(c =>
        {
            var card = catalog?.FindCombatant(c.Card);
            return new PlayerCombatant(c.Instance, c.Card, c.Family, c.Number, c.InitiativeCards.ToList(), c.Damage, c.Effects.ToList(), c.Defeated, card?.Name, card?.Image, card?.Stats?.Health?.Value);
        }).ToList(),
        e.Activation is { } a ? new PlayerActivation(a.InitiativeCard, a.Figures.ToList()) : null,
        e.Adventurers.ToDictionary(f => f.Name, f => (IReadOnlyList<string>)f.Effects.ToList()));
}
