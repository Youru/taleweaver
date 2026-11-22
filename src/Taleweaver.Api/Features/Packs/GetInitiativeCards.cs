using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.Api.Features.Packs;

/// <summary>
/// The initiative cards of the enabled packs (rulebook p. 36), so that the track of the round is shown with
/// the cards the players deal: their picture, and whether they are an adventurer's.
/// </summary>
public static class GetInitiativeCards
{
    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<IReadOnlyList<InitiativeCard>> Handle(CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            return catalog.Packs.SelectMany(p => p.InitiativeCards).DistinctBy(c => c.Id).ToList();
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/initiative", async Task<Ok<IReadOnlyList<InitiativeCard>>> (Handler handler, HttpContext http, PhoneCards phones, CancellationToken cancellationToken) =>
            {
                var cards = await handler.Handle(cancellationToken);
                return TypedResults.Ok<IReadOnlyList<InitiativeCard>>(await phones.ForAsync(http, cancellationToken) is { } sight ? cards.Where(sight.Sees).ToList() : cards);
            })
            .WithName("GetInitiativeCards");
}
