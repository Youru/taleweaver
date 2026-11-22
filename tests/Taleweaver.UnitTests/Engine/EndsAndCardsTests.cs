using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>End conditions and cards corrected by the play review (docs/design/play-spec.md, lot 1c).</summary>
public class EndsAndCardsTests
{
    private const string Escort = """{"id":"escort","kind":"lose"}""";
    private const string AllDown = """{"id":"all","kind":"lose","builtIn":"allDefeated"}""";
    private const string TimeOut = """{"id":"time","kind":"lose","builtIn":"urgency4"}""";
    private const string Win = """{"id":"win","kind":"win"}""";

    [Fact]
    public void PartyDefeated_TakesTheMarkedEnd_NotTheFirstListed()
    {
        var game = Game(Fight([Escort, TimeOut, AllDown]));

        game.Do(GameCommand.Damage("adventurer:A", 12));

        EndOf(game).Should().Be("all");
    }

    [Fact]
    public void PartyDefeated_SeveralUnmarked_AsksTheGm()
    {
        var game = Game(Fight([Escort, """{"id":"other","kind":"lose"}"""]));

        game.Do(GameCommand.Damage("adventurer:A", 12));
        game.Encounter.Ended.Should().BeFalse();
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Choice);

        game.Answer("1");
        EndOf(game).Should().Be("other");
        Replayed(game).Should().Be(Json(game.State));
    }

    [Fact]
    public void PartyDefeated_LeavesTheUrgencyEndAside()
    {
        var game = Game(Fight([TimeOut, Escort]));

        game.Do(GameCommand.Damage("adventurer:A", 12));

        EndOf(game).Should().Be("escort");
    }

    [Fact]
    public void LastUrgency_TakesItsOwnEnd()
    {
        var game = Game(Fight([Escort, AllDown, TimeOut], urgency: 4));

        game.Do(GameCommand.NextTurn());

        EndOf(game).Should().Be("time");
    }

    [Fact]
    public void LastUrgency_WithoutItsOwnEnd_IsEveryoneDefeated()
    {
        var game = Game(Fight([Escort, AllDown], urgency: 4));

        game.Do(GameCommand.NextTurn());

        EndOf(game).Should().Be("all");
    }

    [Fact]
    public void UrgencyAtTheRoundsEnd_AskingWhichDefeat_StartsNoRound()
    {
        var game = Game(Fight([Escort, """{"id":"other","kind":"lose"}"""], imps: 0));

        for (var i = 0; i < 4; i++)
        {
            game.PlayRound();
        }

        game.Do(GameCommand.NextTurn());

        game.State.Prompt!.Kind.Should().Be(PromptKinds.Choice);
        game.Encounter.Round.Should().Be(4);
    }

    [Fact]
    public void EndThatContinues_GivesItsReward_AndAddsAnEnd()
    {
        var surprise = """
            {"id":"surprise","kind":"win","continues":true,"reward":{"en":"More come."},
             "mechanics":[{"do":"gainGold","amount":5},{"do":"addEndCondition","endCondition":"win"}]}
            """;
        var game = Game(Fight([surprise, """{"id":"win","kind":"win","active":false}""", AllDown]));

        game.Do(GameCommand.ReachEndCondition("surprise")).Continue();

        game.Encounter.Ended.Should().BeFalse();
        game.State.Campaign.Gold.Should().Be(5);
        game.Encounter.ActiveEndConditions.Should().BeEquivalentTo("win", "all");
        Replayed(game).Should().Be(Json(game.State));

        game.Do(GameCommand.ReachEndCondition("win"));
        EndOf(game).Should().Be("win");
    }

    [Fact]
    public void Restore_Unflips_ButKeepsTheSideOfDoubleItems()
    {
        var game = Game(Scene("""{"do":"awaitContinue"},{"do":"restore"}"""));
        foreach (var (item, slot) in new[] { ("hb:item/flip", "equipped"), ("hb:item/double", "equipped"), ("hb:item/flip", "pack") })
        {
            game.Do(GameCommand.AddItem(item, "A", slot));
        }

        game.Do(GameCommand.AddItem("hb:item/flip", "stash"));
        foreach (var id in Owned(game).Select(o => o.Id).ToList())
        {
            game.Do(GameCommand.FlipItem(id, true));
        }

        game.Continue();

        Owned(game).Where(o => o.Flipped).Should().ContainSingle().Which.Item.Should().Be("hb:item/double");
    }

    [Fact]
    public void Health_CountsDisciplines()
    {
        var game = Game(Scene("""
            {"do":"gainDiscipline","ref":"hb:discipline/mark","adventurer":"A"},
            {"do":"gainDiscipline","ref":"hb:discipline/body","adventurer":"A"},
            {"do":"gainDiscipline","ref":"hb:discipline/master","adventurer":"A"}
            """));

        game.State.Campaign.Party[0].Health.Should().Be(12 + 2 + 3);
    }

    [Fact]
    public void Health_CountsUpgradesOfEquippedItemsOnly()
    {
        var game = Game(Scene("""{"do":"awaitContinue"}"""));
        game.Do(GameCommand.AddItem("hb:item/flip", "A", "equipped")).Do(GameCommand.AddItem("hb:item/flip", "A", "pack"));
        foreach (var id in Owned(game).Select(o => o.Id).ToList())
        {
            game.Do(GameCommand.AttachUpgrade(id, "hb:item-upgrade/vital", 0));
        }

        game.State.Campaign.Party[0].Health.Should().Be(14);
    }

    [Fact]
    public void GainItem_GoesToWhoIsNamed()
    {
        var game = Game(Scene("""{"do":"gainItem","ref":"hb:item/flip","who":"A"}"""));

        game.State.Campaign.Party[0].Pack.Should().ContainSingle();
        game.State.Campaign.Unassigned.Should().BeEmpty();
    }

    [Fact]
    public void GainItem_AskAdventurer_AsksThenGivesIt()
    {
        var game = Game(Scene("""{"do":"gainItem","ref":"hb:item/flip","who":"askAdventurer"}"""));

        game.State.Prompt!.Kind.Should().Be(PromptKinds.Choice);
        game.Answer("0");

        game.State.Campaign.Party[0].Pack.Should().ContainSingle();
    }

    [Theory]
    [InlineData("", 9)]
    [InlineData(""","spawnedBy":"boss" """, 10)]
    public void Loot_OfASpawnedOpponent_DependsOnWhoSpawnedIt(string spawnedBy, int left)
    {
        var node = Fight([Win, AllDown], imps: 0, start: $$"""{"do":"spawn","family":"imp","count":1{{spawnedBy}}}""");
        var game = Game(node);

        game.Do(GameCommand.Defeat("imp-1"));

        game.Encounter.LootDeck.Should().HaveCount(left);
    }

    private static string EndOf(EngineDriver game) => game.State.Log.Last(l => l.Code == "encounterEnd").Args!["endCondition"];

    private static IEnumerable<OwnedItem> Owned(EngineDriver game) =>
        game.State.Campaign.Party.SelectMany(a => a.Equipped.Concat(a.Pack)).Concat(game.State.Campaign.Stash).Concat(game.State.Campaign.Unassigned);

    private static string Json(GameState state) => JsonSerializer.Serialize(state, JsonDefaults.FileOptions);

    private static string Replayed(EngineDriver game)
    {
        GameState Saved(GameState state) => JsonSerializer.Deserialize<GameState>(Json(state), JsonDefaults.FileOptions)!;
        return Json(game.History.Aggregate(Saved(GameEngine.Start(game.Context, game.Party)),
            (state, command) => Saved(GameEngine.Apply(state, command, game.Context).Value)));
    }

    private static Node Scene(string mechanics) =>
        Read<Node>($$"""{"id":"node","type":"scene","mechanics":[{{mechanics}}],"next":"@end"}""");

    private static Node Fight(string[] ends, int imps = 1, int urgency = 0, string? start = null)
    {
        var placed = string.Join(',', Enumerable.Repeat("""{"family":"imp"}""", imps));
        var events = new List<string>();
        if (urgency > 0)
        {
            events.Add($$$"""{"id":"late","trigger":{"on":"roundStart"},"actions":[{"do":"addUrgency","amount":{{{urgency}}}}]}""");
        }

        if (start is not null)
        {
            events.Add($$$"""{"id":"start","trigger":{"on":"encounterStart"},"actions":[{{{start}}}]}""");
        }

        return Read<Node>($$$"""
            {"id":"node","type":"encounter","lootLevel":"common","setup":{"combatants":[{{{placed}}}]},
             "events":[{{{string.Join(',', events)}}}],"endConditions":[{{{string.Join(',', ends)}}}]}
            """);
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)!;

    private static EngineDriver Game(Node node)
    {
        var name = new Dictionary<string, string> { ["en"] = "Cards" };
        var pack = new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = name },
            Rules = Read<PackRules>("""{"lootDecks":[{"lootLevel":"common","cards":[{"kind":"gold","value":5,"count":10}]}]}"""),
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":4}}
                    """),
            ],
            Items =
            [
                Read<Item>("""{"id":"hb:item/flip","name":{"en":"Flip"},"type":"core","tier":"common","tags":["FLIP"]}"""),
                Read<Item>("""{"id":"hb:item/double","name":{"en":"Double"},"type":"weapon","tier":"common","tags":["DOUBLE"]}"""),
            ],
            ItemUpgrades = [Read<ItemUpgrade>("""{"id":"hb:item-upgrade/vital","name":{"en":"Vital"},"tags":["UNIVERSAL UPGRADE"],"health":2}""")],
            Disciplines =
            [
                Read<Discipline>("""{"id":"hb:discipline/mark","name":{"en":"Mark"},"tree":"sanctus","level":1,"health":2}"""),
                Read<Discipline>("""{"id":"hb:discipline/body","name":{"en":"Body"},"tree":"martial","level":2,"healthPerHighestLevel":"martial"}"""),
                Read<Discipline>("""{"id":"hb:discipline/master","name":{"en":"Master"},"tree":"martial","level":3}"""),
            ],
        };
        return new EngineDriver(new Scenario { Id = "hb:scenario/cards", Title = name, Start = node.Id, Nodes = [node] }, new Catalog([pack]), "A");
    }
}
