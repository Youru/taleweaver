using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

/// <summary>The save file of a game, to download (it can be imported back).</summary>
public static class ExportGame
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id}/export", async Task<Results<FileContentHttpResult, ProblemHttpResult>> (string id, IGameStore games, CancellationToken cancellationToken) =>
            {
                if (await games.GetAsync(id, cancellationToken) is not { } game)
                {
                    return GameRunner.Errors.NotFound(id).ToProblem();
                }

                var name = $"{game.Scenario.Split('/')[^1]}-{game.UpdatedAt:yyyyMMdd-HHmm}.json";
                return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(game, JsonDefaults.FileOptions), "application/json", name);
            })
            .WithName("ExportGame");
}
