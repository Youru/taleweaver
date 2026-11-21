using System.Net;
using System.Net.Http.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.IntegrationTests;

public class ItemsEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetItems_ReturnsTheCatalogueCards()
    {
        var items = await _client.GetFromJsonAsync<List<Item>>("/api/catalog/items", JsonDefaults.Options, Token);

        items!.Select(i => i.Id).Should().Equal("hb:item/plank", "hb:item/sword");
        var sword = items!.Single(i => i.Id == "hb:item/sword").Stats!;
        sword.Dice.Should().Equal("black", "red");
        sword.Hands.Should().Be(2);
        sword.Attack.Should().Be("melee");
        sword.Armor.Should().Be(1);
        sword.Defense!.Value.GetString().Should().Be("X");
        items!.Single(i => i.Id == "hb:item/plank").Stats.Should().BeNull();
    }

    [Fact]
    public async Task ItemCommands_HandOutMoveUse_ChangeThePartyItems()
    {
        var game = await StartAsync();

        game = await SendAsync(HttpMethod.Post, $"/api/games/{game.Id}/items", new { item = "hb:item/plank", owner = "Rook", slot = "pack" });
        var rook = game.State.Campaign.Party.Single(a => a.Name == "Rook");
        rook.Pack.Should().ContainSingle().Which.Item.Should().Be("hb:item/plank");
        var id = rook.Pack[0].Id;

        game = await SendAsync(HttpMethod.Put, $"/api/games/{game.Id}/items/{id}/owner", new { owner = "Rook", slot = "equipped" });
        game = await SendAsync(HttpMethod.Put, $"/api/games/{game.Id}/items/{id}/flipped", new { flipped = true });
        game.State.Campaign.Party.Single(a => a.Name == "Rook").Equipped.Single().Flipped.Should().BeTrue();

        game = await SendAsync(HttpMethod.Post, $"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        game = await SendAsync(HttpMethod.Post, $"/api/games/{game.Id}/items/{id}/use", new { discard = true, token = "hb:token/plank", at = new { x = 2, y = 3 } });

        game.State.Campaign.Party.Single(a => a.Name == "Rook").Equipped.Should().BeEmpty();
        game.State.Encounter!.Tokens.Should().Contain(t => t.Type == "hb:token/plank" && t.Area == new Rect(2, 3, 1, 1));
        game.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task AddItem_TooExpensive_ReturnsConflict()
    {
        var game = await StartAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.Id}/items", new { item = "hb:item/sword", owner = "stash", price = 40 }, JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddItem_BadSlot_ReturnsValidationProblem()
    {
        var game = await StartAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.Id}/items", new { item = "hb:item/sword", owner = "Rook", slot = "belt" }, JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<GameView> StartAsync() =>
        await Read(await _client.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Nightingale", "Rook"]), JsonDefaults.Options, Token));

    private async Task<GameView> SendAsync(HttpMethod method, string url, object body)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: JsonDefaults.Options) }, Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Token));
        return await Read(response);
    }

    private static async Task<GameView> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, Token))!;
}
