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
    IReadOnlyList<PlayerLogEntry> Log,
    IReadOnlyList<PlayerRequest> Requests,
    string Autonomy,
    IReadOnlyList<ShopItem>? Shop,
    IReadOnlyList<UpgradeOffer> Upgrades,
    PartyPreparation? Preparation,
    string? Cards = null);

/// <summary>The seat as its phone knows it.</summary>
public sealed record PlayerSeat(string Id, string DisplayName, string Color, string? Followed);

/// <summary>A party member as the table sees them: never their items, disciplines or XP.</summary>
public sealed record PartyMemberView(string Name, int Damage, int Health, bool Defeated, bool Injured, bool Unselectable, bool Active, bool Mine, bool Held);

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
    IReadOnlyDictionary<string, IReadOnlyList<string>> AdventurerEffects,
    PlayerBoard? Board,
    IReadOnlyList<PlayerAlly> Allies,
    IReadOnlyList<PlayerAllyOption> AllyOptions);

/// <summary>An ally on the board, played by the players: its card is theirs to read.</summary>
public sealed record PlayerAlly(
    string Id,
    string Family,
    LocalizedText Name,
    string? Image,
    string TrackCard,
    string? Owner,
    bool Esper,
    int Damage,
    int? Health,
    int? Force,
    int Stamina,
    int StaminaMax,
    IReadOnlyList<string> Effects,
    bool Defeated);

/// <summary>What one of the seat's adventurers may bring or become, with the cards (a phone reads no combatant card).</summary>
public sealed record PlayerAllyOption(string Adventurer, string Kind, IReadOnlyList<PlayerCard> Cards);

public sealed record PlayerCard(string Id, LocalizedText? Name, string? Image);

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
        Catalog? catalog = null,
        Scenario? scenario = null,
        Scenario? played = null)
    {
        var campaign = state.Campaign;
        var active = campaign.ActiveGroup?.ToHashSet()
            ?? campaign.Party.Where(a => !a.Injured && !a.Unselectable).Select(a => a.Name).ToHashSet();
        var mine = campaign.Party.Where(a => adventurers.Contains(a.Name)).ToList();
        var encounter = state.Encounter is { } e ? EncounterOf(e, played ?? scenario, catalog, adventurers) : null;

        return new PlayerView(
            version,
            gameId,
            scenarioTitle,
            room?.Code,
            seat is null ? null : new PlayerSeat(seat.Id, seat.DisplayName, seat.Color, seat.Followed),
            Impersonated: seat is null,
            mine,
            campaign.Party.Select(a => new PartyMemberView(a.Name, a.Damage, a.Health, a.Defeated, a.Injured, a.Unselectable, active.Contains(a.Name), adventurers.Contains(a.Name),
                room?.Seats.Any(s => s.Adventurers.Contains(a.Name)) == true)).ToList(),
            campaign.Gold,
            campaign.Stash,
            campaign.Unassigned,
            encounter,
            PublicLog(state.Log),
            // The seat's own requests: the pending ones and the last answers
            room is null || seat is null ? [] : room.Requests.Where(r => r.SeatId == seat.Id).TakeLast(8).ToList(),
            room?.Autonomy ?? RoomAutonomy.ValidateAll,
            PlayerShop.Of(state, scenario, catalog),
            PlayerShop.UpgradesOf(state, catalog),
            // Only what the rules offer: nothing of the scenario's story
            state.Preparation);
    }

    /// <summary>
    /// What the table hears (room-spec §8): the scenes read aloud, the rounds and turns, damage, defeats, loot and
    /// rewards. Never the hidden texts and rules, the AI, the GM's answers, the variables nor the spawn checks; "text"
    /// only between encounters: during one, it is a hidden text's, often written for the GM ("reveal the hall").
    /// </summary>
    public static readonly IReadOnlySet<string> PublicLogCodes = new HashSet<string>
    {
        "scene", "text", "encounter", "navigate", "revealArea", "roundStart", "roundEnd", "turn", "urgency", "urgencyDefeat",
        "damage", "defeated", "adventurerDamage", "adventurerDefeated", "stamina", "adventurersPrepared", "restoreAdventurers",
        "encounterEnd", "reward", "scenarioEnd", "partyDefeated", "adventurerReturns",
        "achievement", "achievementText", "abraxisStart", "abraxisRound", "abraxisEnd",
        "gainGold", "gainXp", "gainItem", "gainDiscipline", "revealHiddenCard", "itemAdded", "itemMoved", "itemUsed", "itemDiscarded", "itemSold", "itemFlipped", "consumableDiscarded",
        "lootGold", "lootItem", "lootMonster", "lootUnique", "lootNone",
        "upgradeUnlocked", "upgradeAttached", "upgradeDetached", "disciplineLearnt", "disciplineForgotten", "respec",
        "setOut", "allyPlaced", "allyJoined", "allyStays", "allySummoned", "allyDamage", "allyDefeated", "allyDismissed",
        "summonToken", "conduitEarned", "conduitForm", "conduitLeft",
        "joinParty", "leaveParty", "activeGroup", "adventurerStatus",
        "storyRound", "storyRoundReturn", "storyRoundExplore", "sideQuest", "ventureForth", "storeUnique", "bountyStart", "bountyEnd",
    };

    /// <summary>The last lines a phone keeps (the whole view is sent at each change).</summary>
    public const int LogLength = 80;

    private static List<PlayerLogEntry> PublicLog(IReadOnlyList<LogEntry> log)
    {
        var lines = new List<PlayerLogEntry>();
        var fighting = false;
        for (var index = 0; index < log.Count; index++)
        {
            var code = log[index].Code;
            fighting = code switch
            {
                "encounter" => true,
                "encounterEnd" or "scene" or "storyRound" or "storyRoundReturn" => false,
                _ => fighting,
            };
            if (PublicLogCodes.Contains(code) && !(fighting && code == "text"))
            {
                lines.Add(new PlayerLogEntry(index, log[index]));
            }
        }

        return lines.TakeLast(LogLength).ToList();
    }

    private static PlayerEncounter EncounterOf(EncounterState e, Scenario? played, Catalog? catalog, IReadOnlyCollection<string> mine)
    {
        // A figure standing under the fog (moved there, or saved before they waited off the board) is not shown
        var node = played?.Nodes.FirstOrDefault(n => n.Id == e.Node);
        bool UnderFog(CombatantState c) =>
            node is not null && catalog is not null && c.Position is { } at && FogOfWar.HiddenAreaAt(node, e, catalog, at) is not null;
        var inView = e.Combatants.Where(c => c.Defeated || !UnderFog(c)).ToList();
        return new PlayerEncounter(
        e.Round,
        e.Urgency,
        e.Ended,
        e.InitiativeTrack.ToList(),
        e.CurrentTurn,
        e.PlayedCards.ToList(),
        inView.Select(c =>
        {
            var card = catalog?.FindCombatant(c.Card);
            return new PlayerCombatant(c.Instance, c.Card, c.Family, c.Number, c.InitiativeCards.ToList(), c.Damage, c.Effects.ToList(), c.Defeated, card?.Name, card?.Image, c.Health ?? card?.Stats?.Health?.Value);
        }).ToList(),
        e.Activation is { } a ? new PlayerActivation(a.InitiativeCard, a.Figures.ToList()) : null,
        e.Adventurers.ToDictionary(f => f.Name, f => (IReadOnlyList<string>)f.Effects.ToList()),
        PlayerBoard.Of(e, played, catalog),
        e.Allies.Select(a => new PlayerAlly(a.Id, a.Family, a.Name, catalog?.FindCombatant(a.Card)?.Image, a.TrackCard, a.Owner, a.Esper,
            a.Damage, a.Health, a.Force, a.Stamina, a.StaminaMax, a.Effects.ToList(), a.Defeated)).ToList(),
        e.AllyOptions.Where(o => mine.Contains(o.Adventurer))
            .Select(o => new PlayerAllyOption(o.Adventurer, o.Kind, o.Cards.Select(id => new PlayerCard(id, catalog?.FindCombatant(id)?.Name, catalog?.FindCombatant(id)?.Image)).ToList()))
            .ToList());
    }
}
