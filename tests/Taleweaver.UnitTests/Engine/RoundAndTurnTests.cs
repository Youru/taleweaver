using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Rounds and turns corrected by the play review (docs/design/play-spec.md, lot 1b).</summary>
public class RoundAndTurnTests
{
    private const string Paralyze = "paralyze";

    [Fact]
    public void RoundEndActions_RunBeforeTheNextRound()
    {
        var game = Game(Fight(events: On("roundEnd", Say("END")))).PlayRound().Turn();

        var log = game.State.Log;
        log.FindIndex(l => l.Code == "text").Should().BeLessThan(log.FindLastIndex(l => l.Code == "roundStart"));
    }

    [Fact]
    public void RoundEndQuestion_HoldsTheRoundUntilItIsAnswered()
    {
        var game = Game(Fight(events: On("roundEnd", Question()))).PlayRound();

        game.Do(GameCommand.NextTurn());
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Choice);
        game.Encounter.Round.Should().Be(1);
        game.Encounter.Urgency.Should().Be(0);

        game.Answer("0");
        game.Encounter.Round.Should().Be(2);
        game.Encounter.Urgency.Should().Be(1);
        Replayed(game).Should().Be(Json(game.State));
    }

    [Fact]
    public void RoundEndSpawn_PreventsUrgency()
    {
        var spawn = new ScenarioAction { Do = "spawn", Family = "imp", Count = 1 };

        var game = Game(Fight(imps: 0, events: On("roundEnd", spawn))).PlayRound().Turn();

        game.Encounter.Urgency.Should().Be(0);
        game.Encounter.Round.Should().Be(2);
    }

    [Fact]
    public void RoundEndThatEndsTheEncounter_StartsNoRoundAndAddsNoUrgency()
    {
        var end = new ScenarioAction { Do = "endEncounter", EndCondition = "win" };

        var game = Game(Fight(events: On("roundEnd", end))).PlayRound().Turn();

        game.Encounter.Ended.Should().BeTrue();
        game.Encounter.Round.Should().Be(1);
        game.Encounter.Urgency.Should().Be(0);
    }

    [Fact]
    public void RoundStartActions_RunBeforeTheFirstTurn()
    {
        var game = Game(Fight(events: On("roundStart", Say("START")))).Turn();

        var log = game.State.Log;
        log.FindIndex(l => l.Code == "text").Should().BeLessThan(log.FindIndex(l => l.Code == "turn"));
    }

    [Fact]
    public void RoundStartQuestion_HoldsTheFirstTurn()
    {
        var game = Game(Fight(events: On("roundStart", Question())));

        game.Do(GameCommand.NextTurn());
        game.Codes.Should().NotContain("turn");

        game.Answer("0");
        game.Codes.Should().Contain("turn");
        Replayed(game).Should().Be(Json(game.State));
    }

    [Theory]
    [InlineData("roundStart")]
    [InlineData("roundEnd")]
    public void NavigateBetweenTurns_LeavesOneFirstTurn(string trigger)
    {
        var node = Fight(events: On(trigger, new ScenarioAction { Do = "navigate", Diagram = "cave" }))
            with { Diagrams = [new Diagram { Id = "cave" }] };
        var game = Game(node);

        if (trigger == "roundEnd")
        {
            game.PlayRound();
        }

        game.Turn();
        game.Encounter.Round.Should().Be(trigger == "roundEnd" ? 2 : 1);

        var afterNavigate = game.State.Log.Skip(game.State.Log.FindLastIndex(l => l.Code == "navigate"));
        afterNavigate.Count(l => l.Code == "turn").Should().Be(1);
    }

    [Fact]
    public void Target_WithoutDamage_CountsForAggro()
    {
        var game = Game(Fight());
        game.Do(new GameCommand { Type = "target", Instance = "imp-1" });

        game.PlayRound();

        game.Combatant("imp-1").Aggro.Should().BeTrue();
    }

    [Fact]
    public void ParalyzedCombatant_SkipsItsAi_ThenIsImmune()
    {
        var game = Game(Fight(imps: 2));
        game.Do(GameCommand.SetEffect("imp-1", Paralyze, true));

        PlayUntilAsked(game);

        game.State.Prompt!.Instance.Should().Be("imp-2");
        game.Combatant("imp-1").Effects.Should().NotContain(Paralyze);
        game.Do(GameCommand.SetEffect("imp-1", Paralyze, true));
        game.Combatant("imp-1").Effects.Should().NotContain(Paralyze);
        game.Codes.Should().Contain("effectImmune");
        Replayed(game).Should().Be(Json(game.State));
    }

    [Fact]
    public void ParalyzedTurn_CountsForUrgency_AndGivesNoAggro()
    {
        var game = Game(Fight());
        game.Do(GameCommand.Damage("imp-1", 1)).Do(GameCommand.SetEffect("imp-1", Paralyze, true));

        game.PlayRound().Do(GameCommand.NextTurn());

        game.Encounter.Urgency.Should().Be(0);
        game.Combatant("imp-1").Aggro.Should().BeFalse();
    }

    [Fact]
    public void ParalyzedWhileActing_StopsNow_AndLosesTheNextTurnToo()
    {
        var game = Game(Fight());
        PlayUntilAsked(game);

        game.Do(GameCommand.SetEffect("imp-1", Paralyze, true));
        game.State.Prompt.Should().BeNull();
        game.Combatant("imp-1").Effects.Should().Contain(Paralyze);

        for (var i = 0; i < 6 && game.Combatant("imp-1").Effects.Count > 0; i++)
        {
            game.Do(GameCommand.NextTurn());
            game.State.Prompt.Should().BeNull("a paralyzed figure has no AI step");
        }

        game.Combatant("imp-1").Effects.Should().BeEmpty();
    }

    [Fact]
    public void ParalyzedFigure_AfterOneDefeatedInItsTurn_IsSkippedToo()
    {
        var game = Game(Fight(imps: 3));
        game.Do(GameCommand.SetEffect("imp-2", Paralyze, true));
        PlayUntilAsked(game);

        game.Do(GameCommand.Defeat("imp-1"));

        game.State.Prompt!.Instance.Should().Be("imp-3");
    }

    [Fact]
    public void ParalyzedAdventurer_KeepsTheTokenDuringTheTurn_ThenIsImmune()
    {
        var game = Game(Fight(imps: 0));
        game.Do(GameCommand.SetEffect("adventurer:A", Paralyze, true));

        game.Do(GameCommand.NextTurn());
        Effects(game).Should().Contain(Paralyze, "it lasts until the end of the turn");

        game.Do(GameCommand.NextTurn());
        Effects(game).Should().NotContain(Paralyze);
        game.Do(GameCommand.SetEffect("adventurer:A", Paralyze, true));
        Effects(game).Should().NotContain(Paralyze);
    }

    [Fact]
    public void AdventurerParalyzedDuringTheirTurn_KeepsItForTheNextOne()
    {
        var game = Game(Fight(imps: 0));
        game.Do(GameCommand.NextTurn()).Do(GameCommand.SetEffect("adventurer:A", Paralyze, true));

        game.Do(GameCommand.NextTurn());
        Effects(game).Should().Contain(Paralyze);

        game.Do(GameCommand.NextTurn());
        Effects(game).Should().NotContain(Paralyze);
    }

    private static List<string> Effects(EngineDriver game) => game.Encounter.Adventurers.Single().Effects;

    private static void PlayUntilAsked(EngineDriver game)
    {
        for (var i = 0; i < 4 && game.State.Prompt is null; i++)
        {
            game.Do(GameCommand.NextTurn());
        }

        game.State.Prompt!.Kind.Should().Be(PromptKinds.AiCondition);
    }

    private static string Json(GameState state) => JsonSerializer.Serialize(state, JsonDefaults.FileOptions);

    private static string Replayed(EngineDriver game)
    {
        GameState Saved(GameState state) => JsonSerializer.Deserialize<GameState>(Json(state), JsonDefaults.FileOptions)!;
        return Json(game.History.Aggregate(Saved(GameEngine.Start(game.Context, game.Party)),
            (state, command) => Saved(GameEngine.Apply(state, command, game.Context).Value)));
    }

    private static ScenarioAction Say(string text) => new() { Do = "showText", Text = Text(text) };

    private static ScenarioAction Question() =>
        new() { Do = "askChoice", Question = Text("Which?"), Options = [new ActionOption { Label = Text("This") }] };

    private static ScenarioEvent On(string trigger, ScenarioAction action) =>
        new() { Id = "event", Trigger = new EventTrigger { On = trigger }, Once = true, Actions = [action] };

    private static Dictionary<string, string> Text(string text) => new() { ["en"] = text };

    private static Node Fight(int imps = 1, ScenarioEvent? events = null)
    {
        var placed = string.Join(',', Enumerable.Repeat("""{"family":"imp"}""", imps));
        var node = Read<Node>($$$"""
            {"id":"fight","type":"encounter","lootLevel":"common","setup":{"combatants":[{{{placed}}}]},
             "endConditions":[{"id":"win","kind":"win"},{"id":"lose","kind":"lose"}]}
            """);
        return events is null ? node : node with { Events = [events] };
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)!;

    private static EngineDriver Game(Node node) => new(
        new Scenario { Id = "hb:scenario/rounds", Title = Text("Rounds"), Start = node.Id, Nodes = [node] },
        new Catalog([new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = Text("Homebrew") },
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":4},
                     "aiSteps":[{"condition":{"text":{"en":"Adjacent?"}},"instructions":[]}]}
                    """),
            ],
        }]),
        "A");
}
