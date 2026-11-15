using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>A combatant is Defeated (pit, Dangerous Terrain…) whatever its Damage.</summary>
public static class DefeatCombatant
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/combatants/{instance}/defeat", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string instance, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Defeat(instance), cancellationToken)).ToResponse())
            .WithName("DefeatCombatant");
}
