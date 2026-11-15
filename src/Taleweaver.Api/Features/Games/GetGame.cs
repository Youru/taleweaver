using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

public static class GetGame
{
    /// <summary>Loads through the runner, so that a game saved by an older engine comes back up to date.</summary>
    public sealed class Handler(GameRunner runner)
    {
        public Task<Result<Game>> Handle(string id, CancellationToken cancellationToken) => runner.LoadAsync(id, cancellationToken);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (string id, Handler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(id, cancellationToken)).ToResponse())
            .WithName("GetGame");
}
