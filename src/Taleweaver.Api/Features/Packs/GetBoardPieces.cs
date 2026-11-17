using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>What the map view needs to draw a board: tiles, token types and the figure of each family, from the enabled packs.</summary>
public static class GetBoardPieces
{
    public sealed record Response(
        IReadOnlyDictionary<string, TilePiece> Tiles,
        IReadOnlyDictionary<string, TokenPiece> Tokens,
        IReadOnlyDictionary<string, FigurePiece> Figures,
        IReadOnlyDictionary<string, FamilyPiece> Families);

    /// <summary>A tile side; <see cref="Terrain"/> and <see cref="Elevation"/>: printed squares, tile not rotated (see <see cref="Tile"/>).</summary>
    public sealed record TilePiece(
        string Name,
        int W,
        int H,
        string? Image,
        string? Theme = null,
        string? PairedSide = null,
        IReadOnlyList<string>? Terrain = null,
        IReadOnlyList<string>? Elevation = null);

    /// <summary>A token type; <see cref="Variants"/>: its other pictures, from its pack and from the other enabled packs.</summary>
    public sealed record TokenPiece(
        LocalizedText Name,
        string Category,
        string? Size,
        string? Image,
        IReadOnlyDictionary<string, string>? Colors,
        IReadOnlyList<string>? Variants = null);

    public sealed record FigurePiece(string Image, string? BaseSize);

    /// <summary>A family of combatants the setup can place (the card is chosen from the encounter's loot level).</summary>
    public sealed record FamilyPiece(LocalizedText Name, string Kind, string? BaseSize);

    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<Response> Handle(CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            var tiles = new Dictionary<string, TilePiece>();
            var tokens = new Dictionary<string, TokenPiece>();
            var figures = new Dictionary<string, FigurePiece>();
            var families = new Dictionary<string, FamilyPiece>();
            foreach (var pack in catalog.Packs)
            {
                foreach (var tile in pack.Tiles)
                {
                    tiles[tile.Id] = new TilePiece(
                        tile.Name,
                        tile.Size?.W ?? 6,
                        tile.Size?.H ?? 6,
                        tile.Image,
                        tile.Theme,
                        tile.PairedSide,
                        tile.Terrain.Count > 0 ? tile.Terrain : null,
                        tile.Elevation.Count > 0 ? tile.Elevation : null);
                }

                foreach (var token in pack.Tokens)
                {
                    tokens[token.Id] = new TokenPiece(token.Name, token.Category, token.Size, token.Image, token.Colors, VariantsOf(catalog, token));
                }

                foreach (var combatant in pack.Combatants)
                {
                    families.TryAdd(combatant.Family, new FamilyPiece(combatant.Name, combatant.Kind, combatant.BaseSize));
                }

                foreach (var combatant in pack.Combatants.Where(c => c.Miniature is not null))
                {
                    figures.TryAdd(combatant.Family, new FigurePiece(combatant.Miniature!, combatant.BaseSize));
                }

                // Every other figure by its name: the adventurers ("rook") are found this way.
                foreach (var (name, figure) in pack.Manifest.Figures)
                {
                    figures.TryAdd(name, new FigurePiece(figure.Image, figure.BaseSize));
                }
            }

            return new Response(tiles, tokens, figures, families);
        }

        /// <summary>The token's own variants, then those the enabled packs add to it.</summary>
        private static IReadOnlyList<string>? VariantsOf(Catalog catalog, TokenType token)
        {
            var variants = token.ImageVariants
                .Concat(catalog.Packs.SelectMany(p => p.TokenImageVariants.TryGetValue(token.Id, out var added) ? added : []))
                .Distinct()
                .ToList();
            return variants.Count > 0 ? variants : null;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/board", async Task<Ok<Response>> (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("GetBoardPieces");
}
