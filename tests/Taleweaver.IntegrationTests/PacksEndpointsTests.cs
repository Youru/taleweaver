using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Packs;

namespace Taleweaver.IntegrationTests;

public class PacksEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListPacks_HomebrewContent_ReturnsEnabledPack()
    {
        var packs = await _client.GetFromJsonAsync<List<ListPacks.PackResponse>>("/api/packs", JsonDefaults.Options, TestContext.Current.CancellationToken);

        packs.Should().ContainSingle().Which.Should().Match<ListPacks.PackResponse>(p => p.Id == "hb" && p.Enabled && p.Kind == "homebrew");
    }

    [Fact]
    public async Task GetCombatant_KnownCard_ReturnsStatsAndAiSteps()
    {
        var combatant = await _client.GetFromJsonAsync<Combatant>("/api/packs/hb/combatants/brute-mundane", JsonDefaults.Options, TestContext.Current.CancellationToken);

        combatant!.Stats!.Health.Should().Be(new StatValue(8));
        combatant.AiSteps.Should().HaveCount(2);
        combatant.AiSteps[0].Condition.Kind.Should().Be("ask");
    }

    [Fact]
    public async Task GetCombatant_UnknownCard_ReturnsProblemWithStableCode()
    {
        var response = await _client.GetAsync("/api/packs/hb/combatants/dragon", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem!.Title.Should().Be("Packs.CombatantNotFound");
    }

    [Fact]
    public async Task SearchCatalog_KindAndQuery_ReturnsMatchingEntries()
    {
        var entries = await _client.GetFromJsonAsync<List<SearchCatalog.Entry>>("/api/catalog?kind=combatant&q=brute", JsonDefaults.Options, TestContext.Current.CancellationToken);

        entries!.Select(e => e.Id).Should().BeEquivalentTo(["hb:combatant/brute-mundane", "hb:combatant/brute-uncommon"]);
    }

    [Fact]
    public async Task SearchCatalog_UnknownKind_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/api/catalog?kind=monster", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem!.Errors.Should().ContainKey("Kind");
    }
}
