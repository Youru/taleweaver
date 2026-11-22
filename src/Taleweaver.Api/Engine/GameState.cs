using System.Text.Json;
using System.Text.Json.Serialization;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Everything the companion tracks during a game (data model §4). The engine mutates a private copy
/// (<see cref="Clone"/>), so from the outside a command never changes the state it was given.
/// </summary>
public sealed class GameState
{
    public required string Scenario { get; set; }

    public GameStatus Status { get; set; } = GameStatus.InProgress;

    /// <summary>Where the scenario ended ("@end", "@currentStoryRound"…), once finished.</summary>
    public string? Ending { get; set; }

    public required CampaignState Campaign { get; set; }

    /// <summary>The Bounty being played from a Story Round: its nodes are the ones read until it ends.</summary>
    public BountyState? Bounty { get; set; }

    public EncounterState? Encounter { get; set; }

    /// <summary>The optional rules of this game; null when it follows the rulebook as it is.</summary>
    public GameOptions? Options { get; set; }

    /// <summary>The game of Abraxis Battle Dice being played (rulebook p. 85).</summary>
    public AbraxisGame? Abraxis { get; set; }

    /// <summary>The party being made: nothing of the scenario is read until it sets out.</summary>
    public PartyPreparation? Preparation { get; set; }

    /// <summary>The question the GM must answer before anything else can happen.</summary>
    public Prompt? Prompt { get; set; }

    /// <summary>Scenario actions still to run, first one next (a question pauses them).</summary>
    public List<ScenarioAction> PendingActions { get; set; } = [];

    /// <summary>Everything shown to the GM, in order.</summary>
    public List<LogEntry> Log { get; set; } = [];

    /// <summary>Number of random draws made so far (keeps the seeded sequence going after a reload).</summary>
    public int RandomDraws { get; set; }

    public GameState Clone() =>
        JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(this, JsonDefaults.Options), JsonDefaults.Options)!;
}

/// <summary>Written as a word (the front reads words); the saves that wrote a number are still read.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameStatus>))]
public enum GameStatus
{
    InProgress,
    Finished,
}

/// <summary>The adventure sheet: what persists from one node to the next.</summary>
public sealed class CampaignState
{
    public string? CurrentNode { get; set; }

    public List<string> VisitedNodes { get; set; } = [];

    public int Gold { get; set; }

    public List<AdventurerState> Party { get; set; } = [];

    /// <summary>
    /// Every item gained, in order: catalogue ids, or the scenario's description ("1 rare item"). A history:
    /// what the party holds now is in <see cref="Stash"/>, <see cref="Unassigned"/> and each adventurer's items.
    /// </summary>
    public List<string> Items { get; set; } = [];

    /// <summary>The Party Stash (rulebook p. 25): items the party owns without carrying them.</summary>
    public List<OwnedItem> Stash { get; set; } = [];

    /// <summary>Items gained (loot, rewards) that the GM has not handed to an adventurer or the stash yet.</summary>
    public List<OwnedItem> Unassigned { get; set; } = [];

    /// <summary>Number of items handed out so far: the next one is "item-&lt;n + 1&gt;".</summary>
    public int ItemCount { get; set; }

    public List<string> RevealedHiddenCards { get; set; } = [];

    /// <summary>Combatant Loot cards drawn since the last Restore, between two encounters: out of the next one's deck (p. 57, 96).</summary>
    public List<string> LootDrawn { get; set; } = [];

    /// <summary>Scenarios played to their end, won or lost (Additional Prerequisites: "after you've attempted the Bounty").</summary>
    public List<string> AttemptedScenarios { get; set; } = [];

    public Dictionary<string, JsonElement> Variables { get; set; } = [];

    /// <summary>
    /// The Active Group (rulebook p. 11): the adventurers who play the encounters, changed when the story says so
    /// (most often in a Story Round). Null: every member of the party who is neither Injured nor Unselectable.
    /// </summary>
    public List<string>? ActiveGroup { get; set; }

    /// <summary>The Story Round the party is in (or went on a Side Quest from), null elsewhere.</summary>
    public StoryRoundState? StoryRound { get; set; }

    /// <summary>The Unique Items drawn for each named Story Round's Store, by node: drawn once, never redrawn (p. 13).</summary>
    public Dictionary<string, List<string>> StoreUniques { get; set; } = [];

    /// <summary>Side Quests attempted this Adventure: none can be chosen twice (p. 13).</summary>
    public List<string> SideQuestsAttempted { get; set; } = [];

    /// <summary>Story Rounds the party Ventured Forth from: they cannot return to them.</summary>
    public List<string> StoryRoundsLeft { get; set; } = [];

    /// <summary>Bounties won: never attempted again in this Adventure (p. 13).</summary>
    public List<string> BountiesCompleted { get; set; } = [];

    /// <summary>Bounties attempted, won or lost.</summary>
    public List<string> BountiesAttempted { get; set; } = [];

    /// <summary>Story Rounds where a Bounty was attempted: only one per named Story Round.</summary>
    public List<string> BountyStoryRounds { get; set; } = [];

    /// <summary>Item Upgrades unlocked (Adventure Sheet): for sale at every Shop and Train from now on (p. 28).</summary>
    public List<string> UnlockedUpgrades { get; set; } = [];

    /// <summary>What figures carry, or put down.</summary>
    public List<CarriedThing> Carried { get; set; } = [];

    public List<Counter> Counters { get; set; } = [];

    /// <summary>The boards an encounter to come keeps.</summary>
    public List<KeptBoard> Boards { get; set; } = [];

    /// <summary>Who may Re-Equip for free now (a gain, a Restore).</summary>
    public List<string> FreeReequip { get; set; } = [];

    /// <summary>The Adventure Sheet's checklist of Achievements.</summary>
    public List<CompletedAchievement> Achievements { get; set; } = [];

    /// <summary>The adventurers who used their free Re-Spec (XP Leniency).</summary>
    public List<string> FreeRespecs { get; set; } = [];

    /// <summary>Loyal Espers between two encounters (p. 75).</summary>
    public List<AllyState> KeptAllies { get; set; } = [];

    /// <summary>Espers summoned since the last Restore: not available.</summary>
    public List<string> SpentEspers { get; set; } = [];

    /// <summary>How the story's allies left their last encounter, by id.</summary>
    public Dictionary<string, AllyCondition> AllyConditions { get; set; } = [];
}

public sealed class BountyState
{
    public required string Scenario { get; set; }

    /// <summary>The Story Round the party comes back to.</summary>
    public required string StoryRound { get; set; }

    /// <summary>Kind of the last end condition reached (win, lose): a Bounty ending on a win is completed.</summary>
    public string? LastEnd { get; set; }
}

/// <summary>A Discipline learnt by an adventurer, and the XP it cost (its card's cost, +1 per Discipline of the same level).</summary>
public sealed class LearnedDiscipline
{
    public required string Id { get; set; }

    public int Xp { get; set; }
}

public sealed class StoryRoundState
{
    public required string Node { get; set; }

    /// <summary>shop (Shop and Train), explore (Side Quests and Venture Forth), or sideQuest (playing one).</summary>
    public string Phase { get; set; } = StoryRoundPhases.Shop;
}

public static class StoryRoundPhases
{
    public const string Shop = "shop";
    public const string Explore = "explore";
    public const string SideQuest = "sideQuest";
}

public sealed class AdventurerState
{
    public required string Name { get; set; }

    public int Xp { get; set; }

    /// <summary>Injured (p. 11): may not be in the Active Group until the story heals them.</summary>
    public bool Injured { get; set; }

    /// <summary>Unselectable (p. 11): away from the story, cannot be chosen nor trade, still gains every member's rewards.</summary>
    public bool Unselectable { get; set; }

    /// <summary>Disciplines learnt with XP (or given by the story): <see cref="Xp"/> counts every XP gained, spent or not.</summary>
    public List<LearnedDiscipline> Disciplines { get; set; } = [];

    /// <summary>Items equipped (rulebook p. 25: 2 hands of weapons, 1 armor, 1 core, 1 accessory, 3 relics, 3 consumables).</summary>
    public List<OwnedItem> Equipped { get; set; } = [];

    /// <summary>Items stored in the adventurer's Pack (3, more with an Adventurer's Pack): not usable until equipped.</summary>
    public List<OwnedItem> Pack { get; set; } = [];

    /// <summary>
    /// Damage tokens on the card. They stay from one encounter to the next (rulebook p. 69): only the Prepare Phase
    /// (down to half the maximum HP) and a Restore (all of them) remove them.
    /// </summary>
    public int Damage { get; set; }

    /// <summary>Defeated in the current or last encounter: back in play at the next encounter's Prepare Phase.</summary>
    public bool Defeated { get; set; }

    /// <summary>Stamina Points held (set to the Recovery value at the Prepare Phase, gained at each Refresh Phase).</summary>
    public int Stamina { get; set; }

    /// <summary>Maximum HP: the card's (Rook: 14) plus the Health of the cards equipped. Computed, never set by a command.</summary>
    public int Health { get; set; }

    /// <summary>SP gained each turn (red dots of the Stamina Recovery Bar). Computed from the card.</summary>
    public int StaminaRecovery { get; set; }

    /// <summary>Most SP the adventurer may hold (all dots of the bar). Computed from the card.</summary>
    public int StaminaMax { get; set; }

    /// <summary>Cards other than items (Disciplines…), by catalogue id: Exhausted, Used, Flipped.</summary>
    public List<string> ExhaustedCards { get; set; } = [];

    public List<string> UsedCards { get; set; } = [];

    public List<string> FlippedCards { get; set; } = [];

    /// <summary>Dodge, Heal, Intervention, Vow, Energy (p. 95).</summary>
    public Dictionary<string, int>? AbilityTokens { get; set; }

    /// <summary>Summon tokens held (p. 75).</summary>
    public int SummonTokens { get; set; }

    /// <summary>Conduit cards earned (p. 77).</summary>
    public List<string> Conduits { get; set; } = [];

    /// <summary>The Conduit card in the place of the Adventurer Card.</summary>
    public string? ConduitForm { get; set; }
}

/// <summary>
/// One item card the party holds. <see cref="Item"/> is null for an item still to be drawn by the table
/// (Item Loot: the 2 white dice; "1 common consumable"): <see cref="Tiers"/> and <see cref="Type"/> say which.
/// </summary>
public sealed class OwnedItem
{
    /// <summary>"item-3": unique in the game, so that two copies of a card can be told apart.</summary>
    public required string Id { get; set; }

    /// <summary>Catalogue id ("um1:item/ubiquitous-plank"), or null while the card is still to be drawn.</summary>
    public string? Item { get; set; }

    /// <summary>A card still to be drawn: its tiers ("rare").</summary>
    public List<string>? Tiers { get; set; }

    /// <summary>A card still to be drawn: its type ("consumable"), when given.</summary>
    public string? Type { get; set; }

    /// <summary>Where it came from: itemLoot, monsterLoot, uniqueLoot, scenario, gm.</summary>
    public string? Source { get; set; }

    /// <summary>Equipped on its back side (DOUBLE), or Flipped by an ability.</summary>
    public bool Flipped { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Exhausted { get; set; }

    /// <summary>A Used token is on it (Per Encounter).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Used { get; set; }

    /// <summary>Item Upgrades attached to the card (catalogue ids, p. 28).</summary>
    public List<string> Upgrades { get; set; } = [];
}

public sealed class EncounterState
{
    public required string Node { get; set; }

    /// <summary>0 until the first round starts.</summary>
    public int Round { get; set; }

    public int Urgency { get; set; }

    /// <summary>An end condition was reached: the reward is being applied.</summary>
    public bool Ended { get; set; }

    /// <summary>Initiative cards, front first: catalogue ids, or "adventurer:&lt;name&gt;".</summary>
    public List<string> InitiativeTrack { get; set; } = [];

    /// <summary>Index of the card taking its turn; -1 before the first turn of the round.</summary>
    public int CurrentTurn { get; set; } = -1;

    /// <summary>
    /// Cards that had their turn this round, the one playing included: once cards are moved on the track,
    /// the next turn is the card nearest the front that has not played (rulebook p. 36), not the next index.
    /// </summary>
    public List<string> PlayedCards { get; set; } = [];

    public List<CombatantState> Combatants { get; set; } = [];

    /// <summary>
    /// Combatants under the fog (§3.4): not in play (no figure, no card on the track) until their area is revealed.
    /// </summary>
    public List<WaitingCombatant> Waiting { get; set; } = [];

    /// <summary>Command Combatants, on the adventurers' side.</summary>
    public List<AllyState> Allies { get; set; } = [];

    /// <summary>"summoning:Rook": Abilities used once per encounter.</summary>
    public List<string> UsedSummons { get; set; } = [];

    /// <summary>What the Assemblage Disciplines allow now. Computed.</summary>
    public List<AllyOption> AllyOptions { get; set; } = [];

    public List<BoardToken> Tokens { get; set; } = [];

    public List<string> PlacedDiagrams { get; set; } = [];

    /// <summary>A NAVIGATE cleared the encounter's own map: only the placed diagrams are on the table.</summary>
    public bool MapCleared { get; set; }

    /// <summary>Combatant Loot cards left in the deck ("gold:7", "item", "monster", "unique"), in no particular order.</summary>
    public List<string> LootDeck { get; set; } = [];

    /// <summary>Cards drawn since the last Restore: they return to the deck when the party Restores.</summary>
    public List<string> LootDrawn { get; set; } = [];

    /// <summary>Gold of the Combatant Loot cards drawn, added to the party's at the end of the encounter.</summary>
    public int LootGold { get; set; }

    public List<string> ReadHiddenRules { get; set; } = [];

    public List<string> ActiveEndConditions { get; set; } = [];

    public List<string> FiredEvents { get; set; } = [];

    public List<string> ActivatedTokens { get; set; } = [];

    /// <summary>Fog areas revealed so far (those revealed at the start included).</summary>
    public List<string> RevealedAreas { get; set; } = [];

    /// <summary>The adventurers' figures, placed in the starting zone, then moved by the GM.</summary>
    public List<AdventurerFigure> Adventurers { get; set; } = [];

    /// <summary>
    /// The adventurers who had their first turn in this encounter. Null in an encounter begun before engine 13: the
    /// first turn was then the first round's.
    /// </summary>
    [KeptWhenEmpty]
    public List<string>? HadFirstTurn { get; set; }

    /// <summary>Adventurers and allies who began the turn under way Paralyzed: the token leaves at its end.</summary>
    public List<string> ParalyzedThisTurn { get; set; } = [];

    /// <summary>Who lost a turn to Paralyze: Immune to it until the end of the encounter.</summary>
    public List<string> ParalyzeImmune { get; set; } = [];

    /// <summary>AI steps added by the scenario, by family (appendAiStep).</summary>
    public Dictionary<string, List<AiStep>> ExtraAiSteps { get; set; } = [];

    /// <summary>What happened this round, for the Urgency rule.</summary>
    public RoundLog RoundLog { get; set; } = new();

    /// <summary>Dealt before the first round: not shuffled again for it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TrackDealt { get; set; }

    /// <summary>Cards never shuffled, and their place: front, back, or a number (1 first).</summary>
    public Dictionary<string, string>? FixedCards { get; set; }

    /// <summary>The figures of the turn under way whose end is still to come.</summary>
    public List<string> TurnOf { get; set; } = [];

    public List<DelayedEffect> Delayed { get; set; } = [];

    /// <summary>modifyFigure on a family: for those still to come.</summary>
    public List<ScenarioAction> Modifiers { get; set; } = [];

    /// <summary>The reactions under way, the last one first to end: the turn waits.</summary>
    public List<Reaction> Reactions { get; set; } = [];

    /// <summary>Who made their Break Attack this turn.</summary>
    public List<string> BreakAttacks { get; set; } = [];

    /// <summary>Who made their Move Action this turn.</summary>
    public List<string> Moved { get; set; } = [];

    /// <summary>Who placed a Tether, on whom.</summary>
    public Dictionary<string, string>? Tethers { get; set; }

    /// <summary>The AI step that may still prove impossible.</summary>
    public LastAiStep? LastStep { get; set; }

    /// <summary>The adventurers the encounter imposes; null: the Active Group.</summary>
    public List<string>? Participants { get; set; }

    /// <summary>The group taking its turn, while its figures go through their AI steps.</summary>
    public Activation? Activation { get; set; }
}

public sealed class CombatantState
{
    /// <summary>Unique name in the encounter: "fragor-2", or the scenario's instance ("terror").</summary>
    public required string Instance { get; set; }

    public required string Card { get; set; }

    public required string Family { get; set; }

    /// <summary>Activation number of the figure (base snap colour), 0 for a story combatant.</summary>
    public int Number { get; set; }

    public List<string> InitiativeCards { get; set; } = [];

    /// <summary>Maximum HP when an option changes the card's (Tougher Mobs); null: the card's.</summary>
    public int? Health { get; set; }

    public string? Figure { get; set; }

    /// <summary>Square of the figure: where the scenario placed it, then where the GM moved it (optional).</summary>
    public Cell? Position { get; set; }

    public int Damage { get; set; }

    public List<string> Effects { get; set; } = [];

    public bool Aggro { get; set; }

    /// <summary>Target of an opponent's Attack, Spell or Ability since its last turn, hit or not (Golden Rule #4: Aggro).</summary>
    public bool Attacked { get; set; }

    /// <summary>The figure that Spawned it: such an opponent drops no Combatant Loot (p. 57).</summary>
    public string? SpawnedBy { get; set; }

    public bool Defeated { get; set; }

    public Dictionary<string, int>? AbilityTokens { get; set; }

    /// <summary>The name of a figure the scenario describes (its card is "figure:&lt;id&gt;").</summary>
    public Dictionary<string, string>? Name { get; set; }

    /// <summary>Numbers in the place of the card's (maximum HP: <see cref="Health"/>).</summary>
    public FigureStats? Stats { get; set; }

    /// <summary>"players", or another camp; null: the adventurers' opponents.</summary>
    public string? Side { get; set; }

    public List<string> AddedTags { get; set; } = [];

    public List<string> RemovedTags { get; set; } = [];

    /// <summary>AI steps ignored, 1 first.</summary>
    public List<int> SkippedSteps { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Asleep { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WakesWhenDamaged { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool NoLoot { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Untargetable { get; set; }

    /// <summary>An opposing Esper's summoner, and the Force it gives.</summary>
    public string? Summoner { get; set; }

    public int? Force { get; set; }
}

/// <summary>One figure to come, and the fog area it waits for; its card and number are chosen when it appears.</summary>
public sealed class WaitingCombatant
{
    public required string Area { get; set; }

    public required ScenarioAction Action { get; set; }

    public string? InitiativeCard { get; set; }

    /// <summary>An Alternate or Variation card may come instead (never for the encounter's Placement Phase).</summary>
    public bool PossibleSpawns { get; set; }
}

public sealed class AdventurerFigure
{
    public required string Name { get; set; }

    public Cell? Position { get; set; }

    /// <summary>Effects on the adventurer this encounter (the AI aims at them: "an opponent with Darkness").</summary>
    public List<string> Effects { get; set; } = [];

    /// <summary>Defeated: off the board until the next encounter (not a figure to place).</summary>
    public bool Defeated { get; set; }
}

public sealed class BoardToken
{
    public required string Id { get; set; }

    /// <summary>"type:colour" (e.g. "objective:red"), the key used by the scenario's tokens and triggers.</summary>
    public required string Key { get; set; }

    /// <summary>Token id in the catalogue (e.g. "um1:token/objective").</summary>
    public string? Type { get; set; }

    public string? Color { get; set; }

    /// <summary>The picture chosen on the map, when not its type's (a variant).</summary>
    public string? Image { get; set; }

    /// <summary>Squares covered, when the scenario gives them.</summary>
    public Rect? Area { get; set; }

    public bool Removed { get; set; }

    /// <summary>A Bulb that went off.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Flipped { get; set; }
}

/// <summary>A Counter, Break Attack or Redirect, and whom it is against.</summary>
public sealed record Reaction(string Kind, string Figure, string? Against);

public sealed class LastAiStep
{
    public required Activation Before { get; set; }

    public bool RoundHadTrueStep { get; set; }

    public bool Aggro { get; set; }

    public bool Attacked { get; set; }
}

public sealed class RoundLog
{
    /// <summary>A step was true, or a turn was lost to Paralyze, which counts as taken (p. 18).</summary>
    public bool AiStepTrue { get; set; }

    public int Defeated { get; set; }

    public int Spawned { get; set; }
}

public sealed class Activation
{
    public required string InitiativeCard { get; set; }

    /// <summary>Figures still to act, in activation order; the first one is acting.</summary>
    public List<string> Figures { get; set; } = [];

    public int Step { get; set; }

    /// <summary>The acting figure began its turn Paralyzed: no AI step, the token leaves at the end.</summary>
    public bool Paralyzed { get; set; }

    /// <summary>The acting figure was Paralyzed during its turn: no more step, the token stays for the next turn.</summary>
    public bool Interrupted { get; set; }

    public bool AnyStepTrue { get; set; }

    /// <summary>The question asked is the step's ContinueDownIf, not its condition.</summary>
    public bool AskingContinue { get; set; }
}

/// <summary>A question to the GM. The answer is one of <see cref="Options"/>, or a number when <see cref="Kind"/> is "number".</summary>
public sealed class Prompt
{
    /// <summary>continue, choice, skillCheck, aiCondition, number, adventurer, spawnPossible, storyRound, abraxis, activeGroup, arrival, status.</summary>
    public required string Kind { get; set; }

    public LocalizedText? Question { get; set; }

    public List<PromptOption> Options { get; set; } = [];

    /// <summary>Scenario action waiting for the answer (askChoice, askSkillCheck, restoreCombatant, spawn…).</summary>
    public ScenarioAction? Action { get; set; }

    /// <summary>Combatant concerned (AI step, restored combatant).</summary>
    public string? Instance { get; set; }

    /// <summary>Who decides: the GM types their answer.</summary>
    public string? Decider { get; set; }

    /// <summary>activeGroup: how many to choose.</summary>
    public int? Max { get; set; }

    /// <summary>skillCheck: who may test, with their Skills.</summary>
    public List<SkillTester>? Testers { get; set; }

    /// <summary>skillCheck: the Skill and difficulty of each option.</summary>
    public List<SkillOption>? Checks { get; set; }
}

public sealed record SkillTester(string Name, IReadOnlyDictionary<string, int> Skills);

public sealed record SkillOption(string? Skill, int? Difficulty);

public sealed class Counter
{
    public required string Id { get; set; }

    public Dictionary<string, string>? Name { get; set; }

    public int Value { get; set; }

    public int? Min { get; set; }

    /// <summary>A token's id, or a figure; null: the encounter's.</summary>
    public string? On { get; set; }

    /// <summary>Goes on into the next encounter.</summary>
    public bool Keep { get; set; }
}

public sealed class DelayedEffect
{
    public required string Id { get; set; }

    public int Round { get; set; }

    /// <summary>start, or end.</summary>
    public required string At { get; set; }

    public bool Repeats { get; set; }

    public Dictionary<string, string>? Text { get; set; }

    public List<ScenarioAction> Actions { get; set; } = [];
}

public sealed class KeptBoard
{
    public required string From { get; set; }

    public List<CombatantState> Combatants { get; set; } = [];

    public List<WaitingCombatant> Waiting { get; set; } = [];

    public List<BoardToken> Tokens { get; set; } = [];

    public List<string> RevealedAreas { get; set; } = [];

    public List<string> PlacedDiagrams { get; set; } = [];

    public List<AdventurerFigure> Adventurers { get; set; } = [];

    public List<AllyState> Allies { get; set; } = [];

    public List<Counter> Counters { get; set; } = [];

    public List<CarriedThing> Carried { get; set; } = [];
}

public sealed class CarriedThing
{
    public required string Id { get; set; }

    public Dictionary<string, string>? Name { get; set; }

    /// <summary>The figure holding it; null: lying, or lost.</summary>
    public string? Carrier { get; set; }

    public Cell? Position { get; set; }

    /// <summary>Taken into the next encounter.</summary>
    public bool Keep { get; set; }

    public bool Lost { get; set; }

    public bool SurvivesDangerous { get; set; }

    /// <summary>The token it is on the board, when it is one.</summary>
    public string? TokenKey { get; set; }

    public string? TokenType { get; set; }

    public string? Color { get; set; }
}

public sealed record PromptOption(string Id, LocalizedText? Label = null);

/// <summary>
/// A line shown to the GM. <see cref="Code"/> says what it is (narrative, hiddenText, aiInstruction, spawn…);
/// <see cref="Text"/> is the scenario's or the card's own text; <see cref="Args"/> holds the values the
/// front needs to phrase the rest in the reader's language.
/// </summary>
public sealed record LogEntry(string Code, LocalizedText? Text = null, IReadOnlyDictionary<string, string>? Args = null);

/// <summary>A line of the Adventure Sheet's Achievements.</summary>
public sealed class CompletedAchievement
{
    public required string Id { get; set; }

    public required string Scenario { get; set; }

    public required string Node { get; set; }

    public required string Adventurer { get; set; }

    public Dictionary<string, string>? Name { get; set; }
}

public sealed class AbraxisGame
{
    public int Round { get; set; }

    public Dictionary<string, string>? Name { get; set; }

    /// <summary>The card's special rule.</summary>
    public Dictionary<string, string>? Rule { get; set; }

    public List<AbraxisPlayer> Players { get; set; } = [];
}

public sealed class AbraxisPlayer
{
    public required string Name { get; set; }

    /// <summary>0: out of the game.</summary>
    public int Points { get; set; }
}
