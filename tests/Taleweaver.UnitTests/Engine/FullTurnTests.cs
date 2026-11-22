using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.UnitTests.Engine;

/// <summary>A whole turn on the GM's side (docs/design/play-spec.md, lot 3).</summary>
public class FullTurnTests
{
    private const string A = "adventurer:A";
    private const string B = "adventurer:B";

    // ---- R1: the phases ----

    [Fact]
    public void R1_Poison_IsAskedInTheStatusPhase()
    {
        var game = Game().Do(GameCommand.SetEffect(A, "poison", true));

        NextTurnOf(game, A);
        game.State.Prompt!.Kind.Should().Be("status");
        game.Do(Command("""{"type":"answer","option":"apply","value":4}"""));

        Member(game, "A").Damage.Should().Be(4);
        game.State.Prompt.Should().BeNull();
        SameWhenReplayed(game);
    }

    [Fact]
    public void R1_Condemn_DefeatsOnTheSkull()
    {
        var game = Game().Do(GameCommand.SetEffect(A, "condemn", true));

        NextTurnOf(game, A);
        game.Answer("skull");

        Member(game, "A").Defeated.Should().BeTrue();
    }

    [Fact]
    public void R1_Haste_GivesOneSp()
    {
        var game = Game().Do(GameCommand.SetEffect(A, "haste", true));

        NextTurnOf(game, A);
        game.Answer("apply");

        Member(game, "A").Stamina.Should().Be(4);
    }

    [Fact]
    public void R1_OfAnOpponent_ComesBeforeItsAi()
    {
        var game = Game(imps: 1).Do(GameCommand.SetEffect("imp-1", "poison", true));

        NextTurnOf(game, "hb:initiative/imp");
        game.State.Prompt!.Kind.Should().Be("status");
        game.Do(Command("""{"type":"answer","option":"apply","value":1}"""));

        game.Combatant("imp-1").Damage.Should().Be(1);
        game.State.Prompt!.Kind.Should().Be(PromptKinds.AiCondition);
    }

    // ---- R2: what actions cost ----

    [Theory]
    [InlineData("move", 2)]
    [InlineData("extraMove", 2)]
    [InlineData("attack", 1)]
    [InlineData("encounterAction", 2)]
    [InlineData("reequip", 2)]
    [InlineData("dodge", 2)]
    [InlineData("empower", 2)]
    [InlineData("counter", 1)]
    [InlineData("counterDodged", 2)]
    public void R2_ActionsTakeTheirSp(string action, int left)
    {
        var game = Game().Do(Command($$"""{"type":"spend","instance":"adventurer:A","effect":"{{action}}"}"""));

        Member(game, "A").Stamina.Should().Be(left);
    }

    [Fact]
    public void R2_BeyondWhatIsHeld_IsSignalled_NotRefused()
    {
        var spend = Command("""{"type":"spend","instance":"adventurer:A","effect":"attack"}""");
        var game = Game().Do(spend).Do(spend);

        Member(game, "A").Stamina.Should().Be(0);
        game.Codes.Should().Contain("staminaShort");
    }

    // ---- R3: reactions ----

    [Fact]
    public void R3_AReaction_HoldsTheTurn_WhichThenResumes()
    {
        var game = Game(imps: 1);
        NextTurnOf(game, "hb:initiative/imp");

        game.Do(Command("""{"type":"react","instance":"adventurer:A","effect":"counter","against":"imp-1"}"""));
        Member(game, "A").Stamina.Should().Be(1);
        GameEngine.Apply(game.State, GameCommand.Answer("yes"), game.Context).IsSuccess.Should().BeFalse();

        game.Do(Command("""{"type":"endReaction"}"""));
        game.State.Prompt!.Kind.Should().Be(PromptKinds.AiCondition);
        game.Answer("no");
    }

    [Fact]
    public void R3_OneBreakAttackPerTurn()
    {
        var attack = Command("""{"type":"react","instance":"adventurer:A","effect":"break","against":"imp-1"}""");
        var game = Game(imps: 1).Do(attack).Do(Command("""{"type":"endReaction"}"""));

        GameEngine.Apply(game.State, attack, game.Context).IsSuccess.Should().BeFalse();
        Member(game, "A").Stamina.Should().Be(3);
    }

    [Fact]
    public void R3_Redirect_TargetsTheNewFigure()
    {
        var game = Game(imps: 1).Do(Command("""{"type":"react","instance":"imp-1","effect":"redirect","against":"adventurer:A"}"""));

        game.Combatant("imp-1").Attacked.Should().BeTrue();
    }

    // ---- R4: cards and tokens ----

    [Fact]
    public void R4_Exhausted_ComesBackAtTheRefreshPhase()
    {
        var game = Game().Do(Command("""{"type":"card","instance":"adventurer:A","item":"hb:discipline/heart","effect":"exhausted","active":true}"""));
        Json(Member(game, "A")).GetProperty("exhaustedCards").GetArrayLength().Should().Be(1);

        NextTurnOf(game, A);

        Json(Member(game, "A")).TryGetProperty("exhaustedCards", out _).Should().BeFalse();
    }

    [Fact]
    public void R4_Used_ComesBackAtTheNextEncounter()
    {
        var game = Game(second: true).Do(Command("""{"type":"card","instance":"adventurer:A","item":"hb:discipline/heart","effect":"used","active":true}"""));
        Json(Member(game, "A")).GetProperty("usedCards").GetArrayLength().Should().Be(1);

        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        Json(Member(game, "A")).TryGetProperty("usedCards", out _).Should().BeFalse();
    }

    [Fact]
    public void R4_AFlippedDiscipline_LosesItsIcons_UntilTheRestore()
    {
        var game = Game(end: """,{"do":"restore"}""");
        Member(game, "A").Health.Should().Be(14);

        game.Do(Command("""{"type":"card","instance":"adventurer:A","item":"hb:discipline/heart","effect":"flipped","active":true}"""));
        Member(game, "A").Health.Should().Be(12);

        game.Do(GameCommand.ReachEndCondition("win"));
        Member(game, "A").Health.Should().Be(14);
    }

    [Fact]
    public void R4_AbilityTokens_StayWithinTheirLimit_UntilTheRestore()
    {
        var game = Game(end: """,{"do":"restore"}""").Do(Command("""{"type":"abilityToken","instance":"adventurer:A","token":"dodge","value":5}"""));

        Json(Member(game, "A")).GetProperty("abilityTokens").GetProperty("dodge").GetInt32().Should().Be(3);
        game.Codes.Should().Contain("tokenLimit");

        game.Do(GameCommand.ReachEndCondition("win"));
        Member(game, "A").AbilityTokens.Should().BeNull();
    }

    // ---- R5: Disease, Tether ----

    [Fact]
    public void R5_Disease_BlocksHealing_AndPositiveEffects()
    {
        var game = Game().Do(GameCommand.Damage(A, 5)).Do(GameCommand.SetEffect(A, "disease", true));

        game.Do(GameCommand.Damage(A, -3)).Do(GameCommand.SetEffect(A, "haste", true));

        Member(game, "A").Damage.Should().Be(5);
        game.Encounter.Adventurers[0].Effects.Should().Equal("disease");
    }

    [Fact]
    public void R5_Tether_CopiesDamageAndHealing_Once()
    {
        var game = Game()
            .Do(Command("""{"type":"tether","instance":"adventurer:A","against":"adventurer:B"}"""))
            .Do(GameCommand.Damage(A, 5))
            .Do(GameCommand.Damage(A, -2));

        Member(game, "B").Damage.Should().Be(3);
        game.Do(GameCommand.Damage(B, 1));
        Member(game, "A").Damage.Should().Be(3);
    }

    // ---- R6: taking, not giving ----

    [Fact]
    public void R6_InAnEncounter_APlayerDoesNotGive()
    {
        var game = Game().Do(GameCommand.AddItem("hb:item/blade", "A", "pack"));
        var give = new PlayerCommand(PlayerCommand.MoveItem, Item: "item-1", Owner: "B", Slot: "pack");

        PlayerCommands.Check(give, ["A"], game.State.Campaign, game.Encounter).Should().NotBeNull();
        PlayerCommands.Check(give, ["A"], game.State.Campaign).Should().BeNull();
    }

    // ---- R7, R8: the Stash, free Re-Equip ----

    [Fact]
    public void R7_TakenFromTheStashOutOfAStoryRound_IsSignalled()
    {
        var game = Game().Do(GameCommand.AddItem("hb:item/blade", "stash")).Do(GameCommand.MoveItem("item-1", "A", "pack"));

        Member(game, "A").Pack.Should().ContainSingle();
        game.Codes.Should().Contain("stashOutsideStoryRound");
    }

    [Fact]
    public void R8_AGainAndARestore_OfferAFreeReequip()
    {
        var game = Game(start: """{"do":"gainItem","ref":"hb:item/blade","who":"B"}""", end: """,{"do":"restore"}""");
        Json(game.State.Campaign).GetProperty("freeReequip").EnumerateArray().Select(e => e.GetString()).Should().Equal("B");

        game.Do(Command("""{"type":"reequipped","instance":"adventurer:B"}"""));
        Json(game.State.Campaign).TryGetProperty("freeReequip", out _).Should().BeFalse();

        game.Do(GameCommand.ReachEndCondition("win"));
        Json(game.State.Campaign).GetProperty("freeReequip").GetArrayLength().Should().Be(2);
    }

    // ---- R11: combatant tokens ----

    [Fact]
    public void R11_ATentacle_GoesOnWhoStartsTheirTurnOnIt()
    {
        var game = Game(tokens: """{"id":"t","type":"um1:token/combatant/tentacle","x":1,"y":1}""").Do(GameCommand.Move(A, new Cell(1, 1)));

        NextTurnOf(game, A);

        game.Encounter.Adventurers[0].Effects.Should().Contain("tentacle");
        game.Encounter.Tokens.Should().OnlyContain(t => t.Removed);
    }

    [Fact]
    public void R11_ABulb_IsFlipped()
    {
        var game = Game(tokens: """{"id":"b","type":"um1:token/combatant/bulb","x":1,"y":1}""").Do(Command("""{"type":"flipToken","token":"b"}"""));

        Json(game.Encounter.Tokens[0]).GetProperty("flipped").GetBoolean().Should().BeTrue();
    }

    // ---- R12: no instruction possible ----

    [Fact]
    public void R12_NoInstructionPossible_MakesTheConditionFalse()
    {
        var game = Game(imps: 1);
        NextTurnOf(game, "hb:initiative/imp");
        game.Answer("yes");

        game.Do(Command("""{"type":"noInstruction"}"""));

        game.State.Log.Last(l => l.Code == "aiInstruction").Text!["en"].Should().Be("Wait.");
        game.Encounter.RoundLog.AiStepTrue.Should().BeTrue();
        SameWhenReplayed(game);
    }

    [Fact]
    public void R12_OnTheLastStep_TheRoundHadNoTrueStep()
    {
        var game = Game(imps: 1);
        NextTurnOf(game, "hb:initiative/imp");
        game.Answer("no");

        game.Do(Command("""{"type":"noInstruction"}"""));

        game.Encounter.RoundLog.AiStepTrue.Should().BeFalse();
        game.Encounter.Activation.Should().BeNull();
    }

    // ---- Helpers ----

    /// <summary>Turns are played until it is this card's.</summary>
    private static void NextTurnOf(EngineDriver game, string card)
    {
        for (var i = 0; i < 8; i++)
        {
            game.Do(GameCommand.NextTurn());
            if (game.CurrentCard == card)
            {
                return;
            }

            while (game.State.Prompt is { } prompt)
            {
                game.Answer(prompt.Kind == "status" ? "skip" : "no");
            }
        }

        throw new InvalidOperationException($"No turn for {card}.");
    }

    private static AdventurerState Member(EngineDriver game, string name) => game.State.Campaign.Party.Single(a => a.Name == name);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)!;

    private static GameCommand Command(string json) => Read<GameCommand>(json);

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value, JsonDefaults.FileOptions);

    private static void SameWhenReplayed(EngineDriver game)
    {
        GameState Saved(GameState state) =>
            JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state, JsonDefaults.FileOptions), JsonDefaults.FileOptions)!;
        var replayed = game.History.Aggregate(Saved(GameEngine.Start(game.Context, game.Party)),
            (state, command) => Saved(GameEngine.Apply(state, command, game.Context).Value));
        JsonSerializer.Serialize(replayed, JsonDefaults.FileOptions).Should().Be(JsonSerializer.Serialize(game.State, JsonDefaults.FileOptions));
    }

    private static EngineDriver Game(int imps = 0, bool second = false, string start = "", string end = "", string tokens = "")
    {
        var learn = """{"do":"gainDiscipline","ref":"hb:discipline/heart","adventurer":"A"}""";
        var started = start.Length > 0 ? learn + "," + start : learn;
        var setup = string.Join(',', Enumerable.Repeat("""{"family":"imp"}""", imps));
        var nodes = new List<Node>
        {
            Read<Node>($$$"""
                {"id":"fight","type":"encounter","lootLevel":"common","map":{"tokens":[{{{tokens}}}]},"setup":{"combatants":[{{{setup}}}]},
                 "events":[{"id":"s","trigger":{"on":"encounterStart"},"actions":[{{{started}}}]}],
                 "endConditions":[{"id":"win","kind":"win","next":"{{{(second ? "second" : "@end")}}}","mechanics":[{"do":"showText"}{{{end}}}]},{"id":"lose","kind":"lose"}]}
                """),
        };
        if (second)
        {
            nodes.Add(Read<Node>("""{"id":"second","type":"encounter","lootLevel":"common","endConditions":[{"id":"win","kind":"win"}]}"""));
        }

        var name = new Dictionary<string, string> { ["en"] = "Turn" };
        var pack = new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = name },
            Rules = Read<PackRules>("""
                {"effects":[{"id":"poison","name":{"en":"Poison"},"condition":"status","polarity":"negative"},
                            {"id":"condemn","name":{"en":"Condemn"},"condition":"status","polarity":"negative"},
                            {"id":"haste","name":{"en":"Haste"},"condition":"status","polarity":"positive"},
                            {"id":"disease","name":{"en":"Disease"},"condition":"passive","polarity":"negative"}],
                 "abilityTokens":[{"id":"dodge","name":{"en":"Dodge"},"limit":3}]}
                """),
            Items = [Read<Item>("""{"id":"hb:item/blade","name":{"en":"Blade"},"type":"weapon","tier":"common"}""")],
            Disciplines = [Read<Discipline>("""{"id":"hb:discipline/heart","name":{"en":"Heart"},"tree":"sanctus","level":1,"health":2}""")],
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":4},
                     "aiSteps":[{"condition":{"text":{"en":"In range?"},"kind":"ask"},"instructions":[{"text":{"en":"Attack."}}]},
                                {"condition":{"text":{"en":"Always"},"kind":"always"},"instructions":[{"text":{"en":"Wait."}}]}]}
                    """),
            ],
        };
        return new EngineDriver(new Scenario { Id = "hb:scenario/turn", Title = name, Start = "fight", Nodes = nodes }, new Catalog([pack]), "A", "B");
    }
}
