using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.IntegrationTests;

/// <summary>The screens linked by QR code (room-spec §7.6): the GM's tablet and the table's screen.</summary>
public class ScreenEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _gm = factory.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheGmsTablet_PlaysItsGame_AndOnlyThatOne()
    {
        var game = await StartGame();
        var other = await StartGame();
        var tablet = await Linked(game.Id, ScreenRole.Gm);

        (await Get(tablet, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.OK);
        (await Get(tablet, $"/api/games/{game.Id}/room")).Should().Be(HttpStatusCode.OK);
        (await Get(tablet, "/api/scenarios/hb/meadow-ambush")).Should().Be(HttpStatusCode.OK, "its own scenario");
        (await Get(tablet, "/api/catalog/dice")).Should().Be(HttpStatusCode.OK);
        (await tablet.PutAsJsonAsync($"/api/games/{game.Id}/session/pause", new { paused = true }, Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Get(tablet, $"/api/games/{other.Id}")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(tablet, "/api/games")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(tablet, "/api/scenarios")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(tablet, "/api/scenarios/hb/another-one")).Should().Be(HttpStatusCode.Forbidden, "the secrets of the other scenarios");
        (await Get(tablet, "/api/catalog")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(tablet, "/api/print/pdf?scenario=x")).Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A route added under a game is refused to the tablet unless it is one of these groups or routes.</summary>
    [Fact]
    public void UnderAGame_OnlyTheseRoutes_StayOnTheComputer()
    {
        var refused = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in factory.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>())
        {
            var pattern = "/" + endpoint.RoutePattern.RawText!.Trim('/');
            if (!pattern.StartsWith("/api/games/{id}", StringComparison.Ordinal))
            {
                continue;
            }

            var rest = Regex.Replace(pattern["/api/games/{id}".Length..], @"\{[^}]+\}", "x").Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                if (!ScreenAccess.AllowsInGame(method, rest))
                {
                    refused.Add($"{method} {string.Join('/', rest)}".TrimEnd());
                }
            }
        }

        refused.Should().BeEquivalentTo("DELETE", "DELETE room", "DELETE room/screens/x", "GET act-export", "GET export", "POST restart", "POST room", "POST room/screens/x");
    }

    [Theory]
    [InlineData("DELETE", "")]
    [InlineData("DELETE", "/room")]
    [InlineData("POST", "/room")]
    [InlineData("POST", "/room/screens/gm")]
    [InlineData("POST", "/room/screens/table")]
    [InlineData("DELETE", "/room/screens/gm")]
    [InlineData("GET", "/export")]
    [InlineData("POST", "/restart")]
    public async Task WhatManagesTheGame_IsRefusedToTheTablet(string method, string route)
    {
        var game = await StartGame();
        var tablet = await Linked(game.Id, ScreenRole.Gm);

        var answer = await tablet.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/games/{game.Id}{route}"), Token);

        answer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Get(_gm, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(ScreenRole.Gm)]
    [InlineData(ScreenRole.Table)]
    public async Task AQrCode_LinksOneDeviceOnly(string role)
    {
        var game = await StartGame();
        var pairing = await Pairing(game.Id, role);
        (await Device().PostAsJsonAsync("/api/screens/claim", new ScreenEndpoints.ClaimRequest(pairing), Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await Device().PostAsJsonAsync("/api/screens/claim", new ScreenEndpoints.ClaimRequest(pairing), Token);

        again.StatusCode.Should().Be(HttpStatusCode.Gone, "photographed or scanned a second time");
        var room = await Read<RoomView>(await _gm.GetAsync($"/api/games/{game.Id}/room", Token));
        room.Screens.Waiting.Should().BeEmpty("the computer hides the code");
    }

    [Theory]
    [InlineData(ScreenRole.Gm)]
    [InlineData(ScreenRole.Table)]
    public async Task TheQrCodesOwnToken_OpensNothing(string role)
    {
        var game = await StartGame();
        var pairing = await Pairing(game.Id, role);
        var device = Device();

        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Screen", pairing);
        (await Get(device, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.Forbidden);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Table", pairing);
        (await Get(device, "/api/screens/table")).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ANewTablet_ReplacesTheFirst_OnlyOnceScanned()
    {
        var game = await StartGame();
        var first = await Linked(game.Id, ScreenRole.Gm);
        var pairing = await Pairing(game.Id, ScreenRole.Gm);
        (await Get(first, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.OK, "the code is only shown");

        var second = await Claim(pairing, ScreenRole.Gm);

        (await Get(second, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.OK);
        (await Get(first, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SeveralTables_LookUntilRevoked_TheGmsTabletStays()
    {
        var game = await StartGame();
        var tablet = await Linked(game.Id, ScreenRole.Gm);
        HttpClient[] tables = [await Linked(game.Id, ScreenRole.Table), await Linked(game.Id, ScreenRole.Table)];
        foreach (var table in tables)
        {
            (await Get(table, "/api/screens/table")).Should().Be(HttpStatusCode.OK);
        }

        var room = await Read<RoomView>(await _gm.DeleteAsync($"/api/games/{game.Id}/room/screens/table", Token));

        room.Screens.Tables.Should().Be(0);
        foreach (var table in tables)
        {
            (await Get(table, "/api/screens/table")).Should().Be(HttpStatusCode.Forbidden);
        }

        (await Get(tablet, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheTablesToken_NeverActsAsTheGm()
    {
        var game = await StartGame();
        var table = await Linked(game.Id, ScreenRole.Table);
        var token = table.DefaultRequestHeaders.Authorization!.Parameter!;
        table.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Screen", token);

        (await Get(table, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(table, $"/api/games/{game.Id}?screen_token={token}")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(table, "/api/scenarios/hb/meadow-ambush")).Should().Be(HttpStatusCode.Forbidden);
        (await table.PostAsync($"/api/games/{game.Id}/undo", null, Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheTablesView_CarriesNothingOutsideTheWhiteList()
    {
        var game = await StartGame();
        var table = await Linked(game.Id, ScreenRole.Table);

        var json = await table.GetStringAsync("/api/screens/table", Token);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(p => p.Name).Should()
            .BeSubsetOf(["version", "gameId", "scenarioTitle", "party", "encounter", "cards", "paused"]);
        json.Should().NotContain("\"prompt\"").And.NotContain("hiddenText").And.NotContain("aiSteps").And.NotContain("\"stash\"").And.NotContain("\"log\"");
    }

    [Fact]
    public void TheTablesView_DropsTheCards_AndWhatAPlayerAloneReads()
    {
        var encounter = new PlayerEncounter(1, 0, false, [], 0, [],
            [new PlayerCombatant("c-1", "card", "family", 1, [], 0, [], false, null, "assets/card-with-its-ai.jpg", 5)],
            null, new Dictionary<string, IReadOnlyList<string>>(), null, [], [new PlayerAllyOption("Rook", "summon", [])]);
        var view = new PlayerView(1, "g", null, "CODE", null, true, [], [], 12, [], [], encounter, [], [], RoomAutonomy.ValidateAll, null, [], null);

        var table = TableScreenView.Of(view);

        table.Encounter!.Combatants.Single().Image.Should().BeNull();
        table.Encounter.AllyOptions.Should().BeEmpty();
        TableScreenView.Of(view with { Encounter = encounter with { Ended = true } }).Encounter.Should().BeNull("a finished board is not left on show");
    }

    [Fact]
    public async Task AClosedRoom_CutsBothScreens_AndReopenedGivesThemBack()
    {
        var game = await StartGame();
        var tablet = await Linked(game.Id, ScreenRole.Gm);
        var table = await Linked(game.Id, ScreenRole.Table);

        await _gm.DeleteAsync($"/api/games/{game.Id}/room", Token);
        (await Get(tablet, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.Forbidden);
        (await Get(table, "/api/screens/table")).Should().Be(HttpStatusCode.Gone);

        await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token);
        (await Get(tablet, $"/api/games/{game.Id}")).Should().Be(HttpStatusCode.OK);
        (await Get(table, "/api/screens/table")).Should().Be(HttpStatusCode.OK);
    }

    private async Task<GameView> StartGame() =>
        await Read<GameView>(await _gm.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Nightingale", "Rook"]), JsonDefaults.Options, Token));

    private async Task<string> Pairing(string game, string role)
    {
        await _gm.PostAsync($"/api/games/{game}/room", null, Token);
        return (await Read<ScreenEndpoints.PairingResponse>(await _gm.PostAsync($"/api/games/{game}/room/screens/{role}", null, Token))).Token;
    }

    private async Task<HttpClient> Linked(string game, string role) => await Claim(await Pairing(game, role), role);

    private async Task<HttpClient> Claim(string pairing, string role)
    {
        var device = Device();
        var claimed = await Read<ScreenEndpoints.ClaimResponse>(await device.PostAsJsonAsync("/api/screens/claim", new ScreenEndpoints.ClaimRequest(pairing), Token));
        claimed.Role.Should().Be(role);
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(role == ScreenRole.Gm ? "Screen" : "Table", claimed.Token);
        return device;
    }

    private HttpClient Device()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.30");
        return client;
    }

    private static async Task<HttpStatusCode> Get(HttpClient client, string path) => (await client.GetAsync(path, Token)).StatusCode;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Token));
        return (await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, Token))!;
    }
}
