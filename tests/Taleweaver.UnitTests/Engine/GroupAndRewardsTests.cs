using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The party, the rewards and who did what (docs/design/play-spec.md, lot 2).</summary>
public class GroupAndRewardsTests
{
    private static readonly string[] Two = ["A", "B"];
    private static readonly string[] Five = ["A", "B", "C", "D", "E"];

    // ---- A6: who did it ----

    [Fact]
    public void A6_TokenReward_GoesToWhoActivatedIt()
    {
        var game = Game(Two, Fight("""{"id":"e","trigger":{"on":"tokenActivated","token":"loot:red"},"actions":[{"do":"gainXp","amount":2,"who":"author"}]}"""));

        game.Do(Command("""{"type":"activateToken","token":"loot:red","author":"B"}"""));

        Member(game, "B").Xp.Should().Be(2);
        Member(game, "A").Xp.Should().Be(0);
    }

    [Fact]
    public void A6_AuthorUnknown_IsAskedOnce_ForAllTheActions()
    {
        var game = Game(Two, Fight("""
            {"id":"e","trigger":{"on":"tokenActivated","token":"loot:red"},
             "actions":[{"do":"gainXp","amount":2,"who":"author"},{"do":"gainItem","ref":"hb:item/blade","who":"author"}]}
            """));

        game.Do(Command("""{"type":"activateToken","token":"loot:red"}""")).Answer("1");

        game.State.Prompt.Should().BeNull();
        Member(game, "B").Xp.Should().Be(2);
        Member(game, "B").Pack.Should().ContainSingle();
        SameWhenReplayed(game);
    }

    [Fact]
    public void A6_LootAndEvent_GoToWhoDefeatedTheOpponent()
    {
        var game = Game(Two, Fight("""{"id":"e","trigger":{"on":"combatantDefeated"},"actions":[{"do":"gainXp","amount":1,"who":"author"}]}""", imps: 1));

        game.Do(Command("""{"type":"damage","instance":"imp-1","value":4,"author":"adventurer:B"}"""));

        Member(game, "B").Xp.Should().Be(1);
        Member(game, "B").Pack.Should().ContainSingle();
        game.State.Campaign.Unassigned.Should().BeEmpty();
    }

    // ---- A20: who decides ----

    [Fact]
    public void A20_Choice_NamesWhoDecides_AndTheJournalKeepsIt()
    {
        var game = Game(Two, Scene("""{"do":"askChoice","who":"B","options":[{"label":{"en":"Yes"},"actions":[{"do":"gainXp","amount":1,"who":"author"}]}]}"""));

        Json(game.State.Prompt).GetProperty("decider").GetString().Should().Be("B");
        game.Answer("0");

        game.State.Log.Last(l => l.Code == "answer").Args!["adventurer"].Should().Be("B");
        Member(game, "B").Xp.Should().Be(1);
    }

    [Fact]
    public void A20_DeciderToAsk_IsAskedFirst()
    {
        var game = Game(Two, Scene("""{"do":"askChoice","who":"askAdventurer","options":[{"label":{"en":"Yes"}}]}"""));

        game.Answer("1");

        Json(game.State.Prompt).GetProperty("decider").GetString().Should().Be("B");
    }

    // ---- A19: exits under a condition ----

    [Theory]
    [InlineData("", 1)]
    [InlineData("""{"do":"setFlag","flag":"key"}""", 3)]
    public void A19_ExitsAndQuests_FollowTheFlags(string mechanics, int offered)
    {
        var game = Game(Two, Read<Node>($$"""
            {"id":"start","type":"storyRound","mechanics":[{{mechanics}}],
             "sideQuests":[{"node":"next","if":[{"var":"key","value":true}]}],
             "ventureForth":[{"node":"next"},{"node":"next","if":[{"var":"key","value":true}]}]}
            """));

        game.Answer("explore");

        game.State.Prompt!.Options.Should().HaveCount(offered);
    }

    // ---- A1: the Active Group ----

    [Fact]
    public void A1_GroupIsChosenAtOnce_AndAskedAgainEachTime()
    {
        var game = Game(Five, Scene("""{"do":"setActiveGroup"},{"do":"setActiveGroup"}"""));

        game.State.Prompt!.Kind.Should().Be("activeGroup");
        game.State.Prompt.Options.Should().HaveCount(5);
        game.Do(Command("""{"type":"answer","adventurers":["A","B","C","E"]}"""));

        game.State.Campaign.ActiveGroup.Should().BeEquivalentTo("A", "B", "C", "E");
        game.State.Prompt!.Kind.Should().Be("activeGroup");
        game.Do(Command("""{"type":"answer","adventurers":["A","B","C","D"]}"""));
        game.State.Campaign.ActiveGroup.Should().BeEquivalentTo("A", "B", "C", "D");
        SameWhenReplayed(game);
    }

    [Theory]
    [InlineData("""["A","B","C"]""")]
    [InlineData("""["A","B","C","D","E"]""")]
    [InlineData("""["A","A","B","C"]""")]
    public void A1_AnotherCountThanFour_IsRefused(string chosen)
    {
        var game = Game(Five, Scene("""{"do":"setActiveGroup"}"""));

        GameEngine.Apply(game.State, Command($$"""{"type":"answer","adventurers":{{chosen}}}"""), game.Context).IsSuccess.Should().BeFalse();
    }

    // ---- A2: imposed participants ----

    [Fact]
    public void A2_ImposedParticipants_PlayWithoutTouchingTheGroup()
    {
        var game = Game(Five, Scene("""{"do":"setActiveGroup","adventurers":["A","B"]},{"do":"goToNode","node":"fight"}"""),
            Fight(id: "fight", extra: ""","participants":["C"]"""));

        game.Encounter.InitiativeTrack.Should().Equal("adventurer:C");
        game.State.Campaign.ActiveGroup.Should().BeEquivalentTo("A", "B");
        game.Do(GameCommand.Damage("adventurer:C", 12));
        game.Encounter.Ended.Should().BeTrue();
    }

    [Fact]
    public void A2_ImposedParticipants_CannotExchangeWithTheOthers()
    {
        var game = Game(Two, Fight(extra: ""","participants":["B"]"""));
        game.Do(GameCommand.AddItem("hb:item/blade", "A", "pack"));

        GameEngine.Apply(game.State, GameCommand.MoveItem("item-1", "B", "pack"), game.Context).IsSuccess.Should().BeFalse();
    }

    // ---- A3: an arrival ----

    [Fact]
    public void A3_Arrival_IsPlayableWithoutFixingAnythingByHand()
    {
        var game = Game(Two, Scene("""
            {"do":"joinParty","adventurer":"C","amount":4,"setup":true,"gold":7,"items":["hb:item/blade"],"disciplines":["hb:discipline/mark"],
             "actions":[{"do":"gainItem","ref":"hb:item/charm","who":"author"}]}
            """));

        var member = Member(game, "C");
        member.Xp.Should().Be(4);
        member.Equipped.Should().ContainSingle();
        member.Pack.Should().ContainSingle();
        member.Disciplines.Should().ContainSingle();
        game.State.Campaign.Gold.Should().Be(7);
        game.State.Prompt!.Kind.Should().Be("arrival");
        game.State.Prompt.Instance.Should().Be("C");
        game.Answer("continue");
        game.State.Prompt!.Kind.Should().Be("continue");
        SameWhenReplayed(game);
    }

    [Fact]
    public void A3_FifthArrival_LeadsToTheChoiceOfTheGroup()
    {
        var game = Game(["A", "B", "C", "D"], Scene("""{"do":"joinParty","adventurer":"E","setup":true}"""));

        game.Answer("continue");

        game.State.Prompt!.Kind.Should().Be("activeGroup");
    }

    // ---- A4: rewards ----

    [Fact]
    public void A4_RandomConsumable_IsOfTheTierOrBelow_AndStableOnReplay()
    {
        var game = Game(Two, Fight("""{"id":"e","trigger":{"on":"encounterStart"},"actions":[{"do":"gainItem","pick":"random","type":"consumable","count":8,"who":"A"}]}"""));

        var drawn = Member(game, "A").Pack.Select(o => o.Item).ToList();
        drawn.Should().HaveCount(8);
        drawn.Should().OnlyContain(id => id == "hb:item/tonic" || id == "hb:item/salve");
        SameWhenReplayed(game);
    }

    [Fact]
    public void A4_Choice_OffersTheFilteredList()
    {
        var game = Game(Two, Scene("""{"do":"gainItem","pick":"choice","type":"consumable","tiers":["common","mundane"],"who":"A"}"""));

        game.State.Prompt!.Options.Should().HaveCount(2);
        game.Answer("0");

        Member(game, "A").Pack.Should().ContainSingle().Which.Item.Should().NotBeNull();
    }

    [Fact]
    public void A4_CardAlreadyTaken_FallsBack()
    {
        var game = Game(Two, Scene("""
            {"do":"gainItem","ref":"hb:item/relic"},
            {"do":"gainItem","ref":"hb:item/relic","fallback":[{"do":"gainGold","amount":9}]}
            """));

        game.State.Campaign.Unassigned.Should().ContainSingle();
        game.State.Campaign.Gold.Should().Be(9);
    }

    [Theory]
    [InlineData("", 2, 0)]
    [InlineData(""","fallback":[{"do":"gainGold","amount":9}]""", 1, 9)]
    public void A4_AStoryCardOfUnknownCopies_IsOnlyLimitedWithAFallback(string fallback, int held, int gold)
    {
        var game = Game(Two, Scene($$"""{"do":"gainItem","ref":"hb:item/token"},{"do":"gainItem","ref":"hb:item/token"{{fallback}}}"""));

        game.State.Campaign.Unassigned.Should().HaveCount(held);
        game.State.Campaign.Gold.Should().Be(gold);
    }

    [Fact]
    public void A4_EachButOne()
    {
        var game = Game(["A", "B", "C"], Scene("""
            {"do":"gainXp","amount":1,"each":true,"except":["B"]},
            {"do":"gainItem","ref":"hb:item/blade","each":true,"except":["B"]}
            """));

        game.State.Campaign.Party.Select(a => a.Xp).Should().Equal(1, 0, 1);
        game.State.Campaign.Party.Select(a => a.Pack.Count).Should().Equal(1, 0, 1);
    }

    // ---- A5: Skill Checks ----

    [Theory]
    [InlineData(10, 1, 0)]
    [InlineData(9, 0, 1)]
    public void A5_TotalTyped_RunsPassOrFail_ForWhoTested(int total, int passed, int failed)
    {
        var game = Game(Two, Scene("""
            {"do":"askSkillCheck","options":[{"skill":"lore","difficulty":10}],
             "pass":[{"do":"gainXp","amount":1,"who":"author"}],"fail":[{"do":"gainGold","amount":1}]}
            """));

        game.Do(Command($$"""{"type":"answer","option":"total-0","value":{{total}},"instance":"B"}"""));

        Member(game, "B").Xp.Should().Be(passed);
        game.State.Campaign.Gold.Should().Be(failed);
    }

    [Fact]
    public void A5_ShowsWhoTests_AndWhatToAdd()
    {
        var game = Game(Two, Scene("""{"do":"askSkillCheck","who":"A","options":[{"skill":"lore","difficulty":10}]}"""));

        var tester = Json(game.State.Prompt).GetProperty("testers")[0];
        tester.GetProperty("name").GetString().Should().Be("A");
        tester.GetProperty("skills").GetProperty("lore").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("combined", 8, 12, 5)]
    [InlineData("combined", 8, 11, 0)]
    [InlineData("collective", 10, 12, 5)]
    [InlineData("collective", 10, 9, 0)]
    public void A5_CombinedAndCollective(string mode, int first, int second, int gold)
    {
        var difficulty = mode == "combined" ? 20 : 10;
        var game = Game(Two, Scene($$"""
            {"do":"askSkillCheck","mode":"{{mode}}","who":"each","options":[{"skill":"lore","difficulty":{{difficulty}}}],"pass":[{"do":"gainGold","amount":5}]}
            """));

        game.Do(Command($$"""{"type":"answer","option":"total-0","value":{{first}}}"""));
        game.Do(Command($$"""{"type":"answer","option":"total-0","value":{{second}}}"""));

        game.State.Campaign.Gold.Should().Be(gold);
        SameWhenReplayed(game);
    }

    [Fact]
    public void A5_Formula_GoldIsTheResultTimesN()
    {
        var game = Game(Two, Scene("""{"do":"askSkillCheck","who":"A","goldPerPoint":3,"options":[{"skill":"lore"}]}"""));

        game.Do(Command("""{"type":"answer","option":"total-0","value":11}"""));

        game.State.Campaign.Gold.Should().Be(33);
    }

    // ---- A7: carried ----

    [Fact]
    public void A7_DefeatedCarrier_PutsItOnTheirSquare_WhereItIsPickedUp()
    {
        var game = Game(Two, Fight("""{"id":"e","trigger":{"on":"encounterStart"},"actions":[{"do":"carry","id":"idol","who":"A"}]}"""));
        game.Do(GameCommand.Move("adventurer:A", new Cell(3, 4))).Do(GameCommand.Damage("adventurer:A", 12));

        var idol = Json(game.State.Campaign).GetProperty("carried")[0];
        idol.TryGetProperty("carrier", out _).Should().BeFalse();
        idol.GetProperty("position").GetProperty("x").GetInt32().Should().Be(3);

        game.Do(Command("""{"type":"carry","item":"idol","instance":"adventurer:B"}"""));
        Json(game.State.Campaign).GetProperty("carried")[0].GetProperty("carrier").GetString().Should().Be("adventurer:B");
        SameWhenReplayed(game);
    }

    [Fact]
    public void A7_LostOnDangerousTerrain()
    {
        var game = Game(Two, Fight("""{"id":"e","trigger":{"on":"encounterStart"},"actions":[{"do":"carry","id":"idol","who":"A"}]}""",
            extra: ""","map":{"tokens":[{"id":"pit","type":"hb:token/terrain/dangerous","x":3,"y":4}]}"""));

        game.Do(GameCommand.Move("adventurer:A", new Cell(3, 4))).Do(GameCommand.Damage("adventurer:A", 12));

        Json(game.State.Campaign).GetProperty("carried")[0].GetProperty("lost").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"do":"carry","id":"idol","who":"A"}""", 0)]
    [InlineData("""{"do":"carry","id":"idol","who":"A","keep":true}""", 1)]
    public void A7_OnlyWhatIsKept_GoesToTheNextEncounter(string carry, int kept)
    {
        var game = Game(Two,
            Fight($$"""{"id":"e","trigger":{"on":"encounterStart"},"actions":[{{carry}}]}""", next: "second"),
            Fight(id: "second"));

        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        var campaign = Json(game.State.Campaign);
        var count = campaign.TryGetProperty("carried", out var carried) ? carried.GetArrayLength() : 0;
        count.Should().Be(kept);
    }

    // ---- Helpers ----

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

    private static Node Scene(string mechanics) =>
        Read<Node>($$"""{"id":"start","type":"scene","mechanics":[{{mechanics}}],"next":"next"}""");

    private static Node Fight(string events = "", int imps = 0, string id = "start", string extra = "", string next = "next")
    {
        var setup = string.Join(',', Enumerable.Repeat("""{"family":"imp"}""", imps));
        return Read<Node>($$$"""
            {"id":"{{{id}}}","type":"encounter","lootLevel":"common","setup":{"combatants":[{{{setup}}}]},
             "tokens":{"loot:red":{}},"events":[{{{events}}}],
             "endConditions":[{"id":"win","kind":"win","next":"{{{next}}}"},{"id":"lose","kind":"lose"}]{{{extra}}}}
            """);
    }

    private static EngineDriver Game(string[] party, params Node[] nodes)
    {
        var name = new Dictionary<string, string> { ["en"] = "Group" };
        var pack = new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = name },
            Rules = Read<PackRules>("""{"lootDecks":[{"lootLevel":"common","cards":[{"kind":"item","count":5}]}]}"""),
            Adventurers = [Read<Adventurer>("""{"id":"hb:adventurer/a","name":{"en":"A"},"skills":{"lore":3}}""")],
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":4}}
                    """),
            ],
            Items =
            [
                Read<Item>("""{"id":"hb:item/blade","name":{"en":"Blade"},"type":"weapon","tier":"common"}"""),
                Read<Item>("""{"id":"hb:item/charm","name":{"en":"Charm"},"type":"accessory","tier":"common"}"""),
                Read<Item>("""{"id":"hb:item/relic","name":{"en":"Relic"},"type":"relic","tier":"common","unique":true}"""),
                Read<Item>("""{"id":"hb:item/token","name":{"en":"Token"},"type":"relic","tier":"story","story":true}"""),
                Read<Item>("""{"id":"hb:item/tonic","name":{"en":"Tonic"},"type":"consumable","tier":"common"}"""),
                Read<Item>("""{"id":"hb:item/salve","name":{"en":"Salve"},"type":"consumable","tier":"mundane"}"""),
                Read<Item>("""{"id":"hb:item/elixir","name":{"en":"Elixir"},"type":"consumable","tier":"rare"}"""),
            ],
            Disciplines = [Read<Discipline>("""{"id":"hb:discipline/mark","name":{"en":"Mark"},"tree":"sanctus","level":1}""")],
        };
        var scenario = new Scenario
        {
            Id = "hb:scenario/group",
            Title = name,
            Start = "start",
            Nodes = [.. nodes, Read<Node>("""{"id":"next","type":"scene","next":"@end"}""")],
        };
        return new EngineDriver(scenario, new Catalog([pack]), party);
    }
}
