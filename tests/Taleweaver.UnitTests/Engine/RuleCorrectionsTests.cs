using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The rules corrected by the play review (docs/design/play-spec.md, lot 1a).</summary>
public class RuleCorrectionsTests
{
    private const string Shell = "hb:item/shell";

    [Fact]
    public void LootDeck_NextEncounterWithoutRestore_KeepsTheDrawnCardsOut()
    {
        var game = Game(Cards(), ["A"], Fight("one", "two"), Fight("two"));

        game.Do(GameCommand.Defeat("imp-1")).Do(GameCommand.ReachEndCondition("one-win")).Continue();

        game.Encounter.LootDeck.Should().HaveCount(9);
        Replayed(game).Should().Be(Json(game.State));
    }

    [Fact]
    public void LootDeck_RestoreBetweenEncounters_IsWholeAgain()
    {
        var game = Game(Cards(), ["A"], Fight("one", "rest"), Scene("rest", "two", Do("restore")), Fight("two"));

        game.Do(GameCommand.Defeat("imp-1")).Do(GameCommand.ReachEndCondition("one-win")).Continue().Continue();

        game.Encounter.LootDeck.Should().HaveCount(10);
    }

    [Fact]
    public void LootDeck_HigherLootLevel_OnlyMissesTheDrawnCardsItHasToo()
    {
        var game = Game(Cards(), ["A"], Fight("one", "two"), Fight("two") with { LootLevel = "uncommon" });

        game.Do(GameCommand.Defeat("imp-1")).Do(GameCommand.ReachEndCondition("one-win")).Continue();

        game.Encounter.LootDeck.Count(c => c == "gold:5").Should().Be(2);
        game.Encounter.LootDeck.Count(c => c == "gold:6").Should().Be(7);
    }

    [Fact]
    public void Swarm_DropsOnlyWhenTheLastOfItsGroupFalls()
    {
        var game = Game(Cards("SWARM"), ["A"], Fight("one"));

        game.Do(GameCommand.Defeat("imp-1")).Do(GameCommand.Defeat("imp-2"));
        game.Encounter.LootDeck.Should().HaveCount(10);

        game.Do(GameCommand.Defeat("imp-3"));
        game.Encounter.LootDeck.Should().HaveCount(9);
    }

    [Fact]
    public void Trivial_DropsNothing()
    {
        var game = Game(Cards("TRIVIAL"), ["A"], Fight("one"));

        game.Do(GameCommand.Defeat("imp-1"));

        game.Encounter.LootDeck.Should().HaveCount(10);
    }

    [Fact]
    public void Prepare_HalfOfAnOddHealth_IsRoundedUp()
    {
        var game = Game(Cards(), ["A", "B"], Fight("one", "two"), Fight("two"));

        game.Do(GameCommand.AddItem("hb:item/plate", "A")).Do(GameCommand.Damage("adventurer:A", 12))
            .Do(GameCommand.ReachEndCondition("one-win")).Continue();

        game.State.Campaign.Party[0].Damage.Should().Be(7);
    }

    [Fact]
    public void Join_FullGroup_StaysAtFour()
    {
        var game = Game(Cards(), ["A", "B", "C", "D"], Scene("s", "one", Join("E")), Fight("one")).Continue();

        Figures(game).Should().BeEquivalentTo("A", "B", "C", "D");
    }

    [Fact]
    public void Recovered_WithFourAlreadyActive_DoesNotMakeFive()
    {
        var game = Game(Cards(), ["A", "B", "C", "D"], Scene("s", "one", Do("setInjured", ("adventurer", "A")), Join("E"), Do("setInjured", ("adventurer", "A"), ("value", false))), Fight("one")).Continue();

        Figures(game).Should().BeEquivalentTo("B", "C", "D", "E");
    }

    [Fact]
    public void Join_WithoutAmount_HasTheXpOfAMember()
    {
        var game = Game(Cards(), ["A"], Scene("s", "one", Do("gainXp") with { Each = true, Amount = 6 }, Join("E")), Fight("one"));

        game.State.Campaign.Party.Single(a => a.Name == "E").Xp.Should().Be(6);
    }

    [Fact]
    public void ChooseActiveGroup_AsksForTheFourWhoPlay()
    {
        var game = Game(Cards(), ["A", "B", "C", "D", "E", "F"], Scene("s", "one", Do("setActiveGroup")), Fight("one"));

        game.State.Prompt!.Kind.Should().Be(PromptKinds.ActiveGroup);
        game.State.Prompt!.Options.Should().HaveCount(6);
        game.Do(GameCommand.AnswerGroup("C", "D", "E", "F")).Continue();

        Figures(game).Should().BeEquivalentTo("C", "D", "E", "F");
        Replayed(game).Should().Be(Json(game.State));
    }

    [Fact]
    public void HealedInAnEncounter_IsBackOnTheTrack()
    {
        var game = Game(Cards(), ["A", "B"], Fight("one"));

        game.Do(GameCommand.Damage("adventurer:A", 12)).Do(GameCommand.Damage("adventurer:A", -6));

        game.Encounter.InitiativeTrack.Should().ContainSingle(c => c == "adventurer:A");
        game.Encounter.Adventurers.Single(a => a.Name == "A").Defeated.Should().BeFalse();
    }

    [Fact]
    public void RestoredDuringAnEncounter_IsBackOnTheTrack()
    {
        var restore = new ScenarioEvent { Id = "r", Trigger = new EventTrigger { On = "combatantDefeated" }, Actions = [Do("restore")] };
        var game = Game(Cards(), ["A", "B"], Fight("one") with { Events = [restore] });

        game.Do(GameCommand.Damage("adventurer:A", 12)).Do(GameCommand.Defeat("imp-1"));

        game.Encounter.InitiativeTrack.Should().Contain("adventurer:A");
    }

    [Fact]
    public void Gold_IsNeverBelowZero()
    {
        var game = Game(Cards(), ["A"], Scene("s", "@end", Do("gainGold") with { Amount = 5 }, Do("gainGold") with { Amount = -8 }));

        game.State.Campaign.Gold.Should().Be(0);
    }

    [Fact]
    public void MonsterLootTradedIn_CanBeDrawnAgain()
    {
        var game = Game(Cards("GILDED"), ["A"], Fight("one"));

        game.Do(GameCommand.Defeat("imp-1"));
        var first = game.State.Campaign.Unassigned.Single().Id;
        game.Do(GameCommand.DiscardItem(first, 25)).Do(GameCommand.Defeat("imp-2"));

        game.State.Campaign.Unassigned.Should().ContainSingle().Which.Item.Should().Be(Shell);
    }

    [Fact]
    public void FirstTurnAfterRoundOne_GainsNoStamina()
    {
        var game = Game(Cards(), ["A", "B"], Fight("one") with { Setup = null });
        game.Do(GameCommand.Damage("adventurer:A", 12));
        while (game.Encounter.Round < 2)
        {
            game.Turn();
        }

        game.Do(GameCommand.Damage("adventurer:A", -6)).Do(GameCommand.SetStamina("adventurer:A", 0));
        PlayUntil(game, "adventurer:A");
        game.State.Campaign.Party[0].Stamina.Should().Be(0, "it is A's first turn");

        game.Turn();
        PlayUntil(game, "adventurer:A");
        game.State.Campaign.Party[0].Stamina.Should().Be(3);
        Replayed(game).Should().Be(Json(game.State));
    }

    private static void PlayUntil(EngineDriver game, string card)
    {
        for (var i = 0; i < 6 && game.CurrentCard != card; i++)
        {
            game.Turn();
        }

        game.CurrentCard.Should().Be(card);
    }

    private static IEnumerable<string> Figures(EngineDriver game) => game.Encounter.Adventurers.Select(a => a.Name);

    private static string Json(GameState state) => JsonSerializer.Serialize(state, JsonDefaults.FileOptions);

    /// <summary>The history applied to a state written and read back, as a saved game is.</summary>
    private static string Replayed(EngineDriver game)
    {
        var start = JsonSerializer.Deserialize<GameState>(Json(GameEngine.Start(game.Context, game.Party)), JsonDefaults.FileOptions)!;
        var state = game.History.Aggregate(start, (current, command) =>
            JsonSerializer.Deserialize<GameState>(Json(GameEngine.Apply(current, command, game.Context).Value), JsonDefaults.FileOptions)!);
        return Json(state);
    }

    private static ScenarioAction Do(string action, params (string Key, object Value)[] extra) =>
        new() { Do = action, Extra = extra.ToDictionary(e => e.Key, e => JsonSerializer.SerializeToElement(e.Value)) };

    private static ScenarioAction Join(string name) => Do("joinParty", ("adventurer", name));

    private static Node Scene(string id, string next, params ScenarioAction[] mechanics) =>
        new() { Id = id, Type = "scene", Mechanics = mechanics, Next = next };

    private static Node Fight(string id, string next = "@end") => Read<Node>($$"""
        {"id":"{{id}}","type":"encounter","lootLevel":"common",
         "setup":{"combatants":[{"family":"imp"},{"family":"imp"},{"family":"imp"}]},
         "endConditions":[{"id":"{{id}}-win","kind":"win","next":"{{next}}"},{"id":"{{id}}-lose","kind":"lose"}]}
        """);

    private static Catalog Cards(params string[] tags) => new([new Pack
    {
        Manifest = new PackManifest { Id = "hb", Name = new Dictionary<string, string> { ["en"] = "Homebrew" } },
        Rules = Read<PackRules>("""
            {"lootDecks":[{"lootLevel":"common","cards":[{"kind":"gold","value":5,"count":10}]},
                          {"lootLevel":"uncommon","cards":[{"kind":"gold","value":5,"count":3},{"kind":"gold","value":6,"count":7}]}]}
            """),
        Combatants =
        [
            Read<Combatant>($$$"""
                {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                 "initiativeCards":["hb:initiative/imp"],"stats":{"health":4},"tags":{{{JsonSerializer.Serialize(tags)}}},"monsterLoot":["{{{Shell}}}"]}
                """),
        ],
        Items =
        [
            Read<Item>($$$"""{"id":"{{{Shell}}}","name":{"en":"Shell"},"type":"relic","tier":"common"}"""),
            Read<Item>("""{"id":"hb:item/plate","name":{"en":"Plate"},"type":"armor","tier":"common","stats":{"health":1}}"""),
        ],
    }]);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)!;

    private static EngineDriver Game(Catalog catalog, string[] party, params Node[] nodes) => new(
        new Scenario { Id = "hb:scenario/rules", Title = new Dictionary<string, string> { ["en"] = "Rules" }, Start = nodes[0].Id, Nodes = nodes },
        catalog, party);
}
