using System.Text.Json;
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

    public EncounterState? Encounter { get; set; }

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

    /// <summary>Items gained, as described by the scenario (the app does not track the cards).</summary>
    public List<string> Items { get; set; } = [];

    public List<string> RevealedHiddenCards { get; set; } = [];

    public Dictionary<string, JsonElement> Variables { get; set; } = [];
}

public sealed class AdventurerState
{
    public required string Name { get; set; }

    public int Xp { get; set; }
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

    public List<CombatantState> Combatants { get; set; } = [];

    public List<BoardToken> Tokens { get; set; } = [];

    public List<string> PlacedDiagrams { get; set; } = [];

    /// <summary>A NAVIGATE cleared the encounter's own map: only the placed diagrams are on the table.</summary>
    public bool MapCleared { get; set; }

    public List<string> ReadHiddenRules { get; set; } = [];

    public List<string> ActiveEndConditions { get; set; } = [];

    public List<string> FiredEvents { get; set; } = [];

    public List<string> ActivatedTokens { get; set; } = [];

    /// <summary>Fog areas revealed so far (those revealed at the start included).</summary>
    public List<string> RevealedAreas { get; set; } = [];

    /// <summary>The adventurers' figures, placed in the starting zone, then moved by the GM.</summary>
    public List<AdventurerFigure> Adventurers { get; set; } = [];

    /// <summary>AI steps added by the scenario, by family (appendAiStep).</summary>
    public Dictionary<string, List<AiStep>> ExtraAiSteps { get; set; } = [];

    /// <summary>What happened this round, for the Urgency rule.</summary>
    public RoundLog RoundLog { get; set; } = new();

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

    public string? Figure { get; set; }

    /// <summary>Square of the figure: where the scenario placed it, then where the GM moved it (optional).</summary>
    public Cell? Position { get; set; }

    public int Damage { get; set; }

    public List<string> Effects { get; set; } = [];

    public bool Aggro { get; set; }

    /// <summary>Attacked since its last turn (Golden Rule #4: Aggro).</summary>
    public bool Attacked { get; set; }

    public bool Defeated { get; set; }
}

public sealed class AdventurerFigure
{
    public required string Name { get; set; }

    public Cell? Position { get; set; }

    /// <summary>Effects on the adventurer this encounter (the AI aims at them: "an opponent with Darkness").</summary>
    public List<string> Effects { get; set; } = [];
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
}

public sealed class RoundLog
{
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

    public bool AnyStepTrue { get; set; }

    /// <summary>The question asked is the step's ContinueDownIf, not its condition.</summary>
    public bool AskingContinue { get; set; }
}

/// <summary>A question to the GM. The answer is one of <see cref="Options"/>, or a number when <see cref="Kind"/> is "number".</summary>
public sealed class Prompt
{
    /// <summary>continue, choice, skillCheck, aiCondition, number, adventurer, spawnPossible.</summary>
    public required string Kind { get; set; }

    public LocalizedText? Question { get; set; }

    public List<PromptOption> Options { get; set; } = [];

    /// <summary>Scenario action waiting for the answer (askChoice, askSkillCheck, restoreCombatant, spawn…).</summary>
    public ScenarioAction? Action { get; set; }

    /// <summary>Combatant concerned (AI step, restored combatant).</summary>
    public string? Instance { get; set; }
}

public sealed record PromptOption(string Id, LocalizedText? Label = null);

/// <summary>
/// A line shown to the GM. <see cref="Code"/> says what it is (narrative, hiddenText, aiInstruction, spawn…);
/// <see cref="Text"/> is the scenario's or the card's own text; <see cref="Args"/> holds the values the
/// front needs to phrase the rest in the reader's language.
/// </summary>
public sealed record LogEntry(string Code, LocalizedText? Text = null, IReadOnlyDictionary<string, string>? Args = null);
