using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>An adventurer enters a hidden area: the fog lifts over its tiles, then its areaRevealed events fire.</summary>
public static class RevealArea
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/areas/{area}/reveal", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string area, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.RevealArea(area), cancellationToken)).ToResponse())
            .WithName("RevealArea");
}
