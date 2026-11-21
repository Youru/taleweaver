using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Items;

/// <summary>The card shows its other side: a DOUBLE item equipped on its back, or an item Flipped by its ability.</summary>
public static class FlipItem
{
    public sealed record Request(bool Flipped);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{item}/flipped", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string item, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.FlipItem(item, request.Flipped), cancellationToken)).ToResponse())
            .WithName("FlipItem");
}
