using System.Net;
using System.Net.Http.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Rulebook;

namespace Taleweaver.IntegrationTests;

/// <summary>The rulebook as the repository has it: whole for the GM, closed to the phones.</summary>
public class RulebookEndpointTests(RepositoryFactory factory) : IClassFixture<RepositoryFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetRulebook_FromThisMachine_GivesEveryChapterAndTheGlossary()
    {
        var book = await _client.GetFromJsonAsync<GetRulebook.Response>("/api/rulebook", JsonDefaults.Options, Token);

        book!.Chapters.Select(c => c.Id).Should().Equal("01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12");
        book.Chapters[6].Markdown.Should().Contain("<!-- p.36 -->");
        book.Glossary.Should().Contain("| EN | FR |");
    }

    [Fact]
    public async Task GetRulebook_FromAnotherDevice_IsRefused()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/rulebook");
        request.Headers.Add("X-Forwarded-For", "192.168.1.20");

        (await _client.SendAsync(request, Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
