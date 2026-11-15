using Microsoft.AspNetCore.Http.HttpResults;

namespace Taleweaver.Api.Features.Games;

/// <summary>Cancels the last command of the GM (the game is replayed without it).</summary>
public static class UndoLastAction
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id}/undo", async Task<Results<Ok<GameView>, ProblemHttpResult>> (string id, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.UndoAsync(id, cancellationToken)).ToResponse())
            .WithName("UndoLastAction");
}
