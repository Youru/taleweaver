using System.Net.Http.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

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
        report.Warnings.Should().ContainSingle().Which.Code.Should().Be("Scenarios.TokenNeverPlaced");
        report.Cards.Should().HaveCount(9);
    }
}
