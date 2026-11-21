using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>
/// The Item Upgrades (rulebook p. 28) and the Discipline cards (p. 24, 33) of the enabled packs: what the Shop and
/// Train of a Story Round sells for Gold and for XP.
/// </summary>
public static class GetUpgradesAndDisciplines
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/item-upgrades", async Task<Ok<IReadOnlyList<ItemUpgrade>>> (CatalogProvider catalogs, CancellationToken cancellationToken) =>
                TypedResults.Ok<IReadOnlyList<ItemUpgrade>>((await catalogs.GetAsync(cancellationToken)).Packs
                    .SelectMany(p => p.ItemUpgrades).GroupBy(u => u.Id).Select(g => g.Last()).ToList()))
            .WithName("GetItemUpgrades");
        group.MapGet("/disciplines", async Task<Ok<IReadOnlyList<Discipline>>> (CatalogProvider catalogs, CancellationToken cancellationToken) =>
                TypedResults.Ok<IReadOnlyList<Discipline>>((await catalogs.GetAsync(cancellationToken)).Packs
                    .SelectMany(p => p.Disciplines).GroupBy(d => d.Id).Select(g => g.Last()).ToList()))
            .WithName("GetDisciplines");
    }
}
