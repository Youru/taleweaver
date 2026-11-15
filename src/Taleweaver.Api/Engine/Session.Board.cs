using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The board as the GM mirrors it, if they want to: figures' squares and the fog of war (§3.4).
/// The rules never depend on the squares: the AI still asks the GM.
/// </summary>
internal sealed partial class Session
{
    private const string AdventurerFigurePrefix = "adventurer:";

    /// <summary>The adventurers stand on the squares of the starting zone, in the party's order.</summary>
    private void PlaceAdventurers(Node node)
    {
        var zones = node.Map?.Zones ?? [];
        var start = zones.FirstOrDefault(z => z.Id == node.Setup?.AdventurersStart) ?? zones.FirstOrDefault(z => z.Kind == "startingZone");
        var squares = start is null ? [] : RowByRow(start.Rect).ToList();
        Encounter.Adventurers = State.Campaign.Party
            .Select((adventurer, i) => new AdventurerFigure { Name = adventurer.Name, Position = i < squares.Count ? squares[i] : null })
            .ToList();
    }

    private static IEnumerable<Cell> RowByRow(Rect rect) =>
        Enumerable.Range(rect.Y, rect.H).SelectMany(y => Enumerable.Range(rect.X, rect.W).Select(x => new Cell(x, y)));

    private Result Move(string? figure, Cell? to)
    {
        if (to is not { X: >= 0, Y: >= 0 } square)
        {
            return GameEngine.Errors.InvalidAnswer(null);
        }

        if (figure?.StartsWith(AdventurerFigurePrefix, StringComparison.Ordinal) == true)
        {
            var name = figure[AdventurerFigurePrefix.Length..];
            if (Encounter.Adventurers.FirstOrDefault(a => a.Name == name) is not { } adventurer)
            {
                return GameEngine.Errors.UnknownFigure(figure);
            }

            adventurer.Position = square;
            return Result.Success();
        }

        if (Find(figure) is not { Defeated: false } combatant)
        {
            return GameEngine.Errors.UnknownFigure(figure);
        }

        combatant.Position = square;
        return Result.Success();
    }

    /// <summary>The fog areas of a map that are visible from the start.</summary>
    private void RevealAtStart(BoardMap? map)
    {
        foreach (var area in map?.Areas ?? [])
        {
            if (area.RevealedAtStart != false && !Encounter.RevealedAreas.Contains(area.Id))
            {
                Encounter.RevealedAreas.Add(area.Id);
            }
        }
    }

    /// <summary>Every fog area of the maps on the table: the encounter's and the diagrams placed so far.</summary>
    private IEnumerable<FogArea> AreasOnTable()
    {
        var node = EncounterNode;
        var diagrams = node.Diagrams.Where(d => Encounter.PlacedDiagrams.Contains(d.Id)).Select(d => d.Map);
        return new[] { node.Map }.Concat(diagrams).SelectMany(m => m?.Areas ?? []);
    }

    private Result RevealAreaCommand(string? id)
    {
        if (id is null || Encounter.RevealedAreas.Contains(id) || AreasOnTable().All(a => a.Id != id))
        {
            return GameEngine.Errors.UnknownArea(id);
        }

        RevealArea(id);
        return Result.Success();
    }

    /// <summary>Reveals an area (scenario action or the GM, when an adventurer enters it), then fires areaRevealed.</summary>
    private void RevealArea(string? id)
    {
        var area = AreasOnTable().FirstOrDefault(a => a.Id == id);
        if (area is null || Encounter.RevealedAreas.Contains(area.Id))
        {
            return;
        }

        Encounter.RevealedAreas.Add(area.Id);
        Log("revealArea", area.Name, Args(("area", area.Id)));
        Fire(e => e.Trigger.On == "areaRevealed" && (e.Trigger.Area is null || e.Trigger.Area == area.Id));
    }
}
