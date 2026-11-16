using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>The Effects of the enabled packs (Poison, Barrier…): their text, polarity and when they resolve.</summary>
public static class GetEffects
{
    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<IReadOnlyList<RuleEntry>> Handle(CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            return catalog.Packs.SelectMany(p => p.Rules?.Effects ?? []).DistinctBy(e => e.Id).ToList();
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/effects", async Task<Ok<IReadOnlyList<RuleEntry>>> (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("GetEffects");
}
