using System.Net;
using System.Net.Http.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Storage;

namespace Taleweaver.IntegrationTests;

public class ScenariosEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListScenarios_HomebrewContent_ReturnsTheScenario()
    {
        var scenarios = await _client.GetFromJsonAsync<List<ScenarioSummary>>("/api/scenarios", JsonDefaults.Options, TestContext.Current.CancellationToken);

        scenarios.Should().Contain(s => s.Id == "hb:scenario/meadow-ambush");
    }

    [Fact]
    public async Task SaveScenario_NewHomebrewScenario_IsListedAsEditableThenDeleted()
    {
        var scenario = (await FixtureScenarioAsync()) with
        {
            Id = "homebrew:scenario/my-bounty",
            Title = new Dictionary<string, string> { ["fr"] = "Ma prime" },
            Editor = System.Text.Json.JsonDocument.Parse("{\"positions\":{\"intro\":{\"x\":0,\"y\":0}}}").RootElement,
        };

        var saved = await _client.PutAsJsonAsync("/api/scenarios/homebrew/my-bounty", scenario, JsonDefaults.Options, TestContext.Current.CancellationToken);

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<ScenarioReport>(JsonDefaults.Options, TestContext.Current.CancellationToken))!.IsValid.Should().BeTrue();
        var list = await _client.GetFromJsonAsync<List<ScenarioSummary>>("/api/scenarios", JsonDefaults.Options, TestContext.Current.CancellationToken);
        list.Should().ContainSingle(s => s.Id == "homebrew:scenario/my-bounty").Which.Editable.Should().BeTrue();
        list.Should().ContainSingle(s => s.Id == "hb:scenario/meadow-ambush").Which.Editable.Should().BeFalse();
        var read = await _client.GetFromJsonAsync<Scenario>("/api/scenarios/homebrew/my-bounty", JsonDefaults.Options, TestContext.Current.CancellationToken);
        read!.Editor!.Value.GetProperty("positions").GetProperty("intro").GetProperty("x").GetInt32().Should().Be(0);

        var deleted = await _client.DeleteAsync("/api/scenarios/homebrew/my-bounty", TestContext.Current.CancellationToken);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync("/api/scenarios/homebrew/my-bounty", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SaveScenario_IdOfAnotherScenario_ReturnsBadRequest()
    {
        var scenario = await FixtureScenarioAsync();

        var response = await _client.PutAsJsonAsync("/api/scenarios/homebrew/meadow-ambush", scenario, JsonDefaults.Options, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteScenario_OfficialScenario_IsNotFoundAmongHomebrew()
    {
        var response = await _client.DeleteAsync("/api/scenarios/homebrew/meadow-ambush", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync("/api/scenarios/hb/meadow-ambush", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetScenario_KnownId_ReturnsNodes()
    {
        var scenario = await _client.GetFromJsonAsync<Scenario>("/api/scenarios/hb/meadow-ambush", JsonDefaults.Options, TestContext.Current.CancellationToken);

        scenario!.Nodes.Select(n => n.Id).Should().Equal("intro", "ambush");
    }

    [Fact]
    public async Task GetScenario_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/scenarios/hb/missing", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateScenario_ValidScenario_ReturnsReportWithChosenCard()
    {
        var report = await ValidateAsync(await FixtureScenarioAsync());

        report.IsValid.Should().BeTrue();
        report.Cards.Should().ContainSingle().Which.CombatantId.Should().Be("hb:combatant/brute-mundane");
    }

    [Fact]
    public async Task ValidateScenario_BrokenReference_ReturnsErrorInReport()
    {
        var scenario = await FixtureScenarioAsync();
        var broken = scenario with { Nodes = [scenario.Nodes[0] with { Next = "nowhere" }, scenario.Nodes[1]] };

        var report = await ValidateAsync(broken);

        report.Errors.Should().ContainSingle().Which.Code.Should().Be("Scenarios.UnknownNext");
    }

    [Fact]
    public async Task ValidateScenario_ScenarioWithoutNodes_ReturnsValidationProblem()
    {
        var scenario = await FixtureScenarioAsync() with { Nodes = [] };

        var response = await _client.PostAsJsonAsync("/api/scenarios/validate", scenario, JsonDefaults.Options, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Scenario> FixtureScenarioAsync() =>
        (await _client.GetFromJsonAsync<Scenario>("/api/scenarios/hb/meadow-ambush", JsonDefaults.Options, TestContext.Current.CancellationToken))!;

    private async Task<ScenarioReport> ValidateAsync(Scenario scenario)
    {
        var response = await _client.PostAsJsonAsync("/api/scenarios/validate", scenario, JsonDefaults.Options, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ScenarioReport>(JsonDefaults.Options, TestContext.Current.CancellationToken))!;
    }
}
