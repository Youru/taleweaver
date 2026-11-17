using System.Net.Http.Json;
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

    [Fact]
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

    [Fact]
    public async Task ListScenarios_RepositoryFiles_NoneSkipped()
    {
        // A scenario file the API cannot read is skipped with a mere log line: the Python checker does not see it
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scenarios")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        var files = Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json", SearchOption.AllDirectories).Count();

        var scenarios = await _client.GetFromJsonAsync<List<ScenarioSummary>>("/api/scenarios", JsonDefaults.Options, TestContext.Current.CancellationToken);

        scenarios.Should().HaveCount(files, "each file of scenarios/ must deserialize (see the API log for the one skipped)");
    }

    [Fact]
    public async Task GetBoardPieces_WithBe1_GivesTokensTheirPicturesVariantsAndFiguresTheirPortrait()
    {
        var pieces = await _client.GetFromJsonAsync<Taleweaver.Api.Features.Packs.GetBoardPieces.Response>("/api/catalog/board", JsonDefaults.Options, TestContext.Current.CancellationToken);

        pieces!.Tokens["be1:token/terrain/bug-creep"].Image.Should().Be("assets/expansions/be1/tokens/bug-creep-1x1-plain.jpg");
        pieces.Tokens["be1:token/terrain/bug-creep"].Variants.Should().HaveCount(2);
        pieces.Tokens["um1:token/terrain/dangerous"].Variants.Should().Contain("assets/expansions/be1/tokens/dangerous-1x1-hole.jpg");
        pieces.Figures["cave-sickle-warrior"].Portrait.Should().Be("assets/expansions/be1/portraits/cave-sickle-warrior.png");
        pieces.Figures["fragor"].Portrait.Should().Be("assets/templates/encounter/map/basic-monsters/fragor.png");
    }

    [Fact]
    public async Task ValidateScenario_CaveSickleQueen_NoError()
    {
        var token = TestContext.Current.CancellationToken;
        var scenario = await _client.GetFromJsonAsync<Scenario>("/api/scenarios/be1/the-cave-sickle-queen", JsonDefaults.Options, token);

        var response = await _client.PostAsJsonAsync("/api/scenarios/validate", scenario, JsonDefaults.Options, token);

        var report = await response.Content.ReadFromJsonAsync<ScenarioReport>(JsonDefaults.Options, token);
        report!.Errors.Should().BeEmpty();
    }

    [Fact]
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

    [Fact]
    public async Task GetDice_CorePack_NineDiceOfSixFaces()
    {
        var dice = await _client.GetFromJsonAsync<Dictionary<string, Die>>("/api/catalog/dice", JsonDefaults.Options, TestContext.Current.CancellationToken);

        dice!.Keys.Should().BeEquivalentTo("purple", "white", "orange", "teal", "red", "green", "grey", "blue", "black");
        dice.Values.Should().AllSatisfy(d => d.Faces.Should().HaveCount(6));
        dice["blue"].Faces.Average(f => f.Value).Should().Be(7.5);
    }

    [Fact]
    public async Task GetEffects_RealPacks_ListsTheElevenEffectsOfTheRules()
    {
        var effects = await _client.GetFromJsonAsync<List<RuleEntry>>("/api/catalog/effects", JsonDefaults.Options, TestContext.Current.CancellationToken);

        effects!.Select(e => e.Id).Should().BeEquivalentTo(
            "condemn", "darkness", "disease", "paralyze", "poison", "wilt", "barrier", "courage", "haste", "tether", "nemesis");
        effects!.Single(e => e.Id == "poison").Condition.Should().Be("status");
    }
}
