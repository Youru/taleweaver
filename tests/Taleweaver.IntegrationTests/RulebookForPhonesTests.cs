using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Features.Packs;
using Taleweaver.Api.Features.Rulebook;

namespace Taleweaver.IntegrationTests;

/// <summary>On the real content: what a phone reads of the rules names no secret card (rules-spec §4).</summary>
public class RulebookForPhonesTests(RepositoryFactory factory) : IClassFixture<RepositoryFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [ContentFact]
    public async Task APhone_ReadsTheRules_OnlyWhileARoomIsOpen_AndWithoutASecretName()
    {
        var gm = factory.CreateClient();
        using var phone = factory.CreateClient();
        phone.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.31");
        var started = await gm.PostAsJsonAsync("/api/games", new StartGame.Request("um1:scenario/act1", []), JsonDefaults.Options, Token);
        var game = (await started.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, Token))!;
        (await gm.PostAsync($"/api/games/{game.Id}/room", null, Token)).EnsureSuccessStatusCode();

        var whole = (await gm.GetFromJsonAsync<GetRulebook.Response>("/api/rulebook", JsonDefaults.Options, Token))!;
        var read = (await phone.GetFromJsonAsync<GetRulebook.Response>("/api/rulebook", JsonDefaults.Options, Token))!;
        var tables = (await phone.GetFromJsonAsync<GetReference.Response>("/api/catalog/reference", JsonDefaults.Options, Token))!;

        read.Chapters.Select(c => c.Id).Should().Equal("04", "05", "06", "07", "08", "09", "10", "11");
        var entries = tables.Effects.Concat(tables.AbilityTokens).Concat(tables.TerrainTypes).Concat(tables.Tags).Concat(tables.CombatantTokens).ToList();
        entries.Should().NotBeEmpty();
        // The translations too: a French name of a secret card is as secret as the English one
        read.Chapters.Should().Contain(c => c.Translations != null && c.Translations.ContainsKey("fr"), "the content has the French rulebook");
        var sent = string.Join('\n', read.Chapters.SelectMany(c => (c.Translations?.Values ?? []).Prepend(c.Markdown)).Append(read.Glossary)
            .Concat(entries.SelectMany(e => e.Name.Values.Concat(e.Text?.Values ?? []))));

        var secrets = PlayerRulebook.SecretNames(await factory.Services.GetRequiredService<CatalogProvider>().GetAsync(Token));
        var book = string.Join('\n', whole.Chapters.SelectMany(c => (c.Translations?.Values ?? []).Prepend(c.Markdown)));
        secrets.Count(name => Named(book, name)).Should().BeGreaterThan(5, "the whole book does name secret cards: the test would prove nothing otherwise");
        // By count: a failure must not print the names
        secrets.Count(name => Named(sent, name)).Should().Be(0);

        sent.Should().Contain("### Being Defeated").And.Contain("<!-- p.69 -->").And.NotContain("\n>", "the examples and the pictures stay out");
        foreach (var chapter in read.Chapters.Where(c => c.Translations?.ContainsKey("fr") == true))
        {
            chapter.Translations!["fr"].Split('\n').Length.Should().Be(chapter.Markdown.Split('\n').Length, "a translation keeps the English's lines");
        }
        (await phone.GetAsync("/api/catalog", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the rest stays the GM's");

        (await gm.DeleteAsync($"/api/games/{game.Id}/room", Token)).EnsureSuccessStatusCode();
        (await phone.GetAsync("/api/rulebook", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.GetAsync("/api/catalog/reference", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Written as a name, with a capital: in lower case it is the word.</summary>
    private static bool Named(string text, string name) =>
        Regex.Matches(text, $@"(?<!\p{{L}}){Regex.Escape(name)}(?:e?s)?(?!\p{{L}})", RegexOptions.IgnoreCase)
            .Any(m => !char.IsLower(m.Value[0]));
}
