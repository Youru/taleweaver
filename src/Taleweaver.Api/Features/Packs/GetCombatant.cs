using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>A combatant card with its stats, AI steps and images.</summary>
public static class GetCombatant
{
    public static class Errors
    {
        public static Error NotFound(string id) => Error.NotFound("Packs.CombatantNotFound", $"No combatant '{id}' in the enabled packs.");
    }

    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<Result<Combatant>> Handle(string id, CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            return catalog.FindCombatant(id) is { } combatant ? combatant : Errors.NotFound(id);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{pack}/combatants/{name}", async Task<Results<Ok<Combatant>, ProblemHttpResult>> (
                string pack, string name, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle($"{pack}:combatant/{name}", cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
            })
            .WithName("GetCombatant");
}
