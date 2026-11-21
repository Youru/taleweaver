using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Applies one command to a private copy of the state. Split in several files: flow and commands here,
/// scenario actions, encounter (rounds, initiative, AI) and events in the other parts.
/// </summary>
internal sealed partial class Session(GameState state, GameContext context)
{
    public GameState State { get; } = state;

    /// <summary>The scenario being read: the Bounty's while one is played from a Story Round, else the game's.</summary>
    private Scenario Scenario =>
        State.Bounty is { } bounty && context.Bounties.TryGetValue(bounty.Scenario, out var played) ? played : context.Scenario;

    /// <summary>The game's own scenario (its Story Rounds), even while a Bounty is played.</summary>
    private Scenario Campaign => context.Scenario;

    private Catalog Catalog => context.Catalog;

    private EncounterState Encounter => State.Encounter ?? throw new InvalidOperationException("No encounter in progress.");

    private Node EncounterNode => Scenario.Nodes.First(n => n.Id == Encounter.Node);

    public Result Execute(GameCommand command)
    {
        if (command.Type == GameCommands.Answer)
        {
            return State.Prompt is null ? GameEngine.Errors.NoQuestion : Answer(command);
        }

        // The party's items change nothing in the flow: allowed in and out of encounters, whatever the question.
        if (GameCommands.IsItemCommand(command.Type))
        {
            return ExecuteItemCommand(command);
        }

        // The party changes between encounters, whatever the question (the Story Round's, most often).
        if (GameCommands.IsPartyCommand(command.Type))
        {
            return State.Encounter is { Ended: false } ? GameEngine.Errors.InEncounter : ExecutePartyCommand(command);
        }

        if (State.Encounter is not { Ended: false })
        {
            return GameEngine.Errors.NotInEncounter;
        }

        // Moving a figure changes nothing in the rules: allowed at any time, without disturbing the question.
        if (command.Type == GameCommands.Move)
        {
            return Move(command.Instance, command.To);
        }

        // Nor does moving a card on the track: the figure playing goes on, the order of the next turns changes.
        if (command.Type == GameCommands.MoveInitiative)
        {
            return MoveInitiative(command.Card, command.Value ?? 0);
        }

        // While a figure goes through its AI steps, the GM may still report what happens on the board.
        // The AI question is set aside: its consequences run first, then the same step is asked again.
        var reportsAllowed = State.Prompt is null || State.Prompt.Kind == PromptKinds.AiCondition;
        if (reportsAllowed && command.Type != GameCommands.NextTurn)
        {
            State.Prompt = null;
        }

        return command.Type switch
        {
            GameCommands.NextTurn => State.Prompt is null ? NextTurn() : GameEngine.Errors.AwaitingAnswer,
            GameCommands.Damage when reportsAllowed => ApplyDamage(command.Instance, command.Value ?? 0),
            GameCommands.SetEffect when reportsAllowed => SetEffect(command.Instance, command.Effect, command.Active ?? true),
            GameCommands.Defeat when reportsAllowed => Defeat(command.Instance),
            GameCommands.ActivateToken when reportsAllowed => ActivateTokenCommand(command.Token),
            GameCommands.ReachEndCondition when reportsAllowed => ReachEndCondition(command.EndCondition),
            GameCommands.RevealArea when reportsAllowed => RevealAreaCommand(command.Area),
            GameCommands.Damage or GameCommands.SetEffect or GameCommands.Defeat or GameCommands.ActivateToken or GameCommands.ReachEndCondition or GameCommands.RevealArea
                => GameEngine.Errors.AwaitingAnswer,
            _ => GameEngine.Errors.UnknownCommand(command.Type),
        };
    }

    /// <summary>Runs the pending scenario actions, then the AI of the acting group, until a question or nothing is left.</summary>
    public void Run()
    {
        while (State.Prompt is null && State.Status == GameStatus.InProgress)
        {
            if (State.PendingActions.Count > 0)
            {
                var action = State.PendingActions[0];
                State.PendingActions.RemoveAt(0);
                Do(action);
            }
            else if (State.Encounter?.Activation is not null)
            {
                ContinueActivation();
            }
            else
            {
                break;
            }
        }
    }

    public void GoToNode(string? target)
    {
        // Leaving a node drops what was left of it (e.g. a scene's "next" after a choice that branched).
        State.PendingActions.Clear();
        State.Encounter = null;
        // The end of a Bounty, whatever it says: back to the Story Round it was chosen from.
        if (State.Bounty is { } bounty && (target is null || target.StartsWith('@')))
        {
            EndBounty(bounty, target ?? "@end");
            return;
        }

        // A Side Quest comes back to its Story Round, which starts again at Shop and Train (p. 13).
        if (target == CurrentStoryRound && State.Campaign.StoryRound is { } round
            && Campaign.Nodes.FirstOrDefault(n => n.Id == round.Node) is { IsStoryRound: true } storyRound)
        {
            State.Campaign.CurrentNode = storyRound.Id;
            EnterStoryRound(storyRound, returning: true);
            return;
        }

        if (target is null || target.StartsWith('@'))
        {
            State.Status = GameStatus.Finished;
            State.Ending = target ?? "@end";
            if (!State.Campaign.AttemptedScenarios.Contains(Scenario.Id))
            {
                State.Campaign.AttemptedScenarios.Add(Scenario.Id);
            }

            Log("scenarioEnd", args: Args(("ending", State.Ending)));
            return;
        }

        var node = Scenario.Nodes.FirstOrDefault(n => n.Id == target);
        if (node is null)
        {
            State.Status = GameStatus.Finished;
            State.Ending = target;
            Log("unknownNode", args: Args(("node", target)));
            return;
        }

        State.Campaign.CurrentNode = node.Id;
        State.Campaign.VisitedNodes.Add(node.Id);
        if (node.IsEncounter)
        {
            EnterEncounter(node);
            return;
        }

        if (node.IsStoryRound)
        {
            EnterStoryRound(node, returning: State.Campaign.StoryRound?.Node == node.Id);
            return;
        }

        Log("scene", node.Narrative, Args(("node", node.Id), ("title", node.Title?.GetValueOrDefault("en"))));
        Push([.. node.Mechanics, Continue(), GoTo(node.Next)]);
    }

    private Result Answer(GameCommand command)
    {
        var prompt = State.Prompt!;
        var option = command.Option;
        if (prompt.Kind != PromptKinds.Number && prompt.Options.All(o => o.Id != option))
        {
            return GameEngine.Errors.InvalidAnswer(option);
        }

        State.Prompt = null;
        Log("answer", args: Args(("question", prompt.Kind), ("answer", option ?? command.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        switch (prompt.Kind)
        {
            case PromptKinds.Continue:
                break;
            case PromptKinds.Choice:
                Push(prompt.Action!.Options![int.Parse(option!, System.Globalization.CultureInfo.InvariantCulture)].Actions ?? []);
                break;
            case PromptKinds.SkillCheck:
                AnswerSkillCheck(prompt.Action!, option!);
                break;
            case PromptKinds.AiCondition:
                AnswerAiCondition(option == "yes");
                break;
            case PromptKinds.SpawnPossible:
                if (option == "yes")
                {
                    Spawn(prompt.Action!, fromSetup: false);
                }
                else
                {
                    Push(prompt.Action!.Fallback ?? []);
                }

                break;
            case PromptKinds.Number when command.Value is { } value && prompt.Action?.Do == "askNumber":
                SetVariable(ExtraString(prompt.Action, "var"), JsonSerializer.SerializeToElement(value));
                break;
            case PromptKinds.Number when command.Value is { } value:
                RestoreCombatant(prompt.Action!, Math.Max(0, value));
                break;
            case PromptKinds.Number:
                State.Prompt = prompt;
                return GameEngine.Errors.InvalidAnswer(null);
            case PromptKinds.StoryRound:
                AnswerStoryRound(option!);
                break;
            case PromptKinds.Adventurer:
                Log("adventurerDefeated", args: Args(("adventurer", option)));
                Fire(e => e.Trigger.On == "adventurerDefeated");
                break;
        }

        return Result.Success();
    }

    private Result ExecutePartyCommand(GameCommand command) => command.Type switch
    {
        GameCommands.SetActiveGroup => SetActiveGroup(command.Adventurers),
        GameCommands.SetAdventurerStatus => SetAdventurerStatus(command.Instance, command.Effect, command.Active ?? true),
        GameCommands.LearnDiscipline => LearnDiscipline(command.Instance, command.Item, command.Value ?? 0),
        GameCommands.ForgetDiscipline => ForgetDiscipline(command.Instance, command.Item),
        _ => GameEngine.Errors.UnknownCommand(command.Type),
    };

    private void Push(IEnumerable<ScenarioAction> actions) => State.PendingActions.InsertRange(0, actions);

    private void Ask(string kind, LocalizedText? question, IEnumerable<PromptOption> options, ScenarioAction? action = null, string? instance = null) =>
        State.Prompt = new Prompt { Kind = kind, Question = question, Options = options.ToList(), Action = action, Instance = instance };

    private void Log(string code, LocalizedText? text = null, IReadOnlyDictionary<string, string>? args = null) =>
        State.Log.Add(new LogEntry(code, text, args));

    private static Dictionary<string, string> Args(params (string Key, string? Value)[] values) =>
        values.Where(v => v.Value is not null).ToDictionary(v => v.Key, v => v.Value!);

    private static ScenarioAction Continue() => new() { Do = InternalActions.AwaitContinue };

    private static ScenarioAction GoTo(string? node) => new() { Do = "goToNode", Node = node ?? "@end" };

    private static string? Json(JsonElement? element) => element is { } e ? e.GetRawText() : null;

    private int NextRandom(int maxExclusive) => context.Random.Next(State.RandomDraws++, maxExclusive);

    private static LocalizedText Text(string en, string fr) => new Dictionary<string, string> { ["en"] = en, ["fr"] = fr };
}

public static class PromptKinds
{
    public const string Continue = "continue";
    public const string Choice = "choice";
    public const string SkillCheck = "skillCheck";
    public const string AiCondition = "aiCondition";
    public const string SpawnPossible = "spawnPossible";
    public const string Number = "number";
    public const string Adventurer = "adventurer";
    public const string StoryRound = "storyRound";
}

internal static class InternalActions
{
    /// <summary>Waits for the GM to have read what precedes (scene text, encounter rewards).</summary>
    public const string AwaitContinue = "awaitContinue";

    /// <summary>Asks the phase of the current Story Round (after its page's mechanics).</summary>
    public const string AskStoryRound = "askStoryRound";
}
