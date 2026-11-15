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
        IReadOnlyDictionary<string, FigurePiece> Figures);

    public sealed record TilePiece(string Name, int W, int H, string? Image);

    public sealed record TokenPiece(LocalizedText Name, string Category, string? Size, string? Image, IReadOnlyDictionary<string, string>? Colors);

    public sealed record FigurePiece(string Image, string? BaseSize);

    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<Response> Handle(CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            var tiles = new Dictionary<string, TilePiece>();
            var tokens = new Dictionary<string, TokenPiece>();
            var figures = new Dictionary<string, FigurePiece>();
            foreach (var pack in catalog.Packs)
            {
                foreach (var tile in pack.Tiles)
                {
                    tiles[tile.Id] = new TilePiece(tile.Name, tile.Size?.W ?? 6, tile.Size?.H ?? 6, tile.Image);
                }

                foreach (var token in pack.Tokens)
                {
                    tokens[token.Id] = new TokenPiece(token.Name, token.Category, token.Size, token.Image, token.Colors);
                }

                foreach (var combatant in pack.Combatants.Where(c => c.Miniature is not null))
                {
                    figures.TryAdd(combatant.Family, new FigurePiece(combatant.Miniature!, combatant.BaseSize));
                }
            }

            return new Response(tiles, tokens, figures);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/board", async Task<Ok<Response>> (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("GetBoardPieces");
}
