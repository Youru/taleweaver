using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>The reference tables of the rulebook (p. 89-100), as the packs give them: the in-game help.</summary>
public static class GetReference
{
    public sealed record Response(
        IReadOnlyList<RuleEntry> Effects,
        IReadOnlyList<RuleEntry> AbilityTokens,
        IReadOnlyList<RuleEntry> TerrainTypes,
        IReadOnlyList<RuleEntry> Tags,
        IReadOnlyList<RuleEntry> CombatantTokens);

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/reference", async Task<Ok<Response>> (CatalogProvider catalogs, CancellationToken cancellationToken) =>
            {
                var rules = (await catalogs.GetAsync(cancellationToken)).Packs.Select(p => p.Rules).OfType<PackRules>().ToList();
                List<RuleEntry> All(Func<PackRules, IReadOnlyList<RuleEntry>> table) => rules.SelectMany(table).DistinctBy(e => e.Id).ToList();
                return TypedResults.Ok(new Response(All(r => r.Effects), All(r => r.AbilityTokens), All(r => r.TerrainTypes), All(r => r.Tags), All(r => r.CombatantTokens)));
            })
            .WithName("GetReference");
}
