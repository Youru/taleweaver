using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>The Command cards the GM may bring by hand. The GM's only: not on the phones' list (the hidden ones are spoilers).</summary>
public static class GetAllies
{
    public sealed record AllyCard(string Id, LocalizedText Name, string? Image, bool Hidden);

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/allies", async Task<Ok<IReadOnlyList<AllyCard>>> (CatalogProvider catalogs, CancellationToken cancellationToken) =>
                TypedResults.Ok<IReadOnlyList<AllyCard>>((await catalogs.GetAsync(cancellationToken)).Combatants
                    .Where(c => c.Kind == "command" && !c.Tags.Contains("CONDUIT"))
                    .Select(c => new AllyCard(c.Id, c.Name, c.Image, c.Source?.StartsWith("hidden-cards", StringComparison.Ordinal) == true))
                    .ToList()))
            .WithName("GetAllies");
}
