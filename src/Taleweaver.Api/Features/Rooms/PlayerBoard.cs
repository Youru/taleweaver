using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// The table as a phone may draw it (room-spec §5.1, Table), cut on the server: under the fog, only that a tile is
/// there. No area name, no identifier of the scenario (they tell what is coming: "obj-seal").
/// </summary>
public sealed record PlayerBoard(
    IReadOnlyList<MapTile> Tiles,
    IReadOnlyList<Zone> Zones,
    IReadOnlyList<ElevationArea> Elevation,
    IReadOnlyList<BoardToken> Tokens,
    IReadOnlyList<PlayerFigure> Figures,
    IReadOnlyList<PlayerAdventurerFigure> Adventurers,
    IReadOnlyList<PlayerFigure> Allies)
{
    /// <summary>The fog of every hidden tile, and the piece drawn for it.</summary>
    public const string Fog = "fog";

    public static PlayerBoard? Of(EncounterState encounter, Scenario? scenario, Catalog? catalog)
    {
        if (catalog is null || scenario?.Nodes.FirstOrDefault(n => n.Id == encounter.Node) is not { } node)
        {
            return null;
        }

        var maps = FogOfWar.MapsOnTable(node, encounter).ToList();
        if (maps.All(m => m.Tiles.Count == 0))
        {
            return null;
        }

        var hidden = maps.SelectMany(m => m.Areas).Select(a => a.Id).Except(encounter.RevealedAreas).ToHashSet();
        bool InView(Cell square) => FogOfWar.HiddenAreaAt(node, encounter, catalog, square) is null;

        var tiles = maps.SelectMany(m => m.Tiles).GroupBy(t => (t.Ref, t.X, t.Y)).Select(g => g.Last())
            .Select(t => t.Area is { } area && hidden.Contains(area) ? new MapTile(Fog, t.X, t.Y, null, Fog) : t with { Area = null })
            .ToList();
        var zones = maps.SelectMany(m => m.Zones).Where(z => InView(new Cell(z.Rect.X, z.Rect.Y)))
            .Select((z, i) => new Zone($"z{i}", z.Kind, z.Rect)).ToList();
        var elevation = maps.SelectMany(m => m.Elevation).Where(e => InView(new Cell(e.Rect.X, e.Rect.Y)))
            .Select(e => e with { Note = null }).ToList();
        var tokens = encounter.Tokens.Where(t => !t.Removed && t.Area is { } at && InView(new Cell(at.X, at.Y)))
            .Select((t, i) => new BoardToken { Id = $"t{i}", Key = t.Key, Type = t.Type, Color = t.Color, Image = t.Image, Area = t.Area })
            .ToList();
        var figures = encounter.Combatants.Where(c => !c.Defeated && c.Position is { } at && InView(at))
            .Select(c => new PlayerFigure(c.Instance, c.Figure, c.Position!.Value)).ToList();
        var adventurers = encounter.Adventurers.Where(a => a is { Defeated: false, Position: not null })
            .Select(a => new PlayerAdventurerFigure(a.Name, a.Position!.Value)).ToList();
        var allies = encounter.Allies.Where(a => !a.Defeated && a.Position is { } at && InView(at))
            .Select(a => new PlayerFigure(a.Id, a.Figure, a.Position!.Value)).ToList();
        return new PlayerBoard(tiles, zones, elevation, tokens, figures, adventurers, allies);
    }
}

public sealed record PlayerFigure(string Instance, string? Figure, Cell Position);

public sealed record PlayerAdventurerFigure(string Name, Cell Position);
