using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>An active end condition is met on the board (exit reached, all Adventurers Defeated…).</summary>
public static class ReachEndCondition
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/end-conditions/{endCondition}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string endCondition, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.ReachEndCondition(endCondition), cancellationToken)).ToResponse())
            .WithName("ReachEndCondition");
}
