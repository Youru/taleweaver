using System.Text.Json;
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

    /// <summary>
    /// The adventurers stand on the squares of the starting zone, in the party's order: the one the setup
    /// names, else every starting zone of the map in turn (The MAST: Day 1 has two of 2 squares).
    /// Those left without a square are placed by the GM.
    /// </summary>
    private void PlaceAdventurers(BoardMap? map, Setup? setup)
    {
        var zones = map?.Zones ?? [];
        var named = zones.FirstOrDefault(z => z.Id == setup?.AdventurersStart);
        var starts = named is not null ? [named] : zones.Where(z => z.Kind == "startingZone").ToList();
        var squares = starts.SelectMany(z => RowByRow(z.Rect)).ToList();
        // A NAVIGATE moves them: their Effects stay.
        var before = Encounter.Adventurers.ToDictionary(a => a.Name);
        Encounter.Adventurers = ActiveParty
            .Select((adventurer, i) => new AdventurerFigure
            {
                Name = adventurer.Name,
                Position = i < squares.Count ? squares[i] : null,
                Effects = before.TryGetValue(adventurer.Name, out var figure) ? figure.Effects : [],
            })
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

        if (IsAlly(figure))
        {
            if (FindAlly(figure) is not { } ally)
            {
                return GameEngine.Errors.UnknownFigure(figure);
            }

            ally.Position = square;
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
    private IEnumerable<FogArea> AreasOnTable() => FogOfWar.MapsOnTable(EncounterNode, Encounter).SelectMany(m => m.Areas);

    /// <summary>
    /// The hidden area a figure would appear in: its square's, or that of the figure or token it is adjacent to.
    /// Null when it appears in view, or when the scenario only says where in words.
    /// </summary>
    private string? HiddenAreaOf(JsonElement? at) =>
        (Locate(at) ?? Anchor(at)) is { } square ? FogOfWar.HiddenAreaAt(EncounterNode, Encounter, Catalog, square) : null;

    private Cell? Anchor(JsonElement? at) =>
        at is { ValueKind: JsonValueKind.Object } where && where.TryGetProperty("adjacentTo", out var next) && next.ValueKind == JsonValueKind.String
            ? Locate(JsonSerializer.SerializeToElement(new { on = next.GetString() }))
            : null;

    /// <summary>The combatants whose area is revealed come into play: their figure, then their card on the track.</summary>
    private void AppearFromTheFog()
    {
        var encounter = Encounter;
        var appearing = encounter.Waiting.Where(w => encounter.RevealedAreas.Contains(w.Area)).ToList();
        encounter.Waiting.RemoveAll(appearing.Contains);
        foreach (var waiting in appearing)
        {
            Spawn(waiting.Action, fromSetup: false, waiting.InitiativeCard, waiting.PossibleSpawns);
        }
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
        AppearFromTheFog();
        Fire(e => e.Trigger.On == "areaRevealed" && (e.Trigger.Area is null || e.Trigger.Area == area.Id));
    }
}
