using System.Text.RegularExpressions;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>Reading the Command cards (rulebook p. 58-59, 72, 75-77).</summary>
internal static partial class AllyRules
{
    public const string Summoning = "summoning";
    public const string FelledAfterlife = "felled-afterlife";
    public const string ConduitSummoning = "conduit-summoning";
    public const string EnslavedSpirit = "enslaved-spirit";

    private const string EsperTag = "LOYAL ESPER";
    private const string ConduitTag = "CONDUIT";

    [GeneratedRegex(@"\d+")]
    private static partial Regex Dots();

    public static bool IsCommand(Combatant? card) => card?.Kind == "command";

    public static bool IsEsper(Combatant card) => IsCommand(card) && card.Tags.Contains(EsperTag);

    public static bool IsConduit(Combatant card) => IsCommand(card) && card.Tags.Contains(ConduitTag);

    public static bool Knows(AdventurerState member, string discipline) =>
        member.Disciplines.Any(d => d.Id.EndsWith("/" + discipline, StringComparison.Ordinal)
                                    || string.Equals(d.Id.Replace(' ', '-'), discipline, StringComparison.OrdinalIgnoreCase));

    /// <summary>The bar as surveyed: "3 rouges / 2 gris" is 3 a turn, 5 held at most.</summary>
    public static AdventurerStamina StaminaOf(Combatant? card)
    {
        var dots = Dots().Matches(card?.Stamina ?? "").Select(m => int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return dots is [var gained, var more] ? new AdventurerStamina(gained, gained + more) : new AdventurerStamina(3, 5);
    }

    public static int ForceOf(Catalog catalog, AdventurerState owner) =>
        6 + owner.Disciplines.Select(d => catalog.FindDiscipline(d.Id)?.Level ?? 0).DefaultIfEmpty(0).Max();
}
