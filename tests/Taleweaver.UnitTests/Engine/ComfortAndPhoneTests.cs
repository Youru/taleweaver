using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The GM's comfort and what is in the players' hands (docs/design/play-spec.md, lots 5 and 6).</summary>
public class ComfortAndPhoneTests
{
    // ---- M3: corrections ----

    [Theory]
    [InlineData("""{"effect":"gold","value":12}""", "gold", "12")]
    [InlineData("""{"effect":"xp","instance":"B","value":7}""", "xp", "7")]
    [InlineData("""{"effect":"flag","item":"met","option":"true"}""", "flag", "true")]
    [InlineData("""{"effect":"urgency","value":2}""", "urgency", "2")]
    public void M3_ACorrection_IsWrittenInTheJournal(string fields, string field, string now)
    {
        var game = Game();

        game.Do(Correct(fields));

        game.State.Log.Last(l => l.Code == "corrected").Args.Should().Contain("field", field).And.Contain("to", now);
        SameWhenReplayed(game);
    }

    [Fact]
    public void M3_Values_AreChanged()
    {
        var game = Game().Do(GameCommand.NextTurn());

        game.Do(Correct("""{"effect":"gold","value":12}"""))
            .Do(Correct("""{"effect":"xp","instance":"B","value":7}"""))
            .Do(Correct("""{"effect":"flag","item":"met","option":"true"}"""))
            .Do(Correct("""{"effect":"urgency","value":2}"""))
            .Do(Correct("""{"effect":"round","value":4}"""));

        game.State.Campaign.Gold.Should().Be(12);
        game.State.Campaign.Party[1].Xp.Should().Be(7);
        game.State.Campaign.Variables["met"].GetBoolean().Should().BeTrue();
        game.Encounter.Urgency.Should().Be(2);
        game.Encounter.Round.Should().Be(4);
    }

    [Fact]
    public void M3_ASpawn_PutsTheFigureAndItsCard()
    {
        var game = Game();

        game.Do(Correct("""{"effect":"spawn","item":"imp","value":2}"""));

        game.Encounter.Combatants.Should().HaveCount(2);
        game.Encounter.InitiativeTrack.Should().Contain("hb:initiative/imp");
        Refused(game, Correct("""{"effect":"spawn","item":"dragon"}"""));
    }

    [Fact]
    public void M3_ACard_LeavesTheTrackAndComesBack_ButNotTheOnePlaying()
    {
        var game = Game().Do(GameCommand.NextTurn());
        var waiting = game.Encounter.InitiativeTrack.Single(c => c != game.CurrentCard);

        game.Do(Correct($$"""{"effect":"track","card":"{{waiting}}","active":false}"""));
        game.Encounter.InitiativeTrack.Should().NotContain(waiting);
        game.Do(Correct($$"""{"effect":"track","card":"{{waiting}}","active":true}"""));

        game.Encounter.InitiativeTrack.Should().Contain(waiting);
        Refused(game, Correct($$"""{"effect":"track","card":"{{game.CurrentCard}}","active":false}"""));
        Refused(game, Correct("""{"effect":"track","card":"nothing","active":true}"""));
    }

    [Fact]
    public void M3_AStep_IsEnteredWhateverWasAsked()
    {
        var game = Game();

        game.Do(Correct("""{"effect":"node","item":"after"}"""));

        game.State.Campaign.CurrentNode.Should().Be("after");
        game.State.Encounter.Should().BeNull();
        game.Codes.Should().Contain("corrected");
        Refused(game, Correct("""{"effect":"node","item":"nowhere"}"""));
        SameWhenReplayed(game);
    }

    [Fact]
    public void M3_AQuestion_StaysWhileAValueIsCorrected()
    {
        var game = Game();
        game.Do(GameCommand.ReachEndCondition("win"));

        game.Do(Correct("""{"effect":"gold","value":3}"""));

        game.State.Prompt!.Kind.Should().Be(PromptKinds.Continue);
        game.State.Campaign.Gold.Should().Be(3);
    }

    // ---- M9, M11 ----

    [Fact]
    public void M9_Ready_GivesTheTrack_OnlyBeforeTheFirstRound()
    {
        var game = Game();

        game.Do(Command("""{"type":"ready"}"""));
        var given = game.Encounter.InitiativeTrack.ToList();
        Json(game.Encounter).GetProperty("ready").GetBoolean().Should().BeTrue();
        game.Do(GameCommand.NextTurn());

        game.Encounter.InitiativeTrack.Should().Equal(given);
        Refused(game, Command("""{"type":"ready"}"""));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void M11_AnAnnouncedUrgency_MayBeWaived_ForThisRoundOnly(bool waived, int urgency)
    {
        var game = Game().PlayOne();
        if (waived)
        {
            game.Do(Correct("""{"effect":"noUrgency","active":true}"""));
        }

        game.Do(GameCommand.NextTurn());
        game.Encounter.Urgency.Should().Be(urgency);

        game.PlayOne().Do(GameCommand.NextTurn());
        game.Encounter.Urgency.Should().Be(urgency + 1);
    }

    // ---- M13: several figures ----

    [Fact]
    public void M13_OneCommand_HitsThemAll()
    {
        var game = Game().Do(Correct("""{"effect":"spawn","item":"imp","value":3}"""));
        var figures = game.Encounter.Combatants.Select(c => c.Instance).ToList();
        var before = game.History.Count;

        game.Do(Command($$"""{"type":"several","figures":{{JsonSerializer.Serialize(figures)}},"value":2}"""));
        game.Encounter.Combatants.Select(c => c.Damage).Should().Equal(2, 2, 2);
        game.Do(Command($$"""{"type":"several","figures":{{JsonSerializer.Serialize(figures)}},"effect":"poison","active":true}"""));

        game.Encounter.Combatants.Should().OnlyContain(c => c.Effects.Contains("poison"));
        game.History.Count.Should().Be(before + 2);
    }

    [Fact]
    public void M13_OneUnknownFigure_ChangesNothing()
    {
        var game = Game();

        Refused(game, Command("""{"type":"several","figures":["adventurer:A","nobody"],"value":2}"""));

        game.State.Campaign.Party[0].Damage.Should().Be(0);
    }

    // ---- J4, J15 ----

    [Fact]
    public void J4_Stamina_IsWrittenWhateverIsAsked()
    {
        var game = Game();
        game.Do(GameCommand.ReachEndCondition("win"));

        game.Do(GameCommand.SetStamina("A", 0));

        game.State.Prompt!.Kind.Should().Be(PromptKinds.Continue);
    }

    [Fact]
    public void J4_Done_EndsOnlyTheTurnItWasSaidIn()
    {
        var game = Game().Do(GameCommand.NextTurn());
        var card = game.CurrentCard;
        var done = Command($$"""{"type":"nextTurn","card":"{{card}}","value":1}""");

        game.Do(done);

        game.CurrentCard.Should().NotBe(card);
        Refused(game, done);
        game.PlayRound();
        Refused(game, done);
    }

    [Fact]
    public void J15_AClaim_NeverTakesWhatWasHandedOutSince()
    {
        var game = Game();
        game.Do(GameCommand.AddItem("hb:item/knife", "unassigned"));
        var id = game.State.Campaign.Unassigned.Single().Id;
        var claim = Command($$"""{"type":"moveItem","instance":"{{id}}","owner":"B","slot":"pack","option":"unassigned"}""");

        game.Do(GameCommand.MoveItem(id, "A", "pack"));

        Refused(game, claim);
        game.State.Campaign.Party[0].Pack.Should().ContainSingle();
    }

    // ---- What a phone is shown ----

    [Fact]
    public void Phone_ShowsImposedParticipantsAsActive_AndAFigureByItsName()
    {
        var game = Game();
        game.Encounter.Participants = ["B"];
        game.Encounter.Combatants.Add(new CombatantState { Instance = "boat", Card = "figure:boat", Family = "boat", Name = new() { ["en"] = "The boat" } });

        var view = PlayerViewProjector.Project("g", 1, game.State, null, ["A"]);

        view.Party.Select(p => p.Active).Should().Equal(false, true);
        view.Encounter!.Combatants.Single().Name!["en"].Should().Be("The boat");
    }

    [Fact]
    public void J4b_ASeatSeesItsOwnMarks_AndNoOneElses()
    {
        var game = Game();
        game.State.Campaign.Party[0].UsedCards.Add("card:A");
        game.State.Campaign.Party[1].UsedCards.Add("card:B");

        var view = PlayerViewProjector.Project("g", 1, game.State, null, ["A"]);

        Json(view).GetRawText().Should().Contain("card:A").And.NotContain("card:B");
    }

    private static GameCommand Correct(string fields) => Command("""{"type":"correct",""" + fields[1..]);

    private static GameCommand Command(string json) => JsonSerializer.Deserialize<GameCommand>(json, JsonDefaults.Options)!;

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)!;

    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value, JsonDefaults.Options);

    private static void Refused(EngineDriver game, GameCommand command) =>
        GameEngine.Apply(game.State, command, game.Context).IsSuccess.Should().BeFalse();

    private static void SameWhenReplayed(EngineDriver game)
    {
        var replayed = GameEngine.Replay(game.Context, game.Party, game.History).Value;
        Json(replayed).GetRawText().Should().Be(Json(game.State).GetRawText());
    }

    private static EngineDriver Game()
    {
        var name = new Dictionary<string, string> { ["en"] = "Comfort" };
        var pack = new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = name },
            Items = [Read<Item>("""{"id":"hb:item/knife","name":{"en":"Knife"},"type":"weapon","tier":"common"}""")],
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":9}}
                    """),
            ],
        };
        var fight = Read<Node>("""
            {"id":"fight","type":"encounter","lootLevel":"common","endConditions":[
              {"id":"win","kind":"win","next":"after"},{"id":"lost","kind":"lose","builtIn":"allDefeated","next":"after"}]}
            """);
        var after = Read<Node>("""{"id":"after","type":"scene","next":"@end"}""");
        return new EngineDriver(new Scenario { Id = "hb:scenario/comfort", Title = name, Start = "fight", Nodes = [fight, after] }, new Catalog([pack]), "A", "B");
    }
}

file static class Rounds
{
    /// <summary>Until the last card of the round plays.</summary>
    public static EngineDriver PlayOne(this EngineDriver game)
    {
        if (game.Encounter.Round == 0)
        {
            game.Turn();
        }

        while (game.Encounter.InitiativeTrack.Any(c => !game.Encounter.PlayedCards.Contains(c)))
        {
            game.Turn();
        }

        return game;
    }
}
