using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.IntegrationTests;

/// <summary>The room, increment R1 (docs/design/room-spec.md): open, join from a phone, give adventurers, the player's view.</summary>
public class RoomEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private const string PhoneAddress = "192.168.1.20";

    private readonly HttpClient _gm = factory.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APhone_JoinsWithTheCode_AndSeesOnlyTheAdventurersTheGmGaveIt()
    {
        var game = await StartGame();
        var room = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token));
        room.Code.Should().MatchRegex("^[A-HJKMNP-Z]{4}$");

        var phone = Phone();
        var joined = await Read<RoomEndpoints.JoinResponse>(await phone.PostAsJsonAsync($"/api/rooms/{room.Code.ToLowerInvariant()}/seats", new RoomEndpoints.JoinRequest("Léa", "rose"), JsonDefaults.Options, Token));
        joined.View.Adventurers.Should().BeEmpty("waiting for the GM");
        joined.View.Seat!.Color.Should().Be("rose");

        await Read<RoomView>(await _gm.PutAsJsonAsync($"/api/games/{game.Id}/room/seats/{joined.SeatId}/adventurers", new RoomEndpoints.AssignRequest(["Rook"]), JsonDefaults.Options, Token));

        phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Seat", joined.SeatToken);
        var view = await Read<PlayerView>(await phone.GetAsync($"/api/rooms/{room.Code}/me", Token));
        view.Adventurers.Select(a => a.Name).Should().Equal("Rook");
        view.Seat!.Followed.Should().Be("Rook");
        view.Party.Select(p => p.Name).Should().Equal("Nightingale", "Rook");
        view.Party.Single(p => p.Name == "Rook").Mine.Should().BeTrue();
    }

    [Fact]
    public async Task ThePlayersView_CarriesNothingOutsideTheWhiteList()
    {
        var game = await StartGame();
        var (phone, code, _) = await JoinedPhone(game, ["Rook"]);

        var json = await phone.GetStringAsync($"/api/rooms/{code}/me", Token);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(p => p.Name).Should().BeSubsetOf(
            ["version", "gameId", "scenarioTitle", "roomCode", "seat", "impersonated", "adventurers", "party", "gold", "stash", "unassigned", "encounter", "log", "requests", "autonomy", "shop"]);
        json.Should().NotContain("\"prompt\"").And.NotContain("hiddenText").And.NotContain("\"variables\"").And.NotContain("aiSteps");
        document.RootElement.GetProperty("log").EnumerateArray().Select(l => l.GetProperty("entry").GetProperty("code").GetString())
            .Should().NotBeEmpty().And.OnlyContain(code => PlayerViewProjector.PublicLogCodes.Contains(code!));
        document.RootElement.GetProperty("party").EnumerateArray().SelectMany(p => p.EnumerateObject().Select(o => o.Name)).Should().NotContain(["equipped", "pack", "disciplines", "xp"]);
    }

    [Fact]
    public async Task GivingAnAdventurerToASeat_TakesItFromTheOtherSeat()
    {
        var game = await StartGame();
        var (_, code, first) = await JoinedPhone(game, ["Rook", "Nightingale"]);
        var second = await Read<RoomEndpoints.JoinResponse>(await Phone().PostAsJsonAsync($"/api/rooms/{code}/seats", new RoomEndpoints.JoinRequest("Max", null), JsonDefaults.Options, Token));

        var room = await Read<RoomView>(await _gm.PutAsJsonAsync($"/api/games/{game.Id}/room/seats/{second.SeatId}/adventurers", new RoomEndpoints.AssignRequest(["Rook"]), JsonDefaults.Options, Token));

        room.Seats.Single(s => s.Id == first).Adventurers.Should().Equal("Nightingale");
        room.Seats.Single(s => s.Id == second.SeatId).Adventurers.Should().Equal("Rook");
    }

    [Fact]
    public async Task APhone_MayNotUseTheGmRoutes_NorAWrongToken()
    {
        var game = await StartGame();
        var (phone, code, _) = await JoinedPhone(game, ["Rook"]);

        (await phone.GetAsync($"/api/games/{game.Id}", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.GetAsync($"/api/games/{game.Id}/room/impersonate/Rook", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var stranger = Phone();
        stranger.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Seat", "nope");
        (await stranger.GetAsync($"/api/rooms/{code}/me", Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AClosedRoom_SendsThePhonesAway_ButKeepsTheSeatsForItsReopening()
    {
        var game = await StartGame();
        var (phone, code, seat) = await JoinedPhone(game, ["Rook"]);

        await _gm.DeleteAsync($"/api/games/{game.Id}/room", Token);
        (await phone.GetAsync($"/api/rooms/{code}/me", Token)).StatusCode.Should().Be(HttpStatusCode.Gone);

        var reopened = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token));
        reopened.Seats.Single().Should().BeEquivalentTo(new { Id = seat, Adventurers = new[] { "Rook" } });
        (await phone.GetAsync($"/api/rooms/{reopened.Code}/me", Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheGm_SeesAnAdventurerAsItsPlayerWould()
    {
        var game = await StartGame();

        var view = await Read<PlayerView>(await _gm.GetAsync($"/api/games/{game.Id}/room/impersonate/Nightingale", Token));

        view.Impersonated.Should().BeTrue();
        view.Adventurers.Select(a => a.Name).Should().Equal("Nightingale");
        (await _gm.GetAsync($"/api/games/{game.Id}/room/impersonate/Nobody", Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheGmsSecondScreen_ActsAsTheGm_ForItsGameOnly_UntilRevoked()
    {
        var game = await StartGame();
        var other = await StartGame();
        await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token);
        var screen = await Read<RoomEndpoints.ScreenResponse>(await _gm.PostAsync($"/api/games/{game.Id}/room/screen", null, Token));

        var tablet = Phone();
        tablet.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Screen", screen.Token);
        (await tablet.GetAsync($"/api/games/{game.Id}", Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await tablet.GetAsync($"/api/games/{other.Id}", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tablet.GetAsync("/api/games", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tablet.GetAsync("/api/scenarios", Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var room = await Read<RoomView>(await _gm.DeleteAsync($"/api/games/{game.Id}/room/screen", Token));
        room.HasScreen.Should().BeFalse();
        (await tablet.GetAsync($"/api/games/{game.Id}", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task APlayersRequest_WaitsForTheGm_ThenGoesIntoTheGameUnderTheirName()
    {
        var game = await StartGame();
        var (phone, code, _) = await JoinedPhone(game, ["Rook"]);
        var item = await GiveRookAPlank(game);

        var asked = await Read<PlayerView>(await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.MoveItem, item, "Rook", "equipped"), JsonDefaults.Options, Token));
        var request = asked.Requests.Single();
        request.Status.Should().Be(RequestStatus.Pending);
        (await Read<GameView>(await _gm.GetAsync($"/api/games/{game.Id}", Token))).State.Campaign.Party.Single(a => a.Name == "Rook").Pack.Should().ContainSingle("not applied before the GM says so");

        var room = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room/requests/{request.Id}/accept", null, Token));

        room.Requests.Single().Status.Should().Be(RequestStatus.Accepted);
        var after = await Read<GameView>(await _gm.GetAsync($"/api/games/{game.Id}", Token));
        after.State.Campaign.Party.Single(a => a.Name == "Rook").Equipped.Select(i => i.Id).Should().Contain(item);
        after.State.Log.Last(l => l.Code == "itemMoved").Args!["by"].Should().Be("Léa");
        (await _gm.PostAsync($"/api/games/{game.Id}/room/requests/{request.Id}/accept", null, Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task APlayer_MayNotAskForAnotherSeatsItem_NorForAnythingOutOfTheList()
    {
        var game = await StartGame();
        var (phone, code, _) = await JoinedPhone(game, ["Nightingale"]);
        var rooksItem = await GiveRookAPlank(game);

        (await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.MoveItem, rooksItem, "Nightingale"), JsonDefaults.Options, Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand("nextTurn"), JsonDefaults.Options, Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithAFreeInventory_MovingOnesOwnItem_AppliesAtOnce()
    {
        var game = await StartGame();
        var (phone, code, _) = await JoinedPhone(game, ["Rook"]);
        var item = await GiveRookAPlank(game);
        await Read<RoomView>(await _gm.PutAsJsonAsync($"/api/games/{game.Id}/room/settings", new RoomEndpoints.SettingsRequest(RoomAutonomy.FreeInventory), JsonDefaults.Options, Token));

        var view = await Read<PlayerView>(await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.MoveItem, item, "stash"), JsonDefaults.Options, Token));

        view.Encounter.Should().BeNull("the test game starts with its scene");
        view.Requests.Single().Status.Should().Be(RequestStatus.Applied);
        view.Stash.Select(i => i.Id).Should().Contain(item);
    }

    [Fact]
    public async Task APlayer_DeclaresTheDamageTheirAdventurerTook_TheGmAccepts()
    {
        var game = await StartGame();
        var (phone, code, _) = await JoinedPhone(game, ["Rook"]);

        var asked = await Read<PlayerView>(await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.Damage, Adventurer: "Rook", Amount: 3), JsonDefaults.Options, Token));
        await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room/requests/{asked.Requests.Single().Id}/accept", null, Token));

        var view = await Read<PlayerView>(await phone.GetAsync($"/api/rooms/{code}/me", Token));
        view.Adventurers.Single().Damage.Should().Be(3);
        (await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.Damage, Adventurer: "Nightingale", Amount: 3), JsonDefaults.Options, Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.BuyItem, Item: "hb:item/sword", Owner: "Rook"), JsonDefaults.Options, Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "no Store is open");
    }

    [Fact]
    public async Task APlayer_AsksForAFreeAdventurer_AndGetsItOnceTheGmAccepts()
    {
        var game = await StartGame();
        var (phone, code, seat) = await JoinedPhone(game, ["Rook"]);

        var asked = await Read<PlayerView>(await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.TakeAdventurer, Adventurer: "Nightingale"), JsonDefaults.Options, Token));
        var room = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room/requests/{asked.Requests.Single().Id}/accept", null, Token));

        room.Seats.Single(s => s.Id == seat).Adventurers.Should().Equal("Rook", "Nightingale");
        (await _gm.PostAsJsonAsync($"/api/games/{game.Id}/room/show", new RoomEndpoints.ShowRequest("../secret.json"), JsonDefaults.Options, Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _gm.PostAsJsonAsync($"/api/games/{game.Id}/room/show", new RoomEndpoints.ShowRequest("assets/act1/items/buckler.jpg", "Regardez"), JsonDefaults.Options, Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<string> GiveRookAPlank(GameView game)
    {
        var after = await Read<GameView>(await _gm.PostAsJsonAsync($"/api/games/{game.Id}/items", new { item = "hb:item/plank", owner = "Rook", slot = "pack" }, JsonDefaults.Options, Token));
        return after.State.Campaign.Party.Single(a => a.Name == "Rook").Pack.Last().Id;
    }

    private async Task<(HttpClient Phone, string Code, string Seat)> JoinedPhone(GameView game, IReadOnlyList<string> adventurers)
    {
        var room = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token));
        var phone = Phone();
        var joined = await Read<RoomEndpoints.JoinResponse>(await phone.PostAsJsonAsync($"/api/rooms/{room.Code}/seats", new RoomEndpoints.JoinRequest("Léa", null), JsonDefaults.Options, Token));
        await _gm.PutAsJsonAsync($"/api/games/{game.Id}/room/seats/{joined.SeatId}/adventurers", new RoomEndpoints.AssignRequest(adventurers), JsonDefaults.Options, Token);
        phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Seat", joined.SeatToken);
        return (phone, room.Code, joined.SeatId);
    }

    /// <summary>Another device of the Wi-Fi, as the Vite proxy forwards it.</summary>
    private HttpClient Phone()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", PhoneAddress);
        return client;
    }

    private async Task<GameView> StartGame() =>
        await Read<GameView>(await _gm.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Nightingale", "Rook"]), JsonDefaults.Options, Token));

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Token));
        return (await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, Token))!;
    }
}
