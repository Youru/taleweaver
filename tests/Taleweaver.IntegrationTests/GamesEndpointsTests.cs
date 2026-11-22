using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Encounters;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Storage;

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

    [Fact]
    public async Task MoveFigure_CombatantAndAdventurer_SavesTheirSquares()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));

        await _client.PutAsJsonAsync($"/api/games/{game.Id}/encounter/figures/brute-1/position", new { x = 4, y = 3 }, Token);
        var response = await _client.PutAsJsonAsync($"/api/games/{game.Id}/encounter/figures/adventurer:Rook/position", new { x = 1, y = 5 }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = (await Read(response)).State.Encounter!;
        state.Combatants.Single(c => c.Instance == "brute-1").Position.Should().Be(new Cell(4, 3));
        state.Adventurers.Single(a => a.Name == "Rook").Position.Should().Be(new Cell(1, 5));
    }

    [Fact]
    public async Task MoveFigure_OutsideTheBoard_ReturnsValidationProblem()
    {
        var game = await StartAsync();

        var response = await _client.PutAsJsonAsync($"/api/games/{game.Id}/encounter/figures/brute-1/position", new { x = -1, y = 3 }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MoveInitiativeCard_LastCardToTheFront_SavesTheNewOrder()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        var track = (await PostAsync($"/api/games/{game.Id}/encounter/next-turn")).State.Encounter!.InitiativeTrack;

        var response = await _client.PostAsJsonAsync($"/api/games/{game.Id}/encounter/initiative/moves", new MoveInitiativeCard.Request(track[^1], -9), JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Read(response)).State.Encounter!.InitiativeTrack[0].Should().Be(track[^1]);
    }

    [Fact]
    public async Task TargetCombatant_WithoutDamage_IsKeptForAggro()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));

        var response = await _client.PostAsync($"/api/games/{game.Id}/encounter/combatants/brute-1/target", null, Token);

        (await Read(response)).State.Encounter!.Combatants.Single(c => c.Instance == "brute-1").Attacked.Should().BeTrue();
    }

    [Fact]
    public async Task MoveInitiativeCard_NoPlaces_ReturnsValidationProblem()
    {
        var game = await StartAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.Id}/encounter/initiative/moves", new MoveInitiativeCard.Request("adventurer:Rook", 0), JsonDefaults.Options, Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGame_SavedByAnOlderEngine_KeepsItsState()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        var store = factory.Services.GetRequiredService<IGameStore>();
        var saved = (await store.GetAsync(game.Id, Token))!;
        saved.State.Encounter!.Combatants.ForEach(c => c.Position = null);
        await store.SaveAsync(saved with { EngineVersion = 0 }, Token);

        var loaded = await _client.GetFromJsonAsync<GameView>($"/api/games/{game.Id}", JsonDefaults.Options, Token);

        loaded!.State.Encounter!.Combatants.Single().Position.Should().BeNull();
        loaded.CanUndo.Should().BeFalse();
        loaded.UndoBlocked.Should().BeTrue();
        (await store.GetAsync(game.Id, Token))!.EngineVersion.Should().Be(0, "reading writes nothing");
    }

    [Fact]
    public async Task Undo_AfterAnEngineUpdate_StopsAtTheStateFoundByTheUpdate()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        var old = await SaveAsOlderEngineAsync(game.Id);
        using var scope = factory.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<GameRunner>();

        (await runner.ExecuteAsync(game.Id, GameCommand.NextTurn(), Token)).IsSuccess.Should().BeTrue();
        var undone = await runner.UndoAsync(game.Id, Token);

        Json(undone.Value.State).Should().Be(Json(old.State));
        (await runner.UndoAsync(game.Id, Token)).Error.Should().Be(GameRunner.Errors.UndoBlocked);
    }

    [Fact]
    public async Task Import_OfAnOlderEngine_KeepsItsStateAndItsBaseline()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        var old = await SaveAsOlderEngineAsync(game.Id);
        var exported = await _client.GetByteArrayAsync($"/api/games/{game.Id}/export", Token);

        var response = await _client.PostAsync("/api/games/import", new ByteArrayContent(exported) { Headers = { ContentType = new("application/json") } }, Token);

        var imported = await Read(response);
        Json(imported.State).Should().Be(Json(old.State));
        imported.UndoBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task RestartFromLastEncounter_AfterAnUpdate_StartsFromItsCheckpoint()
    {
        var game = await StartAsync();
        await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        var old = await SaveAsOlderEngineAsync(game.Id);
        var store = factory.Services.GetRequiredService<IGameStore>();
        var finished = old.State.Clone();
        finished.Status = GameStatus.Finished;
        await store.SaveAsync(old with { State = finished }, Token);
        using var scope = factory.Services.CreateScope();

        var again = await scope.ServiceProvider.GetRequiredService<GameRunner>().RestartAsync(game.Id, fromLastEncounter: true, Token);

        Json(again.Value.State).Should().Be(Json(old.State));
    }

    /// <summary>The game as an older engine would have left it: a state the history alone would not give.</summary>
    private async Task<Game> SaveAsOlderEngineAsync(string id)
    {
        var store = factory.Services.GetRequiredService<IGameStore>();
        var saved = (await store.GetAsync(id, Token))!;
        saved.State.Encounter!.Combatants.ForEach(c => c.Position = null);
        saved.EncounterStart?.State.Encounter!.Combatants.ForEach(c => c.Position = null);
        var old = saved with { EngineVersion = 0 };
        await store.SaveAsync(old, Token);
        return old;
    }

    private static string Json(GameState state) => System.Text.Json.JsonSerializer.Serialize(state, JsonDefaults.Options);

    [Fact]
    public async Task AfterAnEnding_TheActIsExported_AndTheGameStartsAgainFromTheLastEncounterOrTheBeginning()
    {
        var game = await Read(await _client.PostAsJsonAsync(
            "/api/games", new StartGame.Request("hb:scenario/meadow-ambush", ["Nightingale", "Rook"], new GameOptions { TougherMobs = true }), JsonDefaults.Options, Token));
        game.State.Options!.TougherMobs.Should().BeTrue();
        (await _client.GetAsync($"/api/games/{game.Id}/act-export", Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        game = await PostAsync($"/api/games/{game.Id}/encounter/end-conditions/win");
        while (game.State.Status != GameStatus.Finished)
        {
            game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request(game.State.Prompt!.Options[0].Id, null));
        }

        var export = await _client.GetStringAsync($"/api/games/{game.Id}/act-export", Token);
        export.Should().Contain(ExportAct.Format).And.Contain("Nightingale");

        var retry = await Read(await _client.PostAsJsonAsync($"/api/games/{game.Id}/restart", new RestartGame.Request(RestartGame.FromLastEncounter), JsonDefaults.Options, Token));
        retry.Id.Should().NotBe(game.Id);
        retry.State.Encounter!.Ended.Should().BeFalse();
        retry.State.Options!.TougherMobs.Should().BeTrue();

        var again = await Read(await _client.PostAsJsonAsync($"/api/games/{game.Id}/restart", new RestartGame.Request(RestartGame.FromBeginning), JsonDefaults.Options, Token));
        again.State.Encounter.Should().BeNull();
        again.State.Options!.TougherMobs.Should().BeTrue();
    }

    [Fact]
    public async Task Play_WhatTheGmKeeps_IsSavedAndUndone()
    {
        var game = await StartAsync();
        game = await PostAsync($"/api/games/{game.Id}/answer", new AnswerQuestion.Request("continue", null));
        var play = $"/api/games/{game.Id}/play";

        game = await PostAsync($"{play}/actions", new { figure = "adventurer:Rook", action = "dodge" });
        game.State.Campaign.Party.Single(a => a.Name == "Rook").Stamina.Should().Be(2);
        game = await PostAsync($"{play}/initiative/deal");
        game.State.Encounter!.TrackDealt.Should().BeTrue();
        game = await PostAsync($"{play}/reactions", new { figure = "adventurer:Rook", kind = "break", against = "brute-1" });
        game.State.Encounter!.Reactions.Should().ContainSingle();

        var undone = await PostAsync($"/api/games/{game.Id}/undo");

        undone.State.Encounter!.Reactions.Should().BeEmpty();
        undone.State.Encounter.TrackDealt.Should().BeTrue();
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
