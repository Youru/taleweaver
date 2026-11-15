using System.Net.Http.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

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
}
