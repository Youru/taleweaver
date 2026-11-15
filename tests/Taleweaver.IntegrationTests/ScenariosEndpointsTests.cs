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

        scenarios.Should().ContainSingle().Which.Id.Should().Be("hb:scenario/meadow-ambush");
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
