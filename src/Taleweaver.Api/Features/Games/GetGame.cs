using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

public static class GetGame
{
    public sealed class Handler(IGameStore games)
    {
        public async Task<Result<Game>> Handle(string id, CancellationToken cancellationToken) =>
            await games.GetAsync(id, cancellationToken) is { } game ? game : GameRunner.Errors.NotFound(id);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (string id, Handler handler, CancellationToken cancellationToken) =>
                (await handler.Handle(id, cancellationToken)).ToResponse())
            .WithName("GetGame");
}
