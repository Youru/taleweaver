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
    public void Answer_ChoiceThatGoesToAnotherScene_PlaysThatSceneAndDropsTheFirstOnesNext()
    {
        static ScenarioAction GoTo(string node) => new() { Do = "goToNode", Node = node };
        var scenario = new Scenario
        {
            Id = "hb:scenario/branches",
            Title = En("Branches"),
            Start = "crossroads",
            Nodes =
            [
                new Node
                {
                    Id = "crossroads",
                    Type = "scene",
                    Mechanics =
                    [
                        new ScenarioAction
                        {
                            Do = "askChoice",
                            Question = En("Which way?"),
                            Options = [new ActionOption { Label = En("Left"), Actions = [GoTo("left")] }, new ActionOption { Label = En("Right"), Actions = [GoTo("right")] }],
                        },
                    ],
                },
                new Node { Id = "left", Type = "scene", Narrative = En("The left path."), Next = "@lost" },
                new Node { Id = "right", Type = "scene", Narrative = En("The right path."), Next = "@found" },
            ],
        };
        var game = new EngineDriver(scenario, Catalog);

        game.Answer("1").Continue();

        game.State.Status.Should().Be(GameStatus.Finished);
        game.State.Ending.Should().Be("@found");
        game.State.Log.Should().Contain(l => l.Code == "scene" && l.Text != null && l.Text["en"] == "The right path.");
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

    [Theory]
    [InlineData("yes", "Is the Brute damaged?")]
    [InlineData("no", null)]
    public void AnswerAiCondition_ConditionalContinueDown_AsksItsQuestionAfterTheInstructions(string answer, string? next)
    {
        var catalog = new Catalog(
        [
            new Pack
            {
                Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
                Combatants =
                [
                    Card("brute-1-2-common", "brute", "1-2", ["hb:initiative/brute"], health: 10, steps:
                    [
                        Step("Is there an opponent adjacent?", "Make a [Melee] ATK.") with { ContinueDownIf = En("Is no ally within SOI?") },
                        Step("Is the Brute damaged?", "Roar."),
                    ]),
                ],
            },
        ]);
        var game = Game(catalog: catalog).Continue();
        StartBruteTurn(game);
        game.Answer("yes");
        game.State.Prompt!.Question!["en"].Should().Be("Is no ally within SOI?");
        game.State.Log.Should().Contain(l => l.Code == "aiInstruction" && l.Text!["en"] == "Make a [Melee] ATK.");

        game.Answer(answer);

        game.State.Prompt?.Question?["en"].Should().Be(next);
        if (next is null)
        {
            game.Encounter.Activation.Should().BeNull();
        }
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
    public void SkillCheck_WithMasterfulBranch_OffersItAndShowsItsText()
    {
        var check = new ScenarioAction
        {
            Do = "askSkillCheck",
            Options = [new ActionOption { Skill = "strength", Difficulty = 12 }],
            Pass = [new ScenarioAction { Do = "showText", Text = En("PASS: held.") }],
            Masterful = [new ScenarioAction { Do = "showText", Text = En("MASTERFUL PASS: thrown back.") }],
            Fail = [new ScenarioAction { Do = "showText", Text = En("FAIL: crushed.") }],
        };
        var game = Game(spawn: check).Continue();

        game.Do(GameCommand.ActivateToken("totem:red"));
        game.State.Prompt!.Options.Select(o => o.Id).Should().Equal("pass-0", "masterful", "fail");
        game.Answer("masterful");

        game.State.Log.Should().Contain(l => l.Code == "text" && l.Text!["en"] == "MASTERFUL PASS: thrown back.");
        game.State.Log.Should().NotContain(l => l.Code == "text" && l.Text!["en"] == "PASS: held.");
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
    public void Spawn_WithEffects_FigureStartsWithThem()
    {
        var game = Game(spawn: new ScenarioAction { Do = "spawn", Family = "imp", Count = 2, Effects = ["burrowed"] }).Continue();

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Encounter.Combatants.Where(c => c.Family == "imp").Should().AllSatisfy(c => c.Effects.Should().Equal("burrowed"));
    }

    [Fact]
    public void Spawn_OwnLootLevelBelowEveryCard_SpawnsNothing()
    {
        var game = Game(spawn: new ScenarioAction { Do = "spawn", Family = "imp", LootLevel = "mundane" }).Continue();

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Encounter.Combatants.Should().NotContain(c => c.Family == "imp");
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

    [Fact]
    public void EnterEncounter_StartingZone_PlacesTheAdventurersRowByRow()
    {
        var game = Game().Continue();

        game.Encounter.Adventurers.Select(a => (a.Name, a.Position)).Should().Equal(("Nightingale", new Cell(0, 4)), ("Rook", new Cell(1, 4)));
        game.Combatant("brute-1").Position.Should().Be(new Cell(1, 1));
    }

    [Fact]
    public void SetEffect_OnAnAdventurer_KeepsItOnceAndRemovesIt()
    {
        var game = Game().Continue();

        game.Do(GameCommand.SetEffect("adventurer:Rook", "darkness", true)).Do(GameCommand.SetEffect("adventurer:Rook", "darkness", true));
        game.Encounter.Adventurers.Single(a => a.Name == "Rook").Effects.Should().Equal("darkness");

        game.Do(GameCommand.SetEffect("adventurer:Rook", "darkness", false));
        game.Encounter.Adventurers.Single(a => a.Name == "Rook").Effects.Should().BeEmpty();
    }

    [Fact]
    public void SetEffect_OnAnUnknownAdventurer_IsRefused()
    {
        var game = Game().Continue();

        var act = () => game.Do(GameCommand.SetEffect("adventurer:Nobody", "poison", true));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Move_DuringAnAiQuestion_MovesTheFigureAndKeepsTheQuestion()
    {
        var game = Game().Continue();
        StartBruteTurn(game);
        var question = game.State.Prompt!.Question;

        game.Do(GameCommand.Move("brute-1", new Cell(3, 2))).Do(GameCommand.Move("adventurer:Rook", new Cell(2, 2)));

        game.Combatant("brute-1").Position.Should().Be(new Cell(3, 2));
        game.Encounter.Adventurers.Single(a => a.Name == "Rook").Position.Should().Be(new Cell(2, 2));
        game.State.Prompt!.Question.Should().BeEquivalentTo(question);
        game.State.Prompt.Kind.Should().Be(PromptKinds.AiCondition);
    }

    [Fact]
    public void Move_UnknownFigure_IsRefused()
    {
        var game = Game().Continue();

        var result = GameEngine.Apply(game.State, GameCommand.Move("adventurer:Zeke", new Cell(0, 0)), game.Context);

        result.Error.Should().Be(GameEngine.Errors.UnknownFigure("adventurer:Zeke"));
    }

    [Fact]
    public void RevealArea_HiddenArea_LiftsTheFogAndFiresItsEvent()
    {
        var game = Game().Continue();
        game.Encounter.RevealedAreas.Should().Equal("hall");

        game.Do(GameCommand.RevealArea("crypt"));

        game.Encounter.RevealedAreas.Should().Equal("hall", "crypt");
        game.State.Log.Should().Contain(l => l.Code == "revealArea" && l.Args!["area"] == "crypt");
        game.State.Log.Should().Contain(l => l.Code == "text" && l.Text!["en"] == "Bones everywhere.");
    }

    [Fact]
    public void RevealArea_AlreadyRevealedOrUnknown_IsRefused()
    {
        var game = Game().Continue();

        GameEngine.Apply(game.State, GameCommand.RevealArea("hall"), game.Context).Error.Should().Be(GameEngine.Errors.UnknownArea("hall"));
        GameEngine.Apply(game.State, GameCommand.RevealArea("cellar"), game.Context).Error.Should().Be(GameEngine.Errors.UnknownArea("cellar"));
    }

    private static EngineDriver Game(ScenarioAction? spawn = null, Catalog? catalog = null)
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
                    Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute", At = new Cell(1, 1) }], AdventurersStart = "start" },
                    Map = new BoardMap
                    {
                        Tiles = [new MapTile("hb:tile/meadow", 0, 0, 0, "hall"), new MapTile("hb:tile/meadow", 6, 0, 0, "crypt")],
                        Areas = [new FogArea("hall"), new FogArea("crypt", RevealedAtStart: false, Name: En("The Crypt"))],
                        Zones = [new Zone("start", "startingZone", new Rect(0, 4, 2, 2))],
                        Tokens = [new MapToken { Id = "t", Type = "hb:token/totem", Color = "red" }],
                    },
                    Tokens = new Dictionary<string, TokenRule> { ["totem:red"] = new() { Events = ["totem"] } },
                    Events =
                    [
                        new ScenarioEvent
                        {
                            Id = "totem",
                            Trigger = new EventTrigger { On = "tokenActivated", Token = ["totem:red"] },
                            Actions = spawn is null ? [] : [spawn],
                        },
                        new ScenarioEvent
                        {
                            Id = "crypt-opened",
                            Trigger = new EventTrigger { On = "areaRevealed", Area = "crypt" },
                            Actions = [new ScenarioAction { Do = "showText", Text = En("Bones everywhere.") }],
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
        return new EngineDriver(scenario, catalog ?? Catalog);
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
