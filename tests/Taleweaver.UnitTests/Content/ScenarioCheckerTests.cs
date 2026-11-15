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
                    Id = "hb:combatant/brute-common", Family = "brute", Kind = "intelligent", LootLevel = "common",
                    Name = new Dictionary<string, string> { ["en"] = "Brute" },
                },
            ],
            Tokens =
            [
                new TokenType { Id = "hb:token/objective", Category = "objective", Name = new Dictionary<string, string> { ["en"] = "Objective" } },
                new TokenType { Id = "hb:token/terrain/obstructing-long", Category = "terrain", Name = new Dictionary<string, string> { ["en"] = "Obstructing" } },
            ],
            Tiles = [new Tile { Id = "hb:tile/t1", Name = "T1" }],
        },
    ]);

    [Fact]
    public async Task Check_BlightOfRhamsted_SameResultAsCheckScenarioPy()
    {
        var scenario = await RepositoryContent.BlightAsync();
        var catalog = await RepositoryContent.CatalogAsync();

        var report = ScenarioChecker.Check(scenario, catalog);

        report.Errors.Should().BeEmpty();
        report.Warnings.Should().ContainSingle().Which.Should().Be(
            new ScenarioIssue("Scenarios.TokenNeverPlaced", "blight-found", "token 'loot:blue' is described but never placed"));
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
            Tokens = new Dictionary<string, TokenRule> { ["objective:blue"] = new() { Mode = "encounterAction" } },
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
