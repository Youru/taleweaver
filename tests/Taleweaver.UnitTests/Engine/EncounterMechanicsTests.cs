using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The mechanics of the encounters (docs/design/play-spec.md, lot 4).</summary>
public class EncounterMechanicsTests
{
    private const string Imps = """{"family":"imp"}""";

    // ---- A8: counters ----

    [Fact]
    public void A8_Counter_CrossingAThreshold_FiresOnce()
    {
        var game = Game(Fight(start: """{"do":"setCounter","id":"time","amount":0}""",
            events: """{"id":"late","trigger":{"on":"counter","counter":"time","value":2},"actions":[{"do":"gainGold","amount":5}]}"""));

        for (var i = 0; i < 3; i++)
        {
            game.Do(Command("""{"type":"counter","item":"time","value":1}"""));
        }

        game.State.Campaign.Gold.Should().Be(5);
        Json(game.State.Campaign).GetProperty("counters")[0].GetProperty("value").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(""","keep":true""", 1)]
    public void A8_OnlyAKeptCounter_GoesToTheNextEncounter(string keep, int kept)
    {
        var game = Game(
            Fight(start: $$"""{"do":"setCounter","id":"time","amount":2{{keep}}}""", next: "second"),
            Fight(id: "second"));

        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        var campaign = Json(game.State.Campaign);
        var count = campaign.TryGetProperty("counters", out var counters) ? counters.GetArrayLength() : 0;
        count.Should().Be(kept);
    }

    // ---- A9: the track ----

    [Theory]
    [InlineData("front", 0)]
    [InlineData("back", 2)]
    public void A9_MovedAfterTheDeal_HoldsForTheFirstRound(string to, int place)
    {
        var game = Game(Fight(Imps, start: $$"""{"do":"moveInitiative","who":"B","to":"{{to}}"}"""));
        game.Encounter.InitiativeTrack.IndexOf("adventurer:B").Should().Be(place);
        var dealt = game.Encounter.InitiativeTrack.ToList();

        game.Do(GameCommand.NextTurn());

        game.Encounter.InitiativeTrack.Should().Equal(dealt);
        SameWhenReplayed(game);
    }

    [Fact]
    public void A9_TheGmDealsTheTrack_ThenMoves()
    {
        var game = Game(Fight(Imps)).Do(Command("""{"type":"dealInitiative"}"""));
        game.Do(GameCommand.MoveInitiative("adventurer:B", -3));

        game.Do(GameCommand.NextTurn());

        game.CurrentCard.Should().Be("adventurer:B");
    }

    // ---- A10: several cards ----

    [Fact]
    public void A10_ASecondCard_IsASecondTurn()
    {
        var game = Game(Fight(start: """{"do":"addInitiativeCard","who":"A"}"""));

        game.PlayRound();

        game.Encounter.InitiativeTrack.Should().HaveCount(3);
        game.State.Log.Count(l => l.Code == "turn" && l.Args!.GetValueOrDefault("adventurer") == "A").Should().Be(2);
    }

    [Fact]
    public void A10_OfACombatant_LeavesWithIt()
    {
        var game = Game(Fight(Imps, start: """{"do":"addInitiativeCard","instance":"imp-1"}"""));
        game.Encounter.InitiativeTrack.Should().HaveCount(4);

        game.Do(GameCommand.Defeat("imp-1"));

        game.Encounter.InitiativeTrack.Should().HaveCount(2);
    }

    [Fact]
    public void A10_AFixedCard_IsNeverShuffled()
    {
        var game = Game(Fight(Imps, start: """{"do":"addInitiativeCard","instance":"imp-1","card":"hb:initiative/dark","to":"back","fixed":true}"""));

        for (var round = 0; round < 5; round++)
        {
            game.Do(GameCommand.NextTurn());
            game.Encounter.InitiativeTrack[^1].Should().Be("hb:initiative/dark");
            while (game.Encounter.PlayedCards.Count < game.Encounter.InitiativeTrack.Count)
            {
                game.Turn();
            }
        }
    }

    // ---- A17: currents ----

    [Fact]
    public void A17_TurnEnd_IsATrigger_AndCurrentsAreRemindedOf()
    {
        var game = Game(Fight(
            map: ""","map":{"currents":[{"rect":{"x":0,"y":0,"w":2,"h":2},"strength":1,"direction":"south"}]}""",
            events: """{"id":"flow","trigger":{"on":"turnEnd"},"actions":[{"do":"gainGold","amount":1}]}"""));

        game.Do(GameCommand.NextTurn()).Do(GameCommand.NextTurn());

        game.State.Campaign.Gold.Should().Be(1);
        game.Codes.Should().Contain("currentReminder");
    }

    // ---- A11: guests ----

    [Fact]
    public void A11_TheGuestsTurn_SaysWhoPlaysIt()
    {
        var game = Game(Fight(start: """{"do":"spawn","ref":"hb:combatant/guard","adventurer":"B"}"""));

        game.PlayRound();

        game.State.Log.Last(l => l.Code == "turn" && l.Args!.ContainsKey("ally")).Args!["playedBy"].Should().Be("B");
    }

    // ---- A13: sides ----

    [Fact]
    public void A13_OnThePartysSide_NoLootNoUrgencyCredit_NotCountedWithTheOpponents()
    {
        var game = Game(Fight("""{"family":"imp","side":"players"}""",
            events: """{"id":"clear","trigger":{"on":"roundStart"},"conditions":[{"var":"alive.imp","op":"==","value":0}],"actions":[{"do":"gainGold","amount":1}]}"""));

        game.Do(GameCommand.NextTurn()).Do(GameCommand.Defeat("imp-1"));

        game.State.Campaign.Gold.Should().Be(1);
        game.Encounter.RoundLog.Defeated.Should().Be(0);
        game.Encounter.LootDeck.Should().HaveCount(9);
    }

    [Fact]
    public void A13_ACampCanBeCounted()
    {
        var game = Game(Fight("""{"family":"imp","side":"wild"}""",
            events: """{"id":"count","trigger":{"on":"roundStart"},"conditions":[{"var":"alive.side:wild","op":"==","value":1}],"actions":[{"do":"gainGold","amount":1}]}"""));

        game.Do(GameCommand.NextTurn());

        game.State.Campaign.Gold.Should().Be(1);
    }

    // ---- A14: the board kept ----

    [Fact]
    public void A14_TheBoardIsKept()
    {
        var game = Game(
            Fight(Imps, map: ""","map":{"tokens":[{"id":"t","type":"hb:token/objective","color":"red","x":7,"y":7}]}""",
                start: """{"do":"setCounter","id":"time","amount":2}""", next: "second"),
            Fight(id: "second", map: ""","keepBoardFrom":"fight" """));
        game.Do(GameCommand.Damage("imp-1", 2)).Do(GameCommand.Move("imp-1", new Cell(4, 5))).Do(GameCommand.Move("adventurer:A", new Cell(1, 2)));

        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        var imp = game.Combatant("imp-1");
        imp.Damage.Should().Be(2);
        imp.Position.Should().Be(new Cell(4, 5));
        game.Encounter.Adventurers[0].Position.Should().Be(new Cell(1, 2));
        game.Encounter.Tokens.Should().ContainSingle(t => t.Id == "t");
        game.Encounter.InitiativeTrack.Should().Contain("hb:initiative/imp");
        Json(game.State.Campaign).GetProperty("counters").GetArrayLength().Should().Be(1);
        SameWhenReplayed(game);
    }

    // ---- A16: drawn among the party ----

    [Fact]
    public void A16_DrawnWithExclusion_IsKept()
    {
        var game = Game(Fight(start: """{"do":"drawAdventurers","var":"chosen","except":["A"]},{"do":"gainXp","amount":2,"who":"@chosen"}"""));

        game.State.Campaign.Party.Select(a => a.Xp).Should().Equal(0, 2);
        SameWhenReplayed(game);
    }

    [Fact]
    public void A16_SplitBetweenStartingZones()
    {
        var game = Game(Fight(start: """{"do":"splitParty","zones":["east","west"]}""",
            map: ""","map":{"zones":[{"id":"west","kind":"startingZone","rect":{"x":0,"y":0}},{"id":"east","kind":"startingZone","rect":{"x":9,"y":0}}]}"""));

        game.Encounter.Adventurers.Select(a => a.Position!.Value.X).Should().BeEquivalentTo(new[] { 0, 9 });
        SameWhenReplayed(game);
    }

    // ---- A18: later ----

    [Fact]
    public void A18_ADelayedEffect_HappensAtItsRound()
    {
        var game = Game(Fight(start: """{"do":"delay","id":"blast","rounds":2,"actions":[{"do":"gainGold","amount":7}]}"""));

        game.PlayRound();
        game.Do(GameCommand.NextTurn());
        game.State.Campaign.Gold.Should().Be(0);

        game.Turn();
        game.Turn();
        game.State.Campaign.Gold.Should().Be(7);
        SameWhenReplayed(game);
    }

    // ---- A12: figures of the scenario ----

    [Fact]
    public void A12_AFigureOfTheScenario_HasItsStats_AndCanBeDefeated()
    {
        var game = Game(Fight("""{"ref":"figure:altar"}""",
            map: ""","figures":[{"id":"altar","name":{"en":"Altar"},"stats":{"health":3,"defense":8,"convictionDice":["purple"]}}]"""));

        Json(game.Combatant("altar")).GetProperty("stats").GetProperty("defense").GetInt32().Should().Be(8);
        game.Do(GameCommand.Damage("altar", 3));

        game.Combatant("altar").Defeated.Should().BeTrue();
        game.Encounter.LootDeck.Should().HaveCount(9);
    }

    [Fact]
    public void A12_NotAttackable_TakesNoDamage()
    {
        var game = Game(Fight("""{"ref":"figure:statue"}""", map: ""","figures":[{"id":"statue","attackable":false}]"""));

        GameEngine.Apply(game.State, GameCommand.Damage("statue", 1), game.Context).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void A12_R11_ARoot_IsABuiltInFigure()
    {
        var game = Game(Fight("""{"ref":"figure:root"}"""));

        game.Do(GameCommand.Damage("root", 6));

        game.Combatant("root").Defeated.Should().BeTrue();
        game.Encounter.LootDeck.Should().HaveCount(9);
    }

    // ---- A15: rules that change a card ----

    [Fact]
    public void A15_StatsAndTags_OfOneFigure()
    {
        var game = Game(Fight(Imps, start: """{"do":"modifyFigure","instance":"imp-1","stats":{"health":7},"bonus":{"armor":2},"addTags":["TRIVIAL"]}"""));

        game.Combatant("imp-1").Health.Should().Be(7);
        Json(game.Combatant("imp-1")).GetProperty("stats").GetProperty("armor").GetInt32().Should().Be(3);
        game.Do(GameCommand.Damage("imp-1", 7));
        game.Encounter.LootDeck.Should().HaveCount(9);
    }

    [Fact]
    public void A15_AFamilysRule_HoldsForThoseToCome()
    {
        var game = Game(Fight(start: """{"do":"modifyFigure","family":"imp","stats":{"health":9}},{"do":"spawn","family":"imp"}"""));

        game.Combatant("imp-1").Health.Should().Be(9);
    }

    [Fact]
    public void A15_Asleep_UntilDamaged()
    {
        var game = Game(Fight(Imps, start: """{"do":"modifyFigure","instance":"imp-1","asleep":true,"wakesWhenDamaged":true}"""));

        game.PlayRound();
        game.Codes.Should().Contain("asleep").And.NotContain("aiInstruction");

        game.Do(GameCommand.Damage("imp-1", 1));
        game.PlayRound();
        game.Codes.Should().Contain("aiInstruction");
    }

    [Fact]
    public void A15_AStepIgnored_AnotherAdded()
    {
        var game = Game(Fight(Imps, start: """
            {"do":"modifyFigure","instance":"imp-1","skipSteps":[1]},
            {"do":"appendAiStep","instance":"imp-1","step":{"condition":{"text":{"en":"Always"},"kind":"always"},"instructions":[{"text":{"en":"Dance."}}]}}
            """));

        game.PlayRound();

        game.State.Log.Where(l => l.Code == "aiInstruction").Select(l => l.Text!["en"]).Should().Equal("Dance.");
    }

    // ---- A21: opposing Espers ----

    [Fact]
    public void A21_AnEsperSummoned_TakesItsSummonersValues_AndDropsNoLoot()
    {
        var game = Game(Fight("""{"ref":"hb:combatant/summoner","instance":"boss"}""",
            map: ""","encounterEspers":[{"ref":"hb:combatant/esper","summoner":"boss"}]"""));

        game.Do(Command("""{"type":"summonEsper","card":"hb:combatant/esper","instance":"boss"}"""));

        var esper = game.Encounter.Combatants.Single(c => c.Family == "esper");
        esper.Health.Should().Be(11);
        Json(esper).GetProperty("force").GetInt32().Should().Be(12);
        game.Do(GameCommand.Damage(esper.Instance, 11));
        game.Encounter.LootDeck.Should().HaveCount(9);
    }

    [Fact]
    public void A21_OneOfTheSetup_DropsLoot()
    {
        var game = Game(Fight("""{"ref":"hb:combatant/summoner","instance":"boss"},{"ref":"hb:combatant/esper","instance":"pet","summoner":"boss"}"""));

        game.Do(GameCommand.Damage("pet", 11));

        game.Encounter.LootDeck.Should().HaveCount(8);
    }

    // ---- Helpers ----

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

    /// <summary><paramref name="map"/>: more fields of the node.</summary>
    private static Node Fight(string setup = "", string start = "", string events = "", string map = "", string id = "fight", string next = "@end")
    {
        var all = new[] { start.Length > 0 ? $$$"""{"id":"start","trigger":{"on":"encounterStart"},"actions":[{{{start}}}]}""" : "", events };
        return Read<Node>($$$"""
            {"id":"{{{id}}}","type":"encounter","lootLevel":"common","setup":{"combatants":[{{{setup}}}]},
             "events":[{{{string.Join(',', all.Where(e => e.Length > 0))}}}],
             "endConditions":[{"id":"win","kind":"win","next":"{{{next}}}"},{"id":"lose","kind":"lose"}]{{{map}}}}
            """);
    }

    private static EngineDriver Game(params Node[] nodes)
    {
        var name = new Dictionary<string, string> { ["en"] = "Mechanics" };
        var pack = new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = name },
            Rules = Read<PackRules>("""{"lootDecks":[{"lootLevel":"common","cards":[{"kind":"gold","value":5,"count":9}]}]}"""),
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":4,"armor":1},
                     "aiSteps":[{"condition":{"text":{"en":"Always"},"kind":"always"},"instructions":[{"text":{"en":"Bite."}}]}]}
                    """),
                Read<Combatant>("""{"id":"hb:combatant/guard","family":"guard","name":{"en":"Guard"},"kind":"command","initiativeCards":["hb:initiative/guard"],"stats":{"health":8}}"""),
                Read<Combatant>("""{"id":"hb:combatant/summoner","family":"summoner","name":{"en":"Summoner"},"kind":"intelligent","tags":["SUMMONER 12"],"stats":{"health":11,"defense":9}}"""),
                Read<Combatant>("""{"id":"hb:combatant/esper","family":"esper","name":{"en":"Esper"},"kind":"intelligent","tags":["ESPER"],"stats":{"health":"*","defense":"*"}}"""),
            ],
        };
        return new EngineDriver(new Scenario { Id = "hb:scenario/mechanics", Title = name, Start = "fight", Nodes = nodes }, new Catalog([pack]), "A", "B");
    }
}
