using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

public static class DeleteGame
{
    public sealed class Handler(IGameStore games)
    {
        public async Task<Result> Handle(string id, CancellationToken cancellationToken) =>
            await games.DeleteAsync(id, cancellationToken) ? Result.Success() : GameRunner.Errors.NotFound(id);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id}", async Task<Results<NoContent, ProblemHttpResult>> (string id, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(id, cancellationToken);
                return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
            })
            .WithName("DeleteGame");
}
