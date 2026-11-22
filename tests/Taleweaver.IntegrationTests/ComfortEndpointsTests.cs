using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Taleweaver.IntegrationTests;

/// <summary>The GM's comfort through the API (play-spec lot 5), and what reaches the phones (lot 6).</summary>
public class ComfortEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task M2_Revert_GoesBackBeforeTheActionOfThatLine()
    {
        var id = await StartAsync();
        var fighting = await Send(HttpMethod.Post, $"/api/games/{id}/answer", new { option = "continue" });
        await Send(HttpMethod.Post, $"/api/games/{id}/encounter/combatants/brute-1/damage", new { amount = 1 });
        var last = await Send(HttpMethod.Post, $"/api/games/{id}/encounter/combatants/brute-1/damage", new { amount = 1 });

        var stale = await Raw(HttpMethod.Post, $"/api/games/{id}/revert", new { actions = 1, outOf = 2 });
        var reverted = await Send(HttpMethod.Post, $"/api/games/{id}/revert", new { actions = 1, outOf = 3 });

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        last.GetProperty("actions").GetArrayLength().Should().Be(3);
        reverted.GetProperty("state").GetRawText().Should().Be(fighting.GetProperty("state").GetRawText());
        reverted.GetProperty("actions").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task M4_M6_M17_NotesAndSeats_OutliveAnUndo()
    {
        var id = await StartAsync();
        await Send(HttpMethod.Post, $"/api/games/{id}/answer", new { option = "continue" });
        await Send(HttpMethod.Post, $"/api/games/{id}/session/notes", new { text = "Stopped at the bridge", kind = "note" });
        await Send(HttpMethod.Put, $"/api/games/{id}/session/players", new { players = new[] { new { adventurer = "Rook", player = "Paul", present = false, tonight = "Anna" } } });

        var undone = await Send(HttpMethod.Post, $"/api/games/{id}/undo");

        var session = undone.GetProperty("table");
        session.GetProperty("notes")[0].GetProperty("text").GetString().Should().Be("Stopped at the bridge");
        session.GetProperty("players")[0].GetProperty("tonight").GetString().Should().Be("Anna");
        session.GetProperty("sessions").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task M16_Pause_TellsThePhone_AndResumesTheSameGame()
    {
        var id = await StartAsync();
        var before = await Send(HttpMethod.Get, $"/api/games/{id}");
        var (code, seat) = await JoinAsync(id);

        await Send(HttpMethod.Put, $"/api/games/{id}/session/pause", new { paused = true });
        var phone = await Send(HttpMethod.Get, $"/api/rooms/{code}/me", seat: seat);
        var resumed = await Send(HttpMethod.Put, $"/api/games/{id}/session/pause", new { paused = false });

        phone.GetProperty("paused").GetBoolean().Should().BeTrue();
        resumed.GetProperty("state").GetRawText().Should().Be(before.GetProperty("state").GetRawText());
    }

    [Fact]
    public async Task J4_J4b_SpendingAppliesAtOnce_AndTheEndOfTheTurnWaits()
    {
        var id = await StartAsync();
        await Send(HttpMethod.Post, $"/api/games/{id}/answer", new { option = "continue" });
        var (code, seat) = await JoinAsync(id);

        var spent = await Send(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "stamina", adventurer = "Rook", amount = 0 }, seat);
        var marked = await Send(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "mark", adventurer = "Rook", card = "card:Rook", effect = "exhausted", active = true }, seat);
        var madeUp = await Raw(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "mark", adventurer = "Rook", card = "whatever", effect = "used", active = true }, seat);
        var notMine = await Raw(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "stamina", adventurer = "Nightingale", amount = 0 }, seat);
        var early = await Raw(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "endTurn", adventurer = "Rook" }, seat);

        spent.GetProperty("requests").GetArrayLength().Should().Be(0, "nothing waits for the GM");
        marked.GetProperty("adventurers")[0].GetProperty("exhaustedCards")[0].GetString().Should().Be("card:Rook");
        madeUp.IsSuccessStatusCode.Should().BeFalse();
        notMine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        early.IsSuccessStatusCode.Should().BeFalse("no turn of theirs is under way");
    }

    [Fact]
    public async Task M14_J15_AClaimWaits_AndAllAreAcceptedInOneGesture()
    {
        var id = await StartAsync();
        var (code, seat) = await JoinAsync(id);
        var given = await Send(HttpMethod.Post, $"/api/games/{id}/items", new { item = "hb:item/sword", owner = "unassigned" });
        var loot = given.GetProperty("state").GetProperty("campaign").GetProperty("unassigned")[0].GetProperty("id").GetString();

        var asked = await Send(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "claim", adventurer = "Rook", item = loot }, seat);
        var again = await Raw(HttpMethod.Post, $"/api/rooms/{code}/me/requests", new { type = "claim", adventurer = "Rook", item = loot }, seat);
        var accepted = await Send(HttpMethod.Post, $"/api/games/{id}/room/requests/accept-all", new { });

        asked.GetProperty("requests").EnumerateArray().Last().GetProperty("status").GetString().Should().Be("pending");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        accepted.GetProperty("requests")[0].GetProperty("status").GetString().Should().Be("accepted");
    }

    [Fact]
    public async Task M17_TheRulebook_OpensToThePhones_NotTheGmsRoutes()
    {
        await JoinAsync(await StartAsync());
        async Task<HttpStatusCode> FromAPhone(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Forwarded-For", "192.168.1.20");
            return (await _client.SendAsync(request, Token)).StatusCode;
        }

        // Filtered: RulebookForPhonesTests
        (await FromAPhone("/api/rulebook")).Should().Be(HttpStatusCode.OK);
        (await FromAPhone("/api/games")).Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> StartAsync() =>
        (await Send(HttpMethod.Post, "/api/games", new { scenario = "hb:scenario/meadow-ambush", party = new[] { "Nightingale", "Rook" } })).GetProperty("id").GetString()!;

    /// <summary>A room, and a phone holding Rook.</summary>
    private async Task<(string Code, string Seat)> JoinAsync(string id)
    {
        var code = (await Send(HttpMethod.Post, $"/api/games/{id}/room")).GetProperty("code").GetString()!;
        var joined = await Send(HttpMethod.Post, $"/api/rooms/{code}/seats", new { displayName = "Paul" });
        await Send(HttpMethod.Put, $"/api/games/{id}/room/seats/{joined.GetProperty("seatId").GetString()}/adventurers", new { adventurers = new[] { "Rook" } });
        return (code, joined.GetProperty("seatToken").GetString()!);
    }

    private Task<HttpResponseMessage> Raw(HttpMethod method, string url, object? body = null, string? seat = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        if (seat is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Seat {seat}");
        }

        return _client.SendAsync(request, Token);
    }

    private async Task<JsonElement> Send(HttpMethod method, string url, object? body = null, string? seat = null)
    {
        var response = await Raw(method, url, body, seat);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Token));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Token);
    }
}
