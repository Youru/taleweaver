using System.Text.Json;
using System.Text.Json.Serialization;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Content;

/// <summary>A scenario as written by the editor (data model §3).</summary>
public sealed record Scenario
{
    public int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required LocalizedText Title { get; init; }

    /// <summary>bounty, adventure, crawl…</summary>
    public string? Kind { get; init; }

    public string? Mode { get; init; }

    /// <summary>A Bounty's difficulty, in skulls (rulebook p. 13).</summary>
    public int? Difficulty { get; init; }

    public IReadOnlyList<string> Packs { get; init; } = [];

    public ScenarioSource? Source { get; init; }

    public Party? Party { get; init; }

    public IReadOnlyList<JsonElement> Variables { get; init; } = [];

    public required string Start { get; init; }

    /// <summary>The pack of the scenarios made in the workshop: "homebrew:scenario/&lt;name&gt;".</summary>
    public const string HomebrewPack = "homebrew";

    /// <summary>What only the workshop reads (where each node sits on the graph); the engine ignores it.</summary>
    public JsonElement? Editor { get; init; }

    /// <summary>
    /// The chapters of a campaign folder, in order (see Storage.CampaignFolder); null for a single-file scenario.
    /// Each node of a chapter names it in <see cref="Node.Chapter"/>.
    /// </summary>
    public IReadOnlyList<ScenarioChapter>? Chapters { get; init; }

    public IReadOnlyList<Node> Nodes { get; init; } = [];
}

/// <summary>A chapter of a campaign folder: its folder name and its title.</summary>
public sealed record ScenarioChapter(string Id, LocalizedText? Title = null);

public sealed record ScenarioSource(string? File, string? Reference, string? Version);

/// <summary>Who may set out and with what (roadmap §5.2): the scenario launched decides, never the app.</summary>
public sealed record Party
{
    /// <summary>The most adventurers (6 when not given).</summary>
    public int? Size { get; init; }

    /// <summary>The fewest (1, or the number of those required).</summary>
    public int? MinSize { get; init; }

    /// <summary>Gold of the whole party, to spend at the start when <see cref="PartyStart.Items"/> opens a Store.</summary>
    public int? StartingGold { get; init; }

    /// <summary>XP of each adventurer.</summary>
    public int? StartingXp { get; init; }

    /// <summary>
    /// Ids, names or "pack:adventurer/*". Null: every adventurer. Those still to unlock (hidden) come only when named
    /// here one by one, or when the GM shows them.
    /// </summary>
    public IReadOnlyList<string>? AllowedAdventurers { get; init; }

    public IReadOnlyList<string>? ExcludedAdventurers { get; init; }

    /// <summary>The story imposes them: in the party from the preparation on.</summary>
    public IReadOnlyList<string>? RequiredAdventurers { get; init; }

    /// <summary>An adventurer without a card may be named (the usual card); by default when no list restricts the choice.</summary>
    public bool? Custom { get; init; }

    public PartyStart? Start { get; init; }
}

public sealed record PartyStart
{
    /// <summary>A Store open before the departure; null: nothing to buy.</summary>
    public StartItems? Items { get; init; }

    /// <summary>What may be learnt before the departure; null: any Discipline the starting XP pays.</summary>
    public StartDisciplines? Disciplines { get; init; }

    public IReadOnlyList<StarterKit> Kits { get; init; } = [];
}

public sealed record StartItems
{
    public IReadOnlyList<string> Tiers { get; init; } = [];

    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Cards bought by each adventurer, at most.</summary>
    public int? Max { get; init; }
}

public sealed record StartDisciplines
{
    public IReadOnlyList<string>? Trees { get; init; }

    public int? MaxLevel { get; init; }

    /// <summary>Disciplines bought by each adventurer, at most (the kit's aside).</summary>
    public int? Max { get; init; }
}

/// <summary>A starting pack: free cards. One with an <see cref="Adventurer"/> is theirs alone.</summary>
public sealed record StarterKit
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public string? Adventurer { get; init; }

    public IReadOnlyList<string> Items { get; init; } = [];

    public IReadOnlyList<string> Disciplines { get; init; } = [];
}

/// <summary>A step of the scenario (§3.2). Encounter fields (§3.3) are only set when <see cref="Type"/> is "encounter".</summary>
public sealed record Node
{
    public const string EncounterType = "encounter";

    public required string Id { get; init; }

    /// <summary>scene, encounter, choice…</summary>
    public required string Type { get; init; }

    /// <summary>Chapter of a campaign folder the step belongs to (one of <see cref="Scenario.Chapters"/>; not written in the step's file).</summary>
    public string? Chapter { get; init; }

    public LocalizedText? Name { get; init; }

    public LocalizedText? Title { get; init; }

    public int? Page { get; init; }

    public LocalizedText? Narrative { get; init; }

    public IReadOnlyList<ScenarioAction> Mechanics { get; init; } = [];

    /// <summary>Next node id, or a reserved target starting with "@" (e.g. "@end").</summary>
    public string? Next { get; init; }

    public LocalizedText? Intro { get; init; }

    /// <summary>What the GM does by hand in this step (shown in the encounter, never to the players).</summary>
    public LocalizedText? GmNote { get; init; }

    public string? LocationType { get; init; }

    public string? LootLevel { get; init; }

    public BoardMap? Map { get; init; }

    public Setup? Setup { get; init; }

    public IReadOnlyList<SpecialRule> SpecialRules { get; init; } = [];

    /// <summary>Behaviour of the tokens of the encounter, by "type:colour" key (e.g. "objective:red").</summary>
    public IReadOnlyDictionary<string, TokenRule> Tokens { get; init; } = new Dictionary<string, TokenRule>();

    public IReadOnlyList<EndCondition> EndConditions { get; init; } = [];

    public IReadOnlyList<ScenarioEvent> Events { get; init; } = [];

    public IReadOnlyList<HiddenRule> HiddenRules { get; init; } = [];

    /// <summary>The encounter's Achievements (rulebook p. 17).</summary>
    public IReadOnlyList<Achievement> Achievements { get; init; } = [];

    public IReadOnlyList<Diagram> Diagrams { get; init; } = [];

    /// <summary>The encounter whose board this one keeps: figures, squares, Damage, tokens, counters.</summary>
    public string? KeepBoardFrom { get; init; }

    /// <summary>Figures written in the rules of the encounter: placed or spawned with the ref "figure:&lt;id&gt;".</summary>
    public IReadOnlyList<SceneFigure> Figures { get; init; } = [];

    /// <summary>The Espers an opponent may summon (p. 76).</summary>
    public IReadOnlyList<EncounterEsper> EncounterEspers { get; init; } = [];

    /// <summary>The adventurers this encounter imposes: they play it, the Active Group stays as it is.</summary>
    public IReadOnlyList<string>? Participants { get; init; }

    /// <summary>Story Round (rulebook p. 13): the Items for sale in the Shop and Train phase.</summary>
    public Store? Store { get; init; }

    /// <summary>Story Round: the Side Quests of the Explore phase, each attempted once in the Adventure.</summary>
    public IReadOnlyList<StoryExit> SideQuests { get; init; } = [];

    /// <summary>Story Round: where the party may Venture Forth (one choice, or several).</summary>
    public IReadOnlyList<StoryExit> VentureForth { get; init; } = [];

    /// <summary>Story Round: the most difficult Bounty (in skulls) that may be attempted here; null when none.</summary>
    public int? BountyDifficulty { get; init; }

    [JsonIgnore]
    public bool IsEncounter => Type == EncounterType;

    public const string StoryRoundType = "storyRound";

    [JsonIgnore]
    public bool IsStoryRound => Type == StoryRoundType;

    /// <summary>Every node this one may lead to: next, end conditions, Story Round exits.</summary>
    [JsonIgnore]
    public IEnumerable<string?> Exits =>
        EndConditions.Select(e => e.Next).Prepend(Next).Concat(SideQuests.Select(q => q.Node)).Concat(VentureForth.Select(v => v.Node));
}

/// <summary>
/// The Store of a Story Round (rulebook p. 13): every non-unique card matching one of <see cref="Items"/>
/// ("all Mundane Items", "all Accessories"), the upgrades unlocked, and random Unique Items drawn once per
/// named Story Round.
/// </summary>
public sealed record Store
{
    public IReadOnlyList<StoreFilter> Items { get; init; } = [];

    public bool? Upgrades { get; init; }

    public StoreUnique? Unique { get; init; }
}

/// <summary>Cards of these tiers and these types (an empty list matches any).</summary>
public sealed record StoreFilter
{
    public IReadOnlyList<string> Tiers { get; init; } = [];

    public IReadOnlyList<string> Types { get; init; } = [];
}

public sealed record StoreUnique(string Tier, int Count = 1);

/// <summary>A choice of a Story Round: a Side Quest or a way to Venture Forth.</summary>
public sealed record StoryExit
{
    /// <summary>Side Quests: the id remembered once attempted (the node's when absent).</summary>
    public string? Id { get; init; }

    public LocalizedText? Label { get; init; }

    public required string Node { get; init; }

    public int? Page { get; init; }

    /// <summary>Offered only when all these hold (a Story Flag, p. 13).</summary>
    public IReadOnlyList<EventCondition>? If { get; init; }

    [JsonIgnore]
    public string Key => Id ?? Node;
}

/// <summary>Tiles, tokens and areas of a board (§3.4). Coordinates are in squares.</summary>
public sealed record BoardMap
{
    public int? NorthArrow { get; init; }

    public IReadOnlyList<MapTile> Tiles { get; init; } = [];

    public IReadOnlyList<MapToken> Tokens { get; init; } = [];

    public IReadOnlyList<Zone> Zones { get; init; } = [];

    public IReadOnlyList<Current> Currents { get; init; } = [];

    public IReadOnlyList<ElevationArea> Elevation { get; init; } = [];

    /// <summary>Areas of the fog of war: their tiles, and what stands on them, stay hidden until revealed.</summary>
    public IReadOnlyList<FogArea> Areas { get; init; } = [];
}

/// <summary>A tile placed with its top-left corner at (X, Y). Rotation in degrees, null when unknown. <see cref="Area"/>: fog area.</summary>
public sealed record MapTile(string Ref, int X, int Y, int? Rotation, string? Area = null);

/// <summary>An area of the fog of war (§3.4); hidden at the start when <see cref="RevealedAtStart"/> is false.</summary>
public sealed record FogArea(string Id, bool? RevealedAtStart = null, LocalizedText? Name = null);

public sealed record MapToken
{
    public required string Id { get; init; }

    /// <summary>Token id in the catalogue (e.g. "um1:token/objective").</summary>
    public required string Type { get; init; }

    public string? Color { get; init; }

    /// <summary>The picture this token shows, when not its type's (or its colour's): one of the type's variants.</summary>
    public string? Image { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int? W { get; init; }

    public int? H { get; init; }

    /// <summary>Fog area of the token (it is hidden with it).</summary>
    public string? Area { get; init; }

    [JsonIgnore]
    public Rect Footprint => new(X, Y, W ?? 1, H ?? 1);
}

public sealed record Rect(int X, int Y, int W = 1, int H = 1)
{
    public IEnumerable<Cell> Cells()
    {
        for (var i = 0; i < W; i++)
        {
            for (var j = 0; j < H; j++)
            {
                yield return new Cell(X + i, Y + j);
            }
        }
    }
}

public readonly record struct Cell(int X, int Y) : IComparable<Cell>
{
    public int CompareTo(Cell other) => X != other.X ? X.CompareTo(other.X) : Y.CompareTo(other.Y);

    public override string ToString() => $"({X}, {Y})";
}

public sealed record Zone(string Id, string? Kind, Rect Rect);

public sealed record Current
{
    public Rect? Rect { get; init; }

    public string? Tile { get; init; }

    /// <summary>Direction of the flow, null when unknown.</summary>
    public string? Direction { get; init; }

    public int Strength { get; init; }

    public IReadOnlyList<string> Tokens { get; init; } = [];

    public LocalizedText? Note { get; init; }
}

public sealed record ElevationArea(int Level, Rect Rect, LocalizedText? Note);

public sealed record Setup
{
    public IReadOnlyList<CombatantPlacement> Combatants { get; init; } = [];

    public string? AdventurersStart { get; init; }

    public IReadOnlyList<HiddenCardReveal> RevealHiddenCards { get; init; } = [];
}

/// <summary>A combatant placed by the setup: by <see cref="Family"/> (card chosen from the loot level) or by <see cref="Ref"/>.</summary>
public sealed record CombatantPlacement
{
    public string? Family { get; init; }

    public string? Ref { get; init; }

    public string? Instance { get; init; }

    public string? Figure { get; init; }

    public string? InitiativeCard { get; init; }

    public Cell? At { get; init; }

    /// <summary>Loot level of this figure's card when it differs from the encounter's ("Common Fragors").</summary>
    public string? LootLevel { get; init; }

    /// <summary>Effects the figure starts with ("burrowed").</summary>
    public IReadOnlyList<string> Effects { get; init; } = [];

    /// <summary>"players", or another camp; none: the adventurers' opponents.</summary>
    public string? Side { get; init; }

    /// <summary>An opposing Esper: the instance whose values its "*" are.</summary>
    public string? Summoner { get; init; }
}

/// <summary>A figure, or a token that may be attacked, whose numbers are in the rules of the encounter.</summary>
public sealed record SceneFigure
{
    public required string Id { get; init; }

    public LocalizedText? Name { get; init; }

    public FigureStats? Stats { get; init; }

    public string? Figure { get; init; }

    public string? InitiativeCard { get; init; }

    public string? Side { get; init; }

    public bool? Attackable { get; init; }

    /// <summary>True: a Combatant Loot card when Defeated.</summary>
    public bool? Loot { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>Numbers in the place of a card's.</summary>
public sealed record FigureStats
{
    public int? Health { get; init; }

    public int? Defense { get; init; }

    public int? Armor { get; init; }

    public int? Movement { get; init; }

    public IReadOnlyList<string>? CombatDice { get; init; }

    public IReadOnlyList<string>? ConvictionDice { get; init; }

    [JsonConverter(typeof(SingleOrArrayConverter<string>))]
    public IReadOnlyList<string>? CastingDie { get; init; }
}

public sealed record EncounterEsper(string Ref, string? Summoner = null);

public sealed record HiddenCardReveal(string Code, string? Size);

/// <summary>An Achievement: its condition is read by all, its hidden text once completed, its actions are the reward.</summary>
public sealed record Achievement
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public LocalizedText? Condition { get; init; }

    public LocalizedText? HiddenText { get; init; }

    public IReadOnlyList<ScenarioAction> Actions { get; init; } = [];
}

public sealed record SpecialRule(string Id, LocalizedText Name, LocalizedText Text);

public sealed record TokenRule
{
    /// <summary>How the token is activated (e.g. "encounterAction").</summary>
    public string? Mode { get; init; }

    public LocalizedText? Activation { get; init; }

    public IReadOnlyList<string> Events { get; init; } = [];
}

public static class BuiltInEnds
{
    public const string AllDefeated = "allDefeated";
    public const string LastUrgency = "urgency4";

    public static bool IsKnown(string builtIn) => builtIn is AllDefeated or LastUrgency;
}

public sealed record EndCondition
{
    public required string Id { get; init; }

    /// <summary>win or loss.</summary>
    public required string Kind { get; init; }

    /// <summary>False when the condition is only added later (addEndCondition).</summary>
    public bool? Active { get; init; }

    /// <summary>The defeat the rules call by themselves: allDefeated (every adventurer Defeated) or urgency4 (the last Urgency token).</summary>
    public string? BuiltIn { get; init; }

    /// <summary>The reward continues the encounter (rulebook p. 14): given once, the encounter goes on, maybe with new End Conditions.</summary>
    public bool? Continues { get; init; }

    public LocalizedText? Text { get; init; }

    public LocalizedText? Reward { get; init; }

    public IReadOnlyList<ScenarioAction> Mechanics { get; init; } = [];

    public string? Next { get; init; }
}

/// <summary>Trigger → conditions → actions (§3.6).</summary>
public sealed record ScenarioEvent
{
    public required string Id { get; init; }

    public required EventTrigger Trigger { get; init; }

    public IReadOnlyList<EventCondition> Conditions { get; init; } = [];

    public IReadOnlyList<ScenarioAction> Actions { get; init; } = [];

    public bool? Once { get; init; }

    public LocalizedText? HiddenText { get; init; }

    public LocalizedText? Note { get; init; }
}

public sealed record EventTrigger
{
    /// <summary>tokenActivated, combatantDefeated, roundStart…</summary>
    public required string On { get; init; }

    /// <summary>Token keys ("objective:red") that fire the trigger.</summary>
    [JsonConverter(typeof(SingleOrArrayConverter<string>))]
    public IReadOnlyList<string>? Token { get; init; }

    public string? Instance { get; init; }

    public string? Family { get; init; }

    /// <summary>roundStart / roundEnd: only that round.</summary>
    public int? Round { get; init; }

    /// <summary>hiddenRuleRead: only that rule.</summary>
    public string? Rule { get; init; }

    /// <summary>areaRevealed: only that area.</summary>
    public string? Area { get; init; }

    /// <summary>counter: which one, and the threshold crossed.</summary>
    public string? Counter { get; init; }

    public int? Value { get; init; }
}

/// <summary>A condition of an event: a variable test, an end-condition test, or a negation.</summary>
public sealed record EventCondition
{
    public string? Var { get; init; }

    public string? Op { get; init; }

    public JsonElement? Value { get; init; }

    public string? EndConditionActive { get; init; }

    public EventCondition? Not { get; init; }

    public IReadOnlyList<EventCondition>? All { get; init; }

    public IReadOnlyList<EventCondition>? Any { get; init; }
}

public sealed record HiddenRule
{
    public required string Id { get; init; }

    public LocalizedText? Name { get; init; }

    public LocalizedText? Text { get; init; }

    /// <summary>Id of the hidden rule this one is a part of.</summary>
    public string? PartOf { get; init; }

    public IReadOnlyList<ScenarioAction> Actions { get; init; } = [];
}

/// <summary>Extra board of an encounter (A, B, C…), placed by placeDiagram or navigate (§3.7.1).</summary>
public sealed record Diagram
{
    public required string Id { get; init; }

    public LocalizedText? Label { get; init; }

    /// <summary>
    /// The diagrams of the encounter already on the table when this one is laid (Diagram 15 builds on
    /// 11 and 13). Only the printed page uses it: it draws them faded under this one, as the books do.
    /// </summary>
    public IReadOnlyList<string> BuiltOn { get; init; } = [];

    public int? Page { get; init; }

    public BoardMap? Map { get; init; }

    public Setup? Setup { get; init; }

    public IReadOnlyList<SpecialRule> SpecialRules { get; init; } = [];

    /// <summary>Its terrain replaces the terrain already on the board.</summary>
    public bool? ReplacesTerrain { get; init; }

    public LocalizedText? Note { get; init; }
}

/// <summary>
/// One scenario action, identified by <see cref="Do"/> (showText, spawn, placeToken, readHiddenRule…).
/// The fields shared by several actions are typed; the others stay in <see cref="Extra"/> so that
/// nothing is lost when the editor saves the scenario. The engine gives each action its meaning.
/// </summary>
public sealed record ScenarioAction
{
    public required string Do { get; init; }

    public string? Rule { get; init; }

    public string? Diagram { get; init; }

    public string? EndCondition { get; init; }

    /// <summary>Token id on the map (removeToken) or token key "type:colour" (activateToken).</summary>
    public string? Token { get; init; }

    /// <summary>Token type of placeToken.</summary>
    public string? Type { get; init; }

    public string? Color { get; init; }

    public string? Family { get; init; }

    public string? Ref { get; init; }

    /// <summary>Name of a combatant in the encounter (spawn, restoreCombatant).</summary>
    public string? Instance { get; init; }

    public string? Figure { get; init; }

    public int? Count { get; init; }

    /// <summary>spawn: loot level of the card when it differs from the encounter's.</summary>
    public string? LootLevel { get; init; }

    /// <summary>spawn: effects the figure starts with ("burrowed").</summary>
    public IReadOnlyList<string>? Effects { get; init; }

    public int? Amount { get; init; }

    /// <summary>gainXp: every party member gains it.</summary>
    public bool? Each { get; init; }

    public LocalizedText? Text { get; init; }

    public LocalizedText? Question { get; init; }

    /// <summary>Who acts: a text shown to the GM, or "askAdventurer".</summary>
    public JsonElement? Who { get; init; }

    /// <summary>restoreCombatant: damage kept, or "ask".</summary>
    public JsonElement? Damage { get; init; }

    /// <summary>appendAiStep: the step added.</summary>
    public AiStep? Step { get; init; }

    /// <summary>gainItem: loot tiers to choose from.</summary>
    public IReadOnlyList<string>? Tiers { get; init; }

    /// <summary>revealHiddenCard: small, medium or large, and the code printed on the card.</summary>
    public string? Size { get; init; }

    public string? Code { get; init; }

    /// <summary>replaceInitiativeCard: card replaced and its replacement.</summary>
    public string? From { get; init; }

    public string? To { get; init; }

    /// <summary>Where a spawned figure may go (normalUnoccupied…).</summary>
    public string? Constraint { get; init; }

    /// <summary>goToNode: the node.</summary>
    public string? Node { get; init; }

    public LocalizedText? Note { get; init; }

    /// <summary>Where the action happens: a token id, or an object (on, adjacentTo, tile, where).</summary>
    public JsonElement? At { get; init; }

    public IReadOnlyList<ScenarioAction>? Actions { get; init; }

    public IReadOnlyList<ScenarioAction>? Pass { get; init; }

    /// <summary>askSkillCheck: what a Masterful Pass does, offered beside "passed" when present.</summary>
    public IReadOnlyList<ScenarioAction>? Masterful { get; init; }

    public IReadOnlyList<ScenarioAction>? Fail { get; init; }

    public IReadOnlyList<ScenarioAction>? Fallback { get; init; }

    public IReadOnlyList<ActionOption>? Options { get; init; }

    /// <summary>abraxis: the rewards by number of survivors (1, 2, 3, 4), cumulative.</summary>
    public IReadOnlyList<IReadOnlyList<ScenarioAction>>? Rewards { get; init; }

    /// <summary>branch: the conditions (all must hold) choosing between <see cref="Then"/> and <see cref="Else"/>.</summary>
    public IReadOnlyList<EventCondition>? If { get; init; }

    public IReadOnlyList<ScenarioAction>? Then { get; init; }

    public IReadOnlyList<ScenarioAction>? Else { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>This action and every nested one (branches, options, fallbacks).</summary>
    public IEnumerable<ScenarioAction> SelfAndDescendants()
    {
        yield return this;
        foreach (var branch in new[] { Pass, Masterful, Fail, Fallback, Actions, Then, Else })
        {
            foreach (var action in Flatten(branch))
            {
                yield return action;
            }
        }

        foreach (var action in (Rewards ?? []).SelectMany(Flatten))
        {
            yield return action;
        }

        foreach (var option in Options ?? [])
        {
            foreach (var action in Flatten(option.Actions).Concat(Flatten(option.Pass)))
            {
                yield return action;
            }
        }
    }

    public static IEnumerable<ScenarioAction> Flatten(IEnumerable<ScenarioAction>? actions) =>
        (actions ?? []).SelectMany(a => a.SelfAndDescendants());
}

/// <summary>An option of askChoice or askSkillCheck.</summary>
public sealed record ActionOption
{
    public LocalizedText? Label { get; init; }

    public string? Skill { get; init; }

    public int? Difficulty { get; init; }

    public IReadOnlyList<ScenarioAction>? Actions { get; init; }

    public IReadOnlyList<ScenarioAction>? Pass { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extra { get; init; }
}
