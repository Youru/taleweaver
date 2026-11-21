using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>
/// The item cards of the enabled packs (rulebook ch. 6): what the GM searches to hand out a card bought or looted,
/// and what the party's items show (picture, both sides, tier, type, price, text).
/// </summary>
public static class GetItems
{
    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<IReadOnlyList<Item>> Handle(CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);

            // A later pack's card replaces an earlier one with the same id.
            return catalog.Packs.SelectMany(p => p.Items).GroupBy(i => i.Id).Select(g => g.Last()).ToList();
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/items", async Task<Ok<IReadOnlyList<Item>>> (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("GetItems");
}
