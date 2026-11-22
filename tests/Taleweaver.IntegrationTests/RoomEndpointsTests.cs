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
        room.Code.Should().MatchRegex("^[A-HJKMNP-Z]{6}$");

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
            ["version", "gameId", "scenarioTitle", "roomCode", "seat", "impersonated", "adventurers", "party", "gold", "stash", "unassigned", "encounter", "log", "requests", "autonomy", "shop", "upgrades", "preparation", "cards", "paused"]);
        json.Should().NotContain("\"prompt\"").And.NotContain("hiddenText").And.NotContain("\"variables\"").And.NotContain("aiSteps");
        document.RootElement.GetProperty("log").EnumerateArray().Select(l => l.GetProperty("entry").GetProperty("code").GetString())
            .Should().NotBeEmpty().And.OnlyContain(code => PlayerViewProjector.PublicLogCodes.Contains(code!));
        document.RootElement.GetProperty("party").EnumerateArray().SelectMany(p => p.EnumerateObject().Select(o => o.Name)).Should().NotContain(["equipped", "pack", "disciplines", "xp"]);
    }

    [Fact]
    public async Task BeforeTheDeparture_PhonesChooseAtTheSameMoment_AndNoChoiceIsLost()
    {
        var game = await Read<GameView>(await _gm.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", []), JsonDefaults.Options, Token));
        var room = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token));
        var phones = new List<HttpClient>();
        foreach (var name in new[] { "Ana", "Ben", "Chloe", "Dan", "Eve" })
        {
            var phone = Phone();
            var joined = await Read<RoomEndpoints.JoinResponse>(await phone.PostAsJsonAsync($"/api/rooms/{room.Code}/seats", new RoomEndpoints.JoinRequest(name, null), JsonDefaults.Options, Token));
            joined.View.Preparation.Should().NotBeNull();
            phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Seat", joined.SeatToken);
            phones.Add(phone);
        }

        var answers = await Task.WhenAll(phones.Select((phone, i) => phone.PostAsJsonAsync(
            $"/api/rooms/{room.Code}/me/requests", new PlayerCommand(PlayerCommand.ChooseAdventurer, Adventurer: $"Hero {i}"), JsonDefaults.Options, Token)));

        answers.Should().OnlyContain(a => a.IsSuccessStatusCode);
        var prepared = await Read<GameView>(await _gm.GetAsync($"/api/games/{game.Id}", Token));
        prepared.State.Campaign.Party.Should().HaveCount(5);
        var seats = await Read<RoomView>(await _gm.GetAsync($"/api/games/{game.Id}/room", Token));
        seats.Seats.Should().OnlyContain(s => s.Adventurers.Count == 1);
    }

    [Fact]
    public async Task BeforeTheDeparture_AnAdventurerIsOnePlayers_AndOnlyTheGmSetsOut()
    {
        var game = await Read<GameView>(await _gm.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", []), JsonDefaults.Options, Token));
        var room = await Read<RoomView>(await _gm.PostAsync($"/api/games/{game.Id}/room", null, Token));
        var responses = new List<HttpStatusCode>();
        HttpClient phone = null!;
        foreach (var name in new[] { "Ana", "Ben" })
        {
            phone = Phone();
            var joined = await Read<RoomEndpoints.JoinResponse>(await phone.PostAsJsonAsync($"/api/rooms/{room.Code}/seats", new RoomEndpoints.JoinRequest(name, null), JsonDefaults.Options, Token));
            phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Seat", joined.SeatToken);
            var chosen = await phone.PostAsJsonAsync($"/api/rooms/{room.Code}/me/requests", new PlayerCommand(PlayerCommand.ChooseAdventurer, Adventurer: "Hero"), JsonDefaults.Options, Token);
            responses.Add(chosen.StatusCode);
        }

        responses.Should().Equal(HttpStatusCode.OK, HttpStatusCode.Conflict);
        (await phone.PostAsync($"/api/games/{game.Id}/preparation/departure", null, Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var setOut = await Read<GameView>(await _gm.PostAsync($"/api/games/{game.Id}/preparation/departure", null, Token));
        setOut.State.Preparation.Should().BeNull();
        setOut.Party.Should().Equal("Hero");
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

    [Fact]
    public async Task AForgedForwardedHeader_DoesNotMakeAPhoneTheGm()
    {
        using var forged = factory.CreateClient();
        forged.DefaultRequestHeaders.Add("X-Forwarded-For", $"127.0.0.1,{PhoneAddress}");

        (await forged.GetAsync("/api/games", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TryingCodes_IsStoppedAfterTenUnknownRooms()
    {
        using var guesser = factory.CreateClient();
        guesser.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.77");
        for (var i = 0; i < CodeGuessLimiter.MaxFailures; i++)
        {
            (await guesser.GetAsync("/api/rooms/ZZZZZZ", Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        (await guesser.GetAsync("/api/rooms/ZZZZZZ", Token)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task APhone_MayNotSearchTheWholeCatalogue()
    {
        var game = await StartGame();
        var (phone, _, _) = await JoinedPhone(game, ["Rook"]);

        (await phone.GetAsync("/api/catalog", Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.GetAsync("/api/catalog/items", Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ASeatSentAway_LosesItsPendingRequests()
    {
        var game = await StartGame();
        var (phone, code, seat) = await JoinedPhone(game, []);
        await Read<PlayerView>(await phone.PostAsJsonAsync($"/api/rooms/{code}/me/requests", new PlayerCommand(PlayerCommand.TakeAdventurer, Adventurer: "Rook"), JsonDefaults.Options, Token));

        var room = await Read<RoomView>(await _gm.DeleteAsync($"/api/games/{game.Id}/room/seats/{seat}", Token));

        room.Requests.Should().OnlyContain(r => r.Status == RequestStatus.Cancelled);
        (await phone.GetAsync($"/api/rooms/{code}/me", Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
