using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.IntegrationTests;

public class GamesEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StartGame_KnownScenario_ReadsTheFirstSceneAndAsksToContinue()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Nightingale", "Rook"]), JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var game = await Read(response);
        game.State.Log[0].Code.Should().Be("scene");
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Continue);
        response.Headers.Location!.ToString().Should().Be($"/api/games/{game.Id}");
    }

    [Fact]
    public async Task StartGame_UnknownScenario_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/missing", ["Rook"]), JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartGame_DuplicateAdventurers_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Rook", "rook"]), JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlayEncounter_TurnsAiAndObjective_EndsTheScenario()
    {
        var game = await StartAsync();
        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        game.State.Encounter!.Combatants.Should().ContainSingle().Which.Card.Should().Be("hb:combatant/brute-mundane");

        for (var turn = 0; game.State.Prompt is null && turn < 10; turn++)
        {
            game = await PostAsync($"/api/games/{game.Id}/encounter/next-turn");
        }

        game.State.Prompt!.Kind.Should().Be(PromptKinds.AiCondition);
        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("yes", null));
        game.State.Log.Should().Contain(l => l.Code == "aiInstruction");

        game = await PostAsync($"/api/games/{game.Id}/encounter/combatants/brute-1/damage", new { amount = 3 });
        game.State.Encounter!.Combatants[0].Damage.Should().Be(3);
        game = await PostAsync($"/api/games/{game.Id}/encounter/tokens/objective:red/activation");
        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));

        game.State.Status.Should().Be(GameStatus.Finished);
    }

    [Fact]
    public async Task UndoLastAction_AfterDamage_RestoresTheState()
    {
        var game = await StartAsync();
        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        await PostAsync($"/api/games/{game.Id}/encounter/combatants/brute-1/damage", new { amount = 5 });

        var undone = await PostAsync($"/api/games/{game.Id}/undo");

        undone.State.Encounter!.Combatants[0].Damage.Should().Be(0);
        undone.State.Log.Should().HaveCount(game.State.Log.Count);
    }

    [Fact]
    public async Task UndoLastAction_NewGame_ReturnsConflict()
    {
        var game = await StartAsync();

        var response = await _client.PostAsync($"/api/games/{game.Id}/undo", null, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NextTurn_WhileAQuestionIsPending_ReturnsConflictWithCode()
    {
        var game = await StartAsync();

        var response = await _client.PostAsync($"/api/games/{game.Id}/encounter/next-turn", null, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        problem!.Title.Should().Be("Games.NotInEncounter");
    }

    [Fact]
    public async Task ApplyDamage_UnknownCombatant_ReturnsNotFound()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));

        var response = await _client.PostAsJsonAsync($"/api/games/{game.Id}/encounter/combatants/dragon-1/damage", new { amount = 1 }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportThenImport_GameInProgress_GivesTheSameStateUnderANewId()
    {
        var game = await StartAsync();
        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        game = await PostAsync($"/api/games/{game.Id}/encounter/next-turn");

        var exported = await _client.GetByteArrayAsync($"/api/games/{game.Id}/export", Token);
        var response = await _client.PostAsync("/api/games/import", new ByteArrayContent(exported) { Headers = { ContentType = new("application/json") } }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var imported = await Read(response);
        imported.Id.Should().NotBe(game.Id);
        System.Text.Json.JsonSerializer.Serialize(imported.State).Should().Be(System.Text.Json.JsonSerializer.Serialize(game.State));
    }

    [Fact]
    public async Task ListAndDelete_SavedGame_DisappearsFromTheList()
    {
        var game = await StartAsync();

        var deleted = await _client.DeleteAsync($"/api/games/{game.Id}", Token);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var games = await _client.GetFromJsonAsync<List<ListGames.GameSummary>>("/api/games", JsonDefaults.Options, Token);
        games.Should().NotContain(g => g.Id == game.Id);
        (await _client.GetAsync($"/api/games/{game.Id}", Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<GameView> StartAsync() =>
        await Read(await _client.PostAsJsonAsync("/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Nightingale", "Rook"]), JsonDefaults.Options, Token));

    private async Task<GameView> PostAsync(string url, object? body = null)
    {
        var response = body is null ? await _client.PostAsync(url, null, Token) : await _client.PostAsJsonAsync(url, body, JsonDefaults.Options, Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Token));
        return await Read(response);
    }

    private static async Task<GameView> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<GameView>(JsonDefaults.Options, Token))!;
}
