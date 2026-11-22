using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

public static class GamesEndpoints
{
    public static IServiceCollection AddGamesFeature(this IServiceCollection services) =>
        services.AddScoped<GameRunner>()
            .AddScoped<StartGame.Handler>()
            .AddScoped<ListGames.Handler>()
            .AddScoped<GetGame.Handler>()
            .AddScoped<DeleteGame.Handler>()
            .AddScoped<ImportGame.Handler>();

    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games").WithTags("Games");
        StartGame.Map(group);
        ListGames.Map(group);
        GetGame.Map(group);
        AnswerQuestion.Map(group);
        UndoLastAction.Map(group);
        TableEndpoints.Map(group);
        DeleteGame.Map(group);
        ExportGame.Map(group);
        ImportGame.Map(group);
        RestartGame.Map(group);
        ExportAct.Map(group);
        GetHiddenCards.Map(group);
        return app;
    }

    /// <summary>Common ending of the game commands: the new state, or the problem.</summary>
    public static Results<Ok<GameView>, ProblemHttpResult> ToResponse(this Result<Game> result) =>
        result.IsSuccess ? TypedResults.Ok(GameView.Of(result.Value)) : result.Error!.ToProblem();
}
