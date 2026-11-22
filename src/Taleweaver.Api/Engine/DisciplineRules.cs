using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>Learning Disciplines (rulebook p. 24, 32), the same at the departure and in a Story Round.</summary>
internal static class DisciplineRules
{
    /// <summary>The printed cost, +1 per Discipline of the same level already learnt; null when unknown.</summary>
    public static int? Cost(Catalog catalog, IEnumerable<string> known, Discipline? card) => card switch
    {
        { Story: true } => 0,
        { Level: { } level } => (card.Cost ?? (2 * level) + 1) + known.Count(id => catalog.FindDiscipline(id) is { Story: false } other && other.Level == level),
        _ => null,
    };

    /// <summary>The lowest level of the tree still lacking below the card's, or null.</summary>
    public static int? MissingLevel(Catalog catalog, IEnumerable<string> known, Discipline? card)
    {
        if (card is not { Story: false, Level: { } level })
        {
            return null;
        }

        var levels = known.Select(catalog.FindDiscipline).Where(d => d is { Story: false } && d.Tree == card.Tree).Select(d => d!.Level).ToHashSet();
        return Enumerable.Range(1, Math.Max(0, level - 1)).Cast<int?>().FirstOrDefault(l => !levels.Contains(l));
    }
}
