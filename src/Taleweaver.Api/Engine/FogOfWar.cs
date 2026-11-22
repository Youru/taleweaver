using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>The fog of war (data model §3.4), read the same way by the engine and by what the players are sent.</summary>
public static class FogOfWar
{
    /// <summary>The maps on the table: the encounter's (unless a NAVIGATE cleared it), then the diagrams placed.</summary>
    public static IEnumerable<BoardMap> MapsOnTable(Node node, EncounterState encounter)
    {
        var diagrams = node.Diagrams.Where(d => encounter.PlacedDiagrams.Contains(d.Id)).Select(d => d.Map);
        return (encounter.MapCleared ? diagrams : new[] { node.Map }.Concat(diagrams)).OfType<BoardMap>();
    }

    /// <summary>The area still hidden that covers a square, or null when the square is in view.</summary>
    public static string? HiddenAreaAt(Node node, EncounterState encounter, Catalog catalog, Cell square)
    {
        var maps = MapsOnTable(node, encounter).ToList();
        var hidden = maps.SelectMany(m => m.Areas).Select(a => a.Id).Except(encounter.RevealedAreas).ToHashSet();
        if (hidden.Count == 0)
        {
            return null;
        }

        // A tile laid again at the same place by a diagram replaces the first one, as on the table's drawing
        var tiles = maps.SelectMany(m => m.Tiles).GroupBy(t => (t.Ref, t.X, t.Y)).Select(g => g.Last());
        return tiles.FirstOrDefault(t => t.Area is { } area && hidden.Contains(area) && Covers(t, catalog.FindTile(t.Ref)?.Size, square))?.Area;
    }

    private static bool Covers(MapTile tile, TileSize? size, Cell square) =>
        square.X >= tile.X && square.X < tile.X + (size?.W ?? Tile.SquaresPerSide)
        && square.Y >= tile.Y && square.Y < tile.Y + (size?.H ?? Tile.SquaresPerSide);
}
