using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Storage;

namespace Taleweaver.IntegrationTests;

/// <summary>The API on the real content: the official scenario must validate as with tools/check_scenario.py.</summary>
public class RepositoryContentTests(RepositoryFactory factory) : IClassFixture<RepositoryFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [ContentFact]
    public async Task ValidateScenario_BlightOfRhamsted_NoErrorAndOneKnownWarning()
    {
        var scenario = await _client.GetFromJsonAsync<Scenario>("/api/scenarios/um1/blight-of-rhamsted", JsonDefaults.Options, TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync("/api/scenarios/validate", scenario, JsonDefaults.Options, TestContext.Current.CancellationToken);

        var report = await response.Content.ReadFromJsonAsync<ScenarioReport>(JsonDefaults.Options, TestContext.Current.CancellationToken);
        report!.Errors.Should().BeEmpty();
        report.Warnings.Select(w => w.Code).Should().BeEquivalentTo(
            ["Scenarios.BeyondCards", "Scenarios.BeyondCards", "Scenarios.TokenNeverPlaced", "Scenarios.JumpNeeded"]);
        report.Cards.Should().HaveCount(9);
    }

    [ContentFact]
    public async Task ValidateScenario_Act1Pilot_NoError()
    {
        var scenario = await _client.GetFromJsonAsync<Scenario>("/api/scenarios/um1/act1", JsonDefaults.Options, TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync("/api/scenarios/validate", scenario, JsonDefaults.Options, TestContext.Current.CancellationToken);

        var report = await response.Content.ReadFromJsonAsync<ScenarioReport>(JsonDefaults.Options, TestContext.Current.CancellationToken);
        report!.Errors.Should().BeEmpty();
        // As in the book: The MAST: Day 1 is blocked (the party pushes the rock into the pits), the Red Exit of Bombing the
        // Sickles and two vault encounters lie past pits, some families outnumber their cards, bosses are hidden cards,
        // the mod's tiles clash, a cave is set up apart from the board (diagram 11). Where a boss hides is left unnamed.
        report.Warnings.Select(w => (w.Code, w.Code == "Scenarios.HiddenCard" ? "(boss)" : w.Where)).Should().Equal(
            ("Scenarios.JumpNeeded", "the-mast-day-1/map"),
            ("Scenarios.BeyondCards", "bombing-the-sickles"),
            ("Scenarios.ThemesApart", "bombing-the-sickles"),
            ("Scenarios.JumpNeeded", "bombing-the-sickles/map"),
            ("Scenarios.BeyondCards", "sickles-upon-sickles"),
            ("Scenarios.HiddenCard", "(boss)"),
            ("Scenarios.JumpNeeded", "retracing-their-steps/map"),
            ("Scenarios.BeyondCards", "an-alternate-path"),
            ("Scenarios.JumpNeeded", "an-alternate-path/map"),
            ("Scenarios.HiddenCard", "(boss)"),
            ("Scenarios.BeyondCards", "bungled-caper"),
            ("Scenarios.ThemesApart", "bungled-caper"),
            ("Scenarios.TilesApart", "bungled-caper"),
            ("Scenarios.HiddenCard", "(boss)"));
    }

    /// <summary>reference/act1/item-cards/symbols.json, through tools/build_items.py: the weapons' band reaches the duel.</summary>
    [ContentFact]
    public async Task Weapons_CarryTheirSymbolAbilities()
    {
        var items = await _client.GetFromJsonAsync<List<Item>>("/api/catalog/items", JsonDefaults.Options, TestContext.Current.CancellationToken);

        var weapons = items!.Where(i => i.Type == "weapon" && i.Id.StartsWith("um1:", StringComparison.Ordinal)).ToList();
        weapons.Count(w => w.SymbolAbilities is { Count: > 0 }).Should().BeGreaterThan(100);
        weapons.Count(w => w.BackSide?.SymbolAbilities is { Count: > 0 }).Should().BeGreaterThan(15);
    }

    /// <summary>tools/build_hidden_cards.py: every code points at a card of the catalogue, or at a picture that exists.</summary>
    [ContentFact]
    public async Task EveryHiddenCardCode_PointsAtACardOrAPicture()
    {
        var catalog = await factory.Services.GetRequiredService<CatalogProvider>().GetAsync(TestContext.Current.CancellationToken);
        var root = new DirectoryInfo(ContentRoot.Path!);

        var known = catalog.Packs.SelectMany(p => p.Combatants.Select(c => c.Id).Concat(p.Items.Select(i => i.Id)).Concat(p.Disciplines.Select(d => d.Id))
            .Concat(p.InitiativeCards.Select(c => c.Id)).Concat(p.Adventurers.Select(a => a.Id))).ToHashSet();

        var values = catalog.Packs.SelectMany(p => p.Manifest.HiddenCards.Values).SelectMany(v => v).ToList();

        values.Should().HaveCountGreaterThan(200);
        values.Where(v => !known.Contains(v) && !(v.StartsWith("assets/", StringComparison.Ordinal) && File.Exists(Path.Combine(root.FullName, v)))).Should().BeEmpty();
    }

    [ContentFact]
    public async Task GetAdventurers_FromAPhone_LeavesOutThoseStillToUnlock()
    {
        var token = TestContext.Current.CancellationToken;
        var started = await _client.PostAsJsonAsync("/api/games", new StartGame.Request("um1:scenario/act1", []), JsonDefaults.Options, token);
        var game = await started.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, token);
        (await _client.PostAsync($"/api/games/{game!.Id}/room", null, token)).EnsureSuccessStatusCode();
        var phone = factory.CreateClient();
        phone.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.20");

        var seen = await phone.GetFromJsonAsync<List<Adventurer>>("/api/catalog/adventurers", JsonDefaults.Options, token);

        seen!.Should().HaveCount(4).And.OnlyContain(a => !a.Hidden);
        (await _client.GetFromJsonAsync<List<Adventurer>>("/api/catalog/adventurers", JsonDefaults.Options, token)).Should().HaveCount(11);
        (await phone.GetAsync("/api/catalog/allies", token)).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [ContentFact]
    public async Task ListScenarios_RepositoryFiles_NoneSkipped()
    {
        // A scenario file the API cannot read is skipped with a mere log line: the Python checker does not see it
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scenarios")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        // A campaign folder (campaign.json and its chapters) is one scenario
        var scenariosRoot = Path.Combine(root, "scenarios");
        var campaigns = Directory.EnumerateFiles(scenariosRoot, "campaign.json", SearchOption.AllDirectories).Select(Path.GetDirectoryName).ToList();
        var files = Directory.EnumerateFiles(scenariosRoot, "*.json", SearchOption.AllDirectories)
            .Count(f => !campaigns.Any(c => f.StartsWith(c + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) + campaigns.Count;

        var scenarios = await _client.GetFromJsonAsync<List<ScenarioSummary>>("/api/scenarios", JsonDefaults.Options, TestContext.Current.CancellationToken);

        scenarios.Should().HaveCount(files, "each file of scenarios/ must deserialize (see the API log for the one skipped)");
    }

    [ContentFact]
    public async Task GetBoardPieces_WithBe1_GivesTokensTheirPicturesVariantsAndFiguresTheirPortrait()
    {
        var pieces = await _client.GetFromJsonAsync<Taleweaver.Api.Features.Packs.GetBoardPieces.Response>("/api/catalog/board", JsonDefaults.Options, TestContext.Current.CancellationToken);

        pieces!.Tokens["be1:token/terrain/bug-creep"].Image.Should().Be("assets/expansions/be1/tokens/bug-creep-1x1-plain.jpg");
        pieces.Tokens["be1:token/terrain/bug-creep"].Variants.Should().HaveCount(2);
        pieces.Tokens["um1:token/terrain/dangerous"].Variants.Should().Contain("assets/expansions/be1/tokens/dangerous-1x1-hole.jpg");
        pieces.Figures["cave-sickle-warrior"].Portrait.Should().Be("assets/expansions/be1/portraits/cave-sickle-warrior.png");
        pieces.Figures["fragor"].Portrait.Should().Be("assets/templates/encounter/map/basic-monsters/fragor.png");
    }

    [ContentFact]
    public async Task ValidateScenario_CaveSickleQueen_NoError()
    {
        var token = TestContext.Current.CancellationToken;
        var scenario = await _client.GetFromJsonAsync<Scenario>("/api/scenarios/be1/the-cave-sickle-queen", JsonDefaults.Options, token);

        var response = await _client.PostAsJsonAsync("/api/scenarios/validate", scenario, JsonDefaults.Options, token);

        var report = await response.Content.ReadFromJsonAsync<ScenarioReport>(JsonDefaults.Options, token);
        report!.Errors.Should().BeEmpty();
    }

    [ContentFact]
    public async Task StartGame_BlightOfRhamsted_EntersTheFirstEncounterAfterTheIntro()
    {
        var token = TestContext.Current.CancellationToken;
        var start = await _client.PostAsJsonAsync("/api/games", new StartGame.Request("um1:scenario/blight-of-rhamsted", ["Nightingale", "Rook"]), JsonDefaults.Options, token);
        var game = (await start.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, token))!;

        var response = await _client.PostAsJsonAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null), JsonDefaults.Options, token);

        var state = (await response.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, token))!.State;
        state.Encounter!.Node.Should().Be("search-for-the-lair");
        state.Encounter.Combatants.Should().HaveCount(6);
    }

    [ContentFact]
    public async Task GetDice_CorePack_NineDiceOfSixFaces()
    {
        var dice = await _client.GetFromJsonAsync<Dictionary<string, Die>>("/api/catalog/dice", JsonDefaults.Options, TestContext.Current.CancellationToken);

        dice!.Keys.Should().BeEquivalentTo("purple", "white", "orange", "teal", "red", "green", "grey", "blue", "black");
        dice.Values.Should().AllSatisfy(d => d.Faces.Should().HaveCount(6));
        dice["blue"].Faces.Average(f => f.Value).Should().Be(7.5);
    }

    [ContentFact]
    public async Task Catalog_RealPacks_HaveTheHealthIconsOfDisciplinesAndUpgrades()
    {
        var provider = factory.Services.GetRequiredService<CatalogProvider>();

        var catalog = await provider.GetAsync(TestContext.Current.CancellationToken);

        catalog.FindDiscipline("um1:discipline/nirvana-s-mark")!.Health.Should().Be(2);
        catalog.FindDiscipline("um1:discipline/physically-perfected")!.HealthPerHighestLevel.Should().Be("martial");
        catalog.FindUpgrade("um1:item-upgrade/sentient")!.Health.Should().Be(2);
    }

    [ContentFact]
    public async Task GetEffects_RealPacks_ListsTheElevenEffectsOfTheRules()
    {
        var effects = await _client.GetFromJsonAsync<List<RuleEntry>>("/api/catalog/effects", JsonDefaults.Options, TestContext.Current.CancellationToken);

        effects!.Select(e => e.Id).Should().BeEquivalentTo(
            "condemn", "darkness", "disease", "paralyze", "poison", "wilt", "barrier", "courage", "haste", "tether", "nemesis");
        effects!.Single(e => e.Id == "poison").Condition.Should().Be("status");
    }
}
