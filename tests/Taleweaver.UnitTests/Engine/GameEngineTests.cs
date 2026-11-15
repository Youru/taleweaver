using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Rules of the companion on a small homebrew pack: AI steps, groups, spawning, Urgency, questions.</summary>
public class GameEngineTests
{
    private static readonly Catalog Catalog = new(
    [
        new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
            Combatants =
            [
                Card("brute-1-2-common", "brute", "1-2", ["hb:initiative/brute"], health: 10, steps:
                [
                    Step("Is there an opponent adjacent?", "Make a [Melee] ATK.", continueDown: true),
                    Step("Is the Brute damaged?", "Roar."),
                ]),
                Card("imp-1-3-common", "imp", "1-3", ["hb:initiative/imp-1-3"], health: 4, steps: [Step("Can it see an opponent?", "Throw a rock.")]),
                Card("imp-4-6-common", "imp", "4-6", ["hb:initiative/imp-4-6"], health: 4, steps: [Step("Can it see an opponent?", "Throw a rock.")]),
            ],
        },
    ]);

    [Fact]
    public void Start_SceneThenEncounter_WaitsForTheGmToReadTheScene()
    {
        var game = Game();

        game.State.Prompt!.Kind.Should().Be(PromptKinds.Continue);
        game.Continue();
        game.Encounter.Round.Should().Be(0);
        game.Encounter.InitiativeTrack.Should().BeEquivalentTo(["adventurer:Nightingale", "adventurer:Rook", "hb:initiative/brute"]);
    }

    [Fact]
    public void AnswerAiCondition_TrueStepWithContinueDown_AsksTheNextStepToo()
    {
        var game = Game().Continue();
        StartBruteTurn(game);

        game.Answer("yes");

        game.State.Prompt!.Question!["en"].Should().Be("Is the Brute damaged?");
        game.State.Log.Should().Contain(l => l.Code == "aiInstruction" && l.Text!["en"] == "Make a [Melee] ATK.");
    }

    [Fact]
    public void AnswerAiCondition_NoTrueStepAfterBeingAttacked_GivesAggroAndItsMoveStep()
    {
        var game = Game().Continue();
        game.Do(GameCommand.Damage("brute-1", 2));
        StartBruteTurn(game);

        game.Answer("no").Answer("no");

        game.Combatant("brute-1").Aggro.Should().BeTrue();
        game.Encounter.Activation.Should().BeNull();
    }

    [Fact]
    public void Damage_ReachingHealth_DefeatsAndRemovesTheInitiativeCard()
    {
        var game = Game().Continue();

        game.Do(GameCommand.Damage("brute-1", 10));

        game.Combatant("brute-1").Defeated.Should().BeTrue();
        game.Encounter.InitiativeTrack.Should().NotContain("hb:initiative/brute");
    }

    [Fact]
    public void Spawn_FourImps_FillsGroupOneThreeThenFourSix()
    {
        var game = Game(spawn: new ScenarioAction { Do = "spawn", Family = "imp", Count = 4 }).Continue();

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Encounter.Combatants.Where(c => c.Family == "imp").Select(c => (c.Number, c.Card)).Should().Equal(
            (1, "hb:combatant/imp-1-3-common"), (2, "hb:combatant/imp-1-3-common"), (3, "hb:combatant/imp-1-3-common"), (4, "hb:combatant/imp-4-6-common"));
        game.Encounter.InitiativeTrack.Should().Contain(["hb:initiative/imp-1-3", "hb:initiative/imp-4-6"]);
    }

    [Fact]
    public void Spawn_DuringARound_InsertsTheCardRightAfterTheCurrentTurn()
    {
        var game = Game(spawn: new ScenarioAction { Do = "spawn", Family = "imp", Count = 1 }).Continue();
        game.Do(GameCommand.NextTurn());
        var current = game.Encounter.CurrentTurn;

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Encounter.InitiativeTrack[current + 1].Should().Be("hb:initiative/imp-1-3");
    }

    [Fact]
    public void NextTurn_RoundWithoutTrueStepDefeatOrSpawn_AddsUrgencyAndTheFourthLoses()
    {
        var game = Game().Continue();

        for (var round = 0; round < 4 && game.State.Status == GameStatus.InProgress; round++)
        {
            game.PlayRound();
        }

        game.Do(GameCommand.NextTurn());
        game.State.Log.Should().Contain(l => l.Code == "urgencyDefeat");
        game.State.Log.Should().Contain(l => l.Code == "encounterEnd" && l.Args!["endCondition"] == "lose-all");
    }

    [Fact]
    public void NextTurn_WhileAQuestionIsPending_IsRefused()
    {
        var game = Game();

        var result = GameEngine.Apply(game.State, GameCommand.NextTurn(), game.Context);

        result.Error.Should().Be(GameEngine.Errors.NotInEncounter);
    }

    [Fact]
    public void Answer_OptionNotOffered_IsRefused()
    {
        var game = Game();

        var result = GameEngine.Apply(game.State, GameCommand.Answer("maybe"), game.Context);

        result.Error!.Code.Should().Be("Games.InvalidAnswer");
    }

    [Fact]
    public void ReachEndCondition_Inactive_IsRefused()
    {
        var game = Game().Continue();

        var result = GameEngine.Apply(game.State, GameCommand.ReachEndCondition("win-later"), game.Context);

        result.Error!.Code.Should().Be("Encounters.InactiveEndCondition");
    }

    [Fact]
    public void Apply_Command_LeavesTheGivenStateUnchanged()
    {
        var game = Game().Continue();
        var before = System.Text.Json.JsonSerializer.Serialize(game.State);

        GameEngine.Apply(game.State, GameCommand.NextTurn(), game.Context);

        System.Text.Json.JsonSerializer.Serialize(game.State).Should().Be(before);
    }

    [Theory]
    [InlineData("1-3", new[] { 1, 2, 3 })]
    [InlineData("7-8", new[] { 7, 8 })]
    [InlineData("1 + Root ×4", new[] { 1 })]
    [InlineData("Quinn", new int[0])]
    public void ActivationNumbers_PrintedActivation_ListsTheNumbers(string activation, int[] expected)
    {
        var numbers = GameEngineInternals.ActivationNumbers(activation);

        numbers.Should().Equal(expected);
    }

    private static void StartBruteTurn(EngineDriver game)
    {
        game.Do(GameCommand.NextTurn());
        while (game.State.Prompt is null)
        {
            game.Do(GameCommand.NextTurn());
        }

        game.State.Prompt.Kind.Should().Be(PromptKinds.AiCondition);
        game.State.Prompt.Instance.Should().Be("brute-1");
    }

    private static EngineDriver Game(ScenarioAction? spawn = null)
    {
        var scenario = new Scenario
        {
            Id = "hb:scenario/test",
            Title = En("Test"),
            Start = "intro",
            Nodes =
            [
                new Node { Id = "intro", Type = "scene", Narrative = En("Once upon a time."), Next = "fight" },
                new Node
                {
                    Id = "fight",
                    Type = Node.EncounterType,
                    LootLevel = "common",
                    Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute", At = new Cell(1, 1) }] },
                    Map = new BoardMap { Tokens = [new MapToken { Id = "t", Type = "hb:token/totem", Color = "red" }] },
                    Tokens = new Dictionary<string, TokenRule> { ["totem:red"] = new() { Events = ["totem"] } },
                    Events =
                    [
                        new ScenarioEvent
                        {
                            Id = "totem",
                            Trigger = new EventTrigger { On = "tokenActivated", Token = ["totem:red"] },
                            Actions = spawn is null ? [] : [spawn],
                        },
                    ],
                    EndConditions =
                    [
                        new EndCondition { Id = "lose-all", Kind = "lose", Next = "@end" },
                        new EndCondition { Id = "win-later", Kind = "win", Active = false, Next = "@end" },
                    ],
                },
            ],
        };
        return new EngineDriver(scenario, Catalog);
    }

    private static Combatant Card(string name, string family, string activation, string[] initiative, int health, AiStep[] steps) => new()
    {
        Id = $"hb:combatant/{name}",
        Family = family,
        Kind = "intelligent",
        LootLevel = "common",
        Activation = activation,
        Name = En(family),
        Stats = new CombatantStats { Health = new StatValue(health) },
        InitiativeCards = initiative,
        AiSteps = steps,
    };

    private static AiStep Step(string condition, string instruction, bool continueDown = false) => new()
    {
        Condition = new AiCondition(En(condition), "ask"),
        Instructions = [new AiInstruction { Text = En(instruction) }],
        ContinueDown = continueDown,
    };

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
