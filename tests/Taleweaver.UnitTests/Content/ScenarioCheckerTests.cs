using System.Text.Json;
using Taleweaver.Api.Content;

namespace Taleweaver.UnitTests.Content;

public class ScenarioCheckerTests
{
    private static readonly Catalog Catalog = new(
    [
        new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = new Dictionary<string, string> { ["en"] = "Homebrew" } },
            Combatants =
            [
                new Combatant
                {
                    Id = "hb:combatant/brute-common", Family = "brute", Kind = "intelligent", LootLevel = "common", Activation = "1-2",
                    Name = new Dictionary<string, string> { ["en"] = "Brute" },
                },
                new Combatant
                {
                    Id = "hb:combatant/queen-common", Family = "queen", Kind = "intelligent", LootLevel = "common", Source = "hidden-cards-and-figures/queen",
                    Name = new Dictionary<string, string> { ["en"] = "Queen" },
                },
            ],
            Tokens =
            [
                new TokenType { Id = "hb:token/objective", Category = "objective", Name = new Dictionary<string, string> { ["en"] = "Objective" } },
                new TokenType { Id = "hb:token/terrain/obstructing-long", Category = "terrain", Name = new Dictionary<string, string> { ["en"] = "Obstructing" } },
            ],
            Tiles =
            [
                new Tile { Id = "hb:tile/t1", Name = "T1", Theme = "forest", PairedSide = "hb:tile/t2" },
                new Tile { Id = "hb:tile/t2", Name = "T2", Theme = "crypt", PairedSide = "hb:tile/t1" },
                new Tile { Id = "hb:tile/t3", Name = "T3", Theme = "marsh" },
            ],
        },
    ]);

    [Fact]
    public async Task Check_BlightOfRhamsted_SameResultAsCheckScenarioPy()
    {
        var scenario = await RepositoryContent.BlightAsync();
        var catalog = await RepositoryContent.CatalogAsync();

        var report = ScenarioChecker.Check(scenario, catalog);

        report.Errors.Should().BeEmpty();
        report.Warnings.Should().BeEquivalentTo(
        [
            new ScenarioIssue("Scenarios.BeyondCards", "search-for-the-lair",
                "6 × fragor planned (setup and spawns), but 3 numbered card(s) at level common: beyond that, a spawn fails while none is defeated"),
            new ScenarioIssue("Scenarios.BeyondCards", "blight-found",
                "4 × animate planned (setup and spawns), but 2 numbered card(s) at level mundane: beyond that, a spawn fails while none is defeated"),
            new ScenarioIssue("Scenarios.TokenNeverPlaced", "blight-found", "token 'loot:blue' is described but never placed"),
        ]);
        report.Cards.Select(c => $"{c.Encounter} : {c.Family} → {c.CombatantId}").Should().Equal(
            "blight-found : animate → um1:combatant/animate-1-2-mundane",
            "crossing-the-river : cave-sickle → um1:combatant/cave-sickle-1-3-common",
            "crossing-the-river : earth-loa → um1:combatant/earth-loa-1-2-common",
            "crossing-the-river : fragor → um1:combatant/fragor-1-3-common",
            "crossing-the-river : water-loa → um1:combatant/crystal-water-loa-1-2-common",
            "search-for-the-lair : earth-loa → um1:combatant/earth-loa-1-2-common",
            "search-for-the-lair : fragor → um1:combatant/fragor-1-3-common",
            "search-for-the-lair : gevaudan → um1:combatant/grizzled-gevaudan-1-2-common",
            "search-for-the-lair : water-loa → um1:combatant/crystal-water-loa-1-2-common");
    }

    [Fact]
    public void Check_ValidEncounter_ReportsNoIssue()
    {
        var report = ScenarioChecker.Check(Scenario(Encounter()), Catalog);

        report.IsValid.Should().BeTrue();
        report.Warnings.Should().BeEmpty();
        report.Cards.Should().ContainSingle().Which.CombatantId.Should().Be("hb:combatant/brute-common");
    }

    [Fact]
    public void Check_DuplicateNodeAndUnknownNext_ReportsBoth()
    {
        var scenario = Scenario(new Node { Id = "fight", Type = "scene", Next = "nowhere" }, Encounter());

        var report = ScenarioChecker.Check(scenario, Catalog);

        report.Errors.Select(e => e.Code).Should().BeEquivalentTo(["Scenarios.DuplicateNode", "Scenarios.UnknownNext"]);
    }

    [Fact]
    public void Check_UnknownStart_ReportsError()
    {
        var scenario = Scenario(Encounter()) with { Start = "missing" };

        var report = ScenarioChecker.Check(scenario, Catalog);

        report.Errors.Should().ContainSingle().Which.Code.Should().Be("Scenarios.UnknownStart");
    }

    [Fact]
    public void Check_FamilyWithoutCardAtLootLevel_ReportsError()
    {
        var report = ScenarioChecker.Check(Scenario(Encounter() with { LootLevel = "mundane" }), Catalog);

        report.Errors.Should().ContainSingle().Which.Code.Should().Be("Scenarios.NoCardForLootLevel");
    }

    [Fact]
    public void Check_SpawnOfUnknownFamily_ReportsError()
    {
        var encounter = Encounter() with
        {
            Events = [Event(new ScenarioAction { Do = "spawn", Family = "dragon", Count = 1 })],
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Should().ContainSingle().Which.Code.Should().Be("Scenarios.UnknownFamily");
    }

    [Fact]
    public void Check_NestedActionWithUnknownReferences_ReportsEachOne()
    {
        var choice = new ScenarioAction
        {
            Do = "askChoice",
            Options =
            [
                new ActionOption { Actions = [new ScenarioAction { Do = "readHiddenRule", Rule = "secret" }] },
                new ActionOption { Pass = [new ScenarioAction { Do = "placeDiagram", Diagram = "z" }] },
            ],
            Fail = [new ScenarioAction { Do = "endEncounter", EndCondition = "lose" }],
        };

        var report = ScenarioChecker.Check(Scenario(Encounter() with { Events = [Event(choice)] }), Catalog);

        report.Errors.Select(e => e.Code).Should().BeEquivalentTo(
            ["Scenarios.UnknownHiddenRule", "Scenarios.UnknownDiagram", "Scenarios.UnknownEndCondition"]);
    }

    [Fact]
    public void Check_ChoiceGoingToAnUnknownNode_ReportsUnknownNext()
    {
        var scene = new Node
        {
            Id = "intro",
            Type = "scene",
            Mechanics =
            [
                new ScenarioAction
                {
                    Do = "askChoice",
                    Options = [new ActionOption { Actions = [new ScenarioAction { Do = "goToNode", Node = "nowhere" }] }],
                },
            ],
        };

        var report = ScenarioChecker.Check(Scenario(Encounter(), scene), Catalog);

        report.Errors.Should().ContainSingle().Which.Should().Be(new ScenarioIssue("Scenarios.UnknownNext", "intro", "unknown next node 'nowhere'"));
    }

    [Fact]
    public void Check_TokenOffTheTiles_ReportsError()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tokens = [new MapToken { Id = "far", Type = "hb:token/objective", Color = "red", X = 5, Y = 0, W = 2 }] },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Should().ContainSingle().Which.Message.Should().Be("token 'far' is off the tiles (6, 0)");
    }

    [Fact]
    public void Check_FigureOnObstructingTerrain_ReportsWarning()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tokens = [new MapToken { Id = "rock", Type = "hb:token/terrain/obstructing-long", X = 1, Y = 1, W = 2 }] },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Warnings.Should().ContainSingle().Which.Code.Should().Be("Scenarios.OnBlockingTerrain");
    }

    [Fact]
    public void Check_TokenDescribedButNeverPlaced_ReportsWarning()
    {
        var encounter = Encounter() with
        {
            Tokens = new Dictionary<string, TokenRule> { ["objective:blue"] = new() { Mode = "adjacent" } },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Warnings.Should().ContainSingle().Which.Code.Should().Be("Scenarios.TokenNeverPlaced");
    }

    [Fact]
    public void Check_ReferenceMissingFromCatalogue_ReportsWarning()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tokens = [new MapToken { Id = "o", Type = "hb:token/beacon", X = 0, Y = 0 }] },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Warnings.Should().ContainSingle().Which.Message.Should().Be("not in the catalogue: 'hb:token/beacon'");
    }

    [Fact]
    public void Check_ActionTriggerAndModeTheEngineDoesNotKnow_ReportsWarnings()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tokens = [new MapToken { Id = "o", Type = "hb:token/objective", Color = "red", X = 0, Y = 0 }] },
            Tokens = new Dictionary<string, TokenRule> { ["objective:red"] = new() { Mode = "lineOfSight" } },
            Events = [Event(new ScenarioAction { Do = "warp" }) with { Trigger = new EventTrigger { On = "turnStart" } }],
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Should().BeEmpty();
        report.Warnings.Select(w => w.Code).Should().BeEquivalentTo(
            ["Scenarios.UnknownTokenMode", "Scenarios.UnknownTrigger", "Scenarios.UnknownAction"]);
    }

    [Fact]
    public void Check_TrueOrFalseVariableComparedAsANumber_ReportsBadCondition()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tokens = [new MapToken { Id = "o", Type = "hb:token/objective", Color = "red", X = 0, Y = 0 }] },
            Tokens = new Dictionary<string, TokenRule> { ["objective:red"] = new() },
            Events =
            [
                Event(new ScenarioAction { Do = "showText" }) with
                {
                    Conditions =
                    [
                        new EventCondition { Var = "activated.objective:red", Op = ">=", Value = JsonSerializer.SerializeToElement(1) },
                        new EventCondition { Not = new EventCondition { Var = "guide", Op = "==", Value = JsonSerializer.SerializeToElement(true) } },
                        new EventCondition { Var = "guid", Op = "==", Value = JsonSerializer.SerializeToElement(true) },
                    ],
                },
            ],
        };
        var scenario = Scenario(encounter) with { Variables = [JsonSerializer.SerializeToElement(new { id = "guide", type = "flag" })] };

        var report = ScenarioChecker.Check(scenario, Catalog);

        report.Errors.Should().ContainSingle().Which.Code.Should().Be("Scenarios.BadCondition");
        report.Warnings.Should().ContainSingle().Which.Message.Should().Be("variable never declared nor set 'guid'");
    }

    [Fact]
    public void Check_FogAreaNotDeclared_ReportsErrors()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tiles = [new MapTile("hb:tile/t1", 0, 0, 0, "crypt")] },
            Events = [Event(Reveal("cellar"))],
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Select(e => e.Message).Should().BeEquivalentTo(
            ["fog area not declared in map.areas 'crypt'", "revealArea: unknown fog area 'cellar'"]);
    }

    [Fact]
    public void Check_SpawnAtAZoneName_ReportsBadSpot()
    {
        var spawn = new ScenarioAction { Do = "spawn", Family = "brute", At = JsonSerializer.SerializeToElement("spawn-a") };

        var report = ScenarioChecker.Check(Scenario(Encounter() with { Events = [Event(spawn)] }), Catalog);

        report.Errors.Should().ContainSingle().Which.Code.Should().Be("Scenarios.BadSpot");
    }

    [Fact]
    public void Check_MoreFiguresThanNumberedCardsAndAHiddenCard_ReportsWarnings()
    {
        var encounter = Encounter() with
        {
            Events =
            [
                Event(new ScenarioAction { Do = "spawn", Family = "brute", Count = 2 }),
                Event(new ScenarioAction { Do = "spawn", Family = "queen" }) with { Id = "e2" },
            ],
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Should().BeEmpty();
        report.Warnings.Select(w => w.Code).Should().BeEquivalentTo(["Scenarios.BeyondCards", "Scenarios.HiddenCard"]);
    }

    [Fact]
    public void Check_BothSidesOfATileAndOneTileTwice_ReportsErrors()
    {
        var encounter = Encounter() with
        {
            Map = Board() with
            {
                Tiles = [new MapTile("hb:tile/t1", 0, 0, 0), new MapTile("hb:tile/t2", 6, 0, 0), new MapTile("hb:tile/t3", 0, 6, 0), new MapTile("hb:tile/t3", 6, 6, 0)],
            },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Select(e => e.Code).Should().BeEquivalentTo(["Scenarios.BothSides", "Scenarios.TileTwice"]);
    }

    [Fact]
    public void Check_SceneriesThatDoNotMeetAndATileApart_ReportsWarnings()
    {
        var encounter = Encounter() with
        {
            Map = Board() with { Tiles = [new MapTile("hb:tile/t2", 0, 0, 0), new MapTile("hb:tile/t3", 12, 0, 0)] },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Errors.Should().BeEmpty();
        report.Warnings.Select(w => w.Code).Should().BeEquivalentTo(["Scenarios.ThemesApart", "Scenarios.TilesApart"]);
    }

    [Fact]
    public void Check_SceneriesOfOneGroup_ReportsNothing()
    {
        // Forest and marsh meet naturally
        var encounter = Encounter() with
        {
            Map = Board() with { Tiles = [new MapTile("hb:tile/t1", 0, 0, 0), new MapTile("hb:tile/t3", 6, 3, 0)] },
        };

        var report = ScenarioChecker.Check(Scenario(encounter), Catalog);

        report.Warnings.Should().BeEmpty();
    }

    private static ScenarioAction Reveal(string area) => new()
    {
        Do = "revealArea",
        Extra = new Dictionary<string, JsonElement> { ["area"] = JsonSerializer.SerializeToElement(area) },
    };

    private static Scenario Scenario(params Node[] nodes) => new()
    {
        Id = "hb:scenario/test",
        Title = new Dictionary<string, string> { ["en"] = "Test" },
        Packs = ["hb"],
        Start = "fight",
        Nodes = nodes,
    };

    private static BoardMap Board() => new() { Tiles = [new MapTile("hb:tile/t1", 0, 0, 0)] };

    private static Node Encounter() => new()
    {
        Id = "fight",
        Type = Node.EncounterType,
        LootLevel = "common",
        Next = "@end",
        Map = Board(),
        Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute", At = new Cell(1, 1) }] },
    };

    private static ScenarioEvent Event(ScenarioAction action) => new()
    {
        Id = "e1",
        Trigger = new EventTrigger { On = "roundStart" },
        Actions = [action],
    };
}
