using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>A combatant was the target of an opponent's Attack, Spell or Ability, even without Damage (Aggro, rulebook p. 60).</summary>
public static class TargetCombatant
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/combatants/{instance}/target", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string instance, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Target(instance), cancellationToken)).ToResponse())
            .WithName("TargetCombatant");
}
