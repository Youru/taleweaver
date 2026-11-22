using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.UnitTests.Features;

/// <summary>The player's view (room-spec §8): what the GM alone may read never reaches a phone.</summary>
public class PlayerViewProjectorTests
{
    [Fact]
    public void TheLog_KeepsOnlyWhatTheTableHears()
    {
        var state = new GameState
        {
            Scenario = "hb:scenario/test",
            Campaign = new CampaignState(),
            Log =
            [
                new LogEntry("scene", new Dictionary<string, string> { ["en"] = "Read aloud" }),
                new LogEntry("hiddenText", new Dictionary<string, string> { ["en"] = "Secret" }),
                new LogEntry("aiInstruction"),
                new LogEntry("setVariable"),
                new LogEntry("answer"),
                new LogEntry("possibleSpawn"),
                new LogEntry("damage"),
            ],
        };

        var view = PlayerViewProjector.Project("game", 1, state, null, []);

        view.Log.Select(l => (l.Index, l.Entry.Code)).Should().Equal((0, "scene"), (6, "damage"));
    }

    [Fact]
    public void TheEncounter_ShowsTheTrackAndTheFigures_NotTheAi()
    {
        var state = new GameState
        {
            Scenario = "hb:scenario/test",
            Campaign = new CampaignState(),
            Encounter = new EncounterState
            {
                Node = "fight",
                Round = 2,
                InitiativeTrack = ["um1:initiative/fragor", "adventurer:Rook"],
                CurrentTurn = 0,
                PlayedCards = ["um1:initiative/fragor"],
                Combatants = [new CombatantState { Instance = "fragor-1", Card = "um1:combatant/fragor", Family = "fragor", Damage = 3, Effects = ["poison"] }],
                Activation = new Activation { InitiativeCard = "um1:initiative/fragor", Figures = ["fragor-1"], Step = 2 },
                Adventurers = [new AdventurerFigure { Name = "Rook", Effects = ["darkness"] }],
            },
        };

        var encounter = PlayerViewProjector.Project("game", 1, state, null, ["Rook"]).Encounter!;

        encounter.InitiativeTrack.Should().Equal("um1:initiative/fragor", "adventurer:Rook");
        encounter.Combatants.Single().Should().BeEquivalentTo(new { Instance = "fragor-1", Damage = 3, Effects = new[] { "poison" } });
        encounter.Activation!.Figures.Should().Equal("fragor-1");
        encounter.AdventurerEffects["Rook"].Should().Equal("darkness");
    }

    [Fact]
    public void AnEncountersTexts_StayWithTheGm_ThoseBetweenEncountersAreRead()
    {
        var state = new GameState
        {
            Scenario = "hb:scenario/test",
            Campaign = new CampaignState(),
            Log = [new LogEntry("text"), new LogEntry("encounter"), new LogEntry("text"), new LogEntry("encounterEnd"), new LogEntry("text")],
        };

        PlayerViewProjector.Project("game", 1, state, null, []).Log.Select(l => l.Index).Should().Equal(0, 1, 3, 4);
    }

    [Fact]
    public void UnderTheFog_OnlyTheShapeOfTheTable_IsSent()
    {
        var tile = new Tile { Id = "hb:tile/secret", Name = "Secret" };
        var catalog = new Catalog([new Pack { Manifest = new PackManifest { Id = "hb", Name = new Dictionary<string, string> { ["en"] = "hb" } }, Tiles = [tile] }]);
        var scenario = new Scenario
        {
            Id = "hb:scenario/test",
            Title = new Dictionary<string, string> { ["en"] = "Test" },
            Start = "fight",
            Nodes =
            [
                new Node
                {
                    Id = "fight",
                    Type = Node.EncounterType,
                    Map = new BoardMap
                    {
                        Tiles = [new MapTile("hb:tile/secret", 6, 0, 90, "lair")],
                        Areas = [new FogArea("lair", false, new Dictionary<string, string> { ["en"] = "The Queen's lair" })],
                        Zones = [new Zone("queen-throne", "objective", new Rect(7, 1))],
                    },
                },
            ],
        };
        var state = new GameState
        {
            Scenario = scenario.Id,
            Campaign = new CampaignState(),
            Encounter = new EncounterState
            {
                Node = "fight",
                Combatants = [new CombatantState { Instance = "queen", Card = "hb:combatant/queen", Family = "queen", Position = new Cell(8, 2) }],
                Tokens = [new BoardToken { Id = "obj-queen-egg", Key = "objective:red", Area = new Rect(9, 3) }],
            },
        };

        var encounter = PlayerViewProjector.Project("game", 1, state, null, [], catalog: catalog, scenario: scenario).Encounter!;

        encounter.Combatants.Should().BeEmpty();
        encounter.Board!.Tiles.Should().Equal(new MapTile(PlayerBoard.Fog, 6, 0, null, PlayerBoard.Fog));
        encounter.Board.Zones.Should().BeEmpty();
        encounter.Board.Tokens.Should().BeEmpty();
        encounter.Board.Figures.Should().BeEmpty();

        state.Encounter.RevealedAreas.Add("lair");
        var revealed = PlayerViewProjector.Project("game", 1, state, null, [], catalog: catalog, scenario: scenario).Encounter!;
        revealed.Combatants.Should().ContainSingle();
        revealed.Board!.Tokens.Single().Id.Should().Be("t0", "the scenario's ids tell what is coming");
        revealed.Board.Zones.Single().Id.Should().Be("z0");
    }

    [Fact]
    public void TheStore_ShowsItsCardsAtTheirPrice_OnlyDuringTheShopPhase()
    {
        Item Card(string id, string tier, int? cost, bool unique = false) =>
            new() { Id = id, Name = new Dictionary<string, string> { ["en"] = id }, Type = "weapon", Tier = tier, Cost = cost, Unique = unique };
        var catalog = new Catalog([new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = new Dictionary<string, string> { ["en"] = "hb" } },
            Items = [Card("hb:item/sword", "mundane", 40), Card("hb:item/axe", "rare", 90), Card("hb:item/odd", "mundane", null), Card("hb:item/crown", "rare", 200, unique: true)],
        }]);
        var scenario = new Scenario
        {
            Id = "hb:scenario/test",
            Title = new Dictionary<string, string> { ["en"] = "Test" },
            Start = "town",
            Nodes = [new Node { Id = "town", Type = "storyRound", Store = new Store { Items = [new StoreFilter { Tiers = ["mundane"] }] } }],
        };
        var state = new GameState
        {
            Scenario = scenario.Id,
            Campaign = new CampaignState { StoryRound = new StoryRoundState { Node = "town" }, StoreUniques = new() { ["town"] = ["hb:item/crown"] } },
        };

        PlayerShop.Of(state, scenario, catalog).Should().Equal(new ShopItem("hb:item/crown", 200), new ShopItem("hb:item/sword", 40));

        state.Campaign.StoryRound.Phase = StoryRoundPhases.Explore;
        PlayerShop.Of(state, scenario, catalog).Should().BeNull();
    }
}
