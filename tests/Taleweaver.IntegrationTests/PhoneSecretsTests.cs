using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Features.Packs;
using Taleweaver.Api.Storage;

namespace Taleweaver.IntegrationTests;

/// <summary>On the real content, a phone reads no secret card until the game reveals it (room-spec §8).</summary>
public class PhoneSecretsTests(RepositoryFactory factory) : IClassFixture<RepositoryFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APhone_ReadsTheSecretCardsOnlyOnceRevealed()
    {
        var gm = factory.CreateClient();
        var started = await gm.PostAsJsonAsync("/api/games", new StartGame.Request("um1:scenario/act1", []), JsonDefaults.Options, Token);
        var game = (await started.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, Token))!;
        (await gm.PostAsync($"/api/games/{game.Id}/room", null, Token)).EnsureSuccessStatusCode();
        using var phone = factory.CreateClient();
        phone.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.30");
        var catalog = await factory.Services.GetRequiredService<CatalogProvider>().GetAsync(Token);
        var packs = catalog.Packs;

        (await Read<Item>(phone, "items")).Should().NotBeEmpty().And.NotContain(i => i.Story || i.Tier == "monster");
        (await Read<Discipline>(phone, "disciplines")).Should().NotBeEmpty().And.NotContain(d => d.Story);
        (await Read<InitiativeCard>(phone, "initiative")).Should().NotBeEmpty().And.OnlyContain(c => c.Adventurer);
        var board = await phone.GetFromJsonAsync<GetBoardPieces.Response>("/api/catalog/board", JsonDefaults.Options, Token);
        board!.Families.Should().BeEmpty();
        // An adventurer's own standee is public, even where a combatant of that name borrows it
        var starters = packs.SelectMany(p => p.Adventurers).Where(a => !a.Hidden).SelectMany(a => a.Name.Values).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var standees = packs.SelectMany(p => p.Manifest.Figures).Where(f => starters.Contains(f.Key)).Select(f => f.Value.Image).ToHashSet();
        board.Figures.Values.Select(f => f.Image).OfType<string>().Except(standees.OfType<string>()).Should().NotIntersectWith(packs.SelectMany(p => p.Combatants).Select(c => c.Miniature).OfType<string>());

        var secrets = packs.SelectMany(p => p.Combatants).SelectMany(c => new[] { c.Image, c.ImageBack, c.Miniature })
            .Concat(packs.SelectMany(p => p.Items).Where(i => i.Story).Select(i => i.Image))
            .OfType<string>().Distinct().Except(standees.OfType<string>()).ToList();
        foreach (var picture in secrets)
        {
            (await phone.GetAsync(Url(picture), Token)).StatusCode.Should().Be(HttpStatusCode.NotFound, picture);
        }

        var tile = packs.SelectMany(p => p.Tiles).First(t => t.Image is not null).Image!;
        (await phone.GetAsync(Url(tile), Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Two codes are revealed: a board and an item
        string[] keys = ["large:BP1", "medium:BP3"];
        var store = factory.Services.GetRequiredService<IGameStore>();
        var saved = (await store.GetAsync(game.Id, Token))!;
        saved.State.Campaign.RevealedHiddenCards.AddRange(keys);
        await store.SaveAsync(saved, Token);
        factory.Services.GetRequiredService<Taleweaver.Api.Features.Rooms.PhoneCards>().Changed();

        var item = packs.SelectMany(p => p.Items).Single(i => i.Id == catalog.HiddenCardValues(keys[1]).Single());
        (await Read<Item>(phone, "items")).Should().ContainSingle(i => i.Id == item.Id);
        foreach (var revealed in keys.SelectMany(catalog.HiddenCard))
        {
            (await phone.GetAsync(Url(revealed.Image), Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await Read<Item>(gm, "items")).Should().Contain(i => i.Story, "the GM reads everything");
    }

    private static string Url(string picture) => "/api/" + picture;

    private static async Task<List<T>> Read<T>(HttpClient client, string table) =>
        (await client.GetFromJsonAsync<List<T>>($"/api/catalog/{table}", JsonDefaults.Options, Token))!;
}
