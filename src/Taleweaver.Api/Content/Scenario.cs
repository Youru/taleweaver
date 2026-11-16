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

    public IReadOnlyList<string> Packs { get; init; } = [];

    public ScenarioSource? Source { get; init; }

    public Party? Party { get; init; }

    public IReadOnlyList<JsonElement> Variables { get; init; } = [];

    public required string Start { get; init; }

    /// <summary>The pack of the scenarios made in the workshop: "homebrew:scenario/&lt;name&gt;".</summary>
    public const string HomebrewPack = "homebrew";

    /// <summary>What only the workshop reads (where each node sits on the graph); the engine ignores it.</summary>
    public JsonElement? Editor { get; init; }

    public IReadOnlyList<Node> Nodes { get; init; } = [];
}

public sealed record ScenarioSource(string? File, string? Reference, string? Version);

public sealed record Party
{
    public int? Size { get; init; }

    public int? StartingGold { get; init; }

    public int? StartingXp { get; init; }

    public IReadOnlyList<string>? AllowedAdventurers { get; init; }
}

/// <summary>A step of the scenario (§3.2). Encounter fields (§3.3) are only set when <see cref="Type"/> is "encounter".</summary>
public sealed record Node
{
    public const string EncounterType = "encounter";

    public required string Id { get; init; }

    /// <summary>scene, encounter, choice…</summary>
    public required string Type { get; init; }

    public LocalizedText? Name { get; init; }

    public LocalizedText? Title { get; init; }

    public int? Page { get; init; }

    public LocalizedText? Narrative { get; init; }

    public IReadOnlyList<ScenarioAction> Mechanics { get; init; } = [];

    /// <summary>Next node id, or a reserved target starting with "@" (e.g. "@end").</summary>
    public string? Next { get; init; }

    public LocalizedText? Intro { get; init; }

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

    public IReadOnlyList<Diagram> Diagrams { get; init; } = [];

    [JsonIgnore]
    public bool IsEncounter => Type == EncounterType;
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
}

public sealed record HiddenCardReveal(string Code, string? Size);

public sealed record SpecialRule(string Id, LocalizedText Name, LocalizedText Text);

public sealed record TokenRule
{
    /// <summary>How the token is activated (e.g. "encounterAction").</summary>
    public string? Mode { get; init; }

    public LocalizedText? Activation { get; init; }

    public IReadOnlyList<string> Events { get; init; } = [];
}

public sealed record EndCondition
{
    public required string Id { get; init; }

    /// <summary>win or loss.</summary>
    public required string Kind { get; init; }

    /// <summary>False when the condition is only added later (addEndCondition).</summary>
    public bool? Active { get; init; }

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

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>This action and every nested one (branches, options, fallbacks).</summary>
    public IEnumerable<ScenarioAction> SelfAndDescendants()
    {
        yield return this;
        foreach (var branch in new[] { Pass, Masterful, Fail, Fallback, Actions })
        {
            foreach (var action in Flatten(branch))
            {
                yield return action;
            }
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
