using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Games;

/// <summary>The Hidden Cards this game revealed, and only those: the others stay secret.</summary>
public static class GetHiddenCards
{
    public sealed record Picture(LocalizedText Name, string Image);

    /// <summary>No picture: the card is taken from the box.</summary>
    public sealed record Card(string Size, string Code, IReadOnlyList<Picture> Pictures);

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id}/hidden-cards", async Task<Results<Ok<List<Card>>, ProblemHttpResult>> (
                string id, GameRunner runner, CatalogProvider catalogs, CancellationToken cancellationToken) =>
            {
                var game = await runner.LoadAsync(id, cancellationToken);
                if (!game.IsSuccess)
                {
                    return game.Error!.ToProblem();
                }

                var catalog = await catalogs.GetAsync(cancellationToken);
                return TypedResults.Ok(game.Value.State.Campaign.RevealedHiddenCards.Distinct().Select(key =>
                {
                    var parts = key.Split(':', 2);
                    return new Card(parts[0], parts[^1], catalog.HiddenCard(key).Select(c => new Picture(c.Name, c.Image)).ToList());
                }).ToList());
            })
            .WithName("GetHiddenCards");
}
