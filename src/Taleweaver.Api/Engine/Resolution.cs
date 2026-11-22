namespace Taleweaver.Api.Engine;

/// <summary>How an Attack or a Spell ended, as the duel reports it.</summary>
/// <param name="Outcome">One of the constants below.</param>
/// <param name="Difference">By how much an Attack hit (FORCE + DIFFERENCE HIT).</param>
/// <param name="Empower">1 SP of the attacker's, when an adventurer or an ally.</param>
/// <param name="Dodge">1 SP of the target's, when an adventurer or an ally.</param>
/// <param name="Step">The AI action resolved: "line:part:occurrence", the line being the instruction's in the journal.</param>
public sealed record Resolution(string Outcome, int Difference = 0, bool Empower = false, bool Dodge = false, string? Step = null)
{
    public const string Hit = "hit";
    public const string Miss = "miss";
    public const string Affected = "affected";
    public const string Resisted = "resisted";
    public const string Backfire = "backfire";
}
