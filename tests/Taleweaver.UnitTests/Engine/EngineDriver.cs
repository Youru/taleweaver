using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Plays a game through the engine, as the GM would, and keeps the history for replay checks.</summary>
internal sealed class EngineDriver
{
    private readonly List<GameCommand> _history = [];

    public EngineDriver(Scenario scenario, Catalog catalog, params string[] party)
    {
        Context = new GameContext(scenario, catalog, new SeededRandomSource(7));
        Party = party.Length > 0 ? party : ["Nightingale", "Rook"];
        State = GameEngine.Start(Context, Party);
    }

    public GameContext Context { get; }

    public IReadOnlyList<string> Party { get; }

    public GameState State { get; private set; }

    public IReadOnlyList<GameCommand> History => _history;

    public EncounterState Encounter => State.Encounter!;

    public EngineDriver Do(GameCommand command)
    {
        var result = GameEngine.Apply(State, command, Context);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        State = result.Value;
        _history.Add(command);
        return this;
    }

    public EngineDriver Answer(string option) => Do(GameCommand.Answer(option));

    public EngineDriver Continue()
    {
        State.Prompt!.Kind.Should().Be(PromptKinds.Continue);
        return Answer("continue");
    }

    /// <summary>
    /// Plays the next turns until the last card of the track has had its turn (starting a new round if needed),
    /// answering every AI condition with <paramref name="aiAnswer"/>.
    /// </summary>
    public EngineDriver PlayRound(string aiAnswer = "no")
    {
        do
        {
            Do(GameCommand.NextTurn());
            while (State.Prompt?.Kind == PromptKinds.AiCondition)
            {
                Answer(aiAnswer);
            }
        }
        while (State.Encounter is { Ended: false } e && e.CurrentTurn + 1 < e.InitiativeTrack.Count);

        return this;
    }

    public IEnumerable<string> Codes => State.Log.Select(l => l.Code);

    public CombatantState Combatant(string instance) => Encounter.Combatants.Single(c => c.Instance == instance);
}
