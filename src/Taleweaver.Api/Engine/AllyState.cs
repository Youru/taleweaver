using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// A Command Combatant in play (rulebook p. 58): always on the adventurers' side, and kept apart from the opponents
/// so that it never counts for loot, Urgency or "none left".
/// </summary>
public sealed class AllyState
{
    /// <summary>Unique in the encounter ("zulfiqar"); its figure is "ally:&lt;id&gt;".</summary>
    public required string Id { get; set; }

    public required string Card { get; set; }

    public required string Family { get; set; }

    public Dictionary<string, string> Name { get; set; } = [];

    /// <summary>Its card on the track, "ally:&lt;initiative card&gt;": shared by the copies, apart from an opponent's.</summary>
    public required string TrackCard { get; set; }

    /// <summary>The adventurer whose Ability brought it, whom its Scaling Icons read (p. 72); null: the story's.</summary>
    public string? Owner { get; set; }

    /// <summary>How it came: summoning, felledAfterlife, story.</summary>
    public string Source { get; set; } = AllySources.Story;

    /// <summary>A LOYAL ESPER: shares its Damage with its summoner, stays from one encounter to the next.</summary>
    public bool Esper { get; set; }

    /// <summary>Maximum HP, computed: the card's, or its owner's for "*". Null: unknown.</summary>
    public int? Health { get; set; }

    /// <summary>Force *: 6 + the owner's highest Discipline level. Computed.</summary>
    public int? Force { get; set; }

    public int Damage { get; set; }

    public int Stamina { get; set; }

    public int StaminaRecovery { get; set; }

    public int StaminaMax { get; set; }

    /// <summary>Had a turn in this encounter: no SP is gained on the first one.</summary>
    public bool HasPlayed { get; set; }

    public List<string> Effects { get; set; } = [];

    public Cell? Position { get; set; }

    public string? Figure { get; set; }

    public bool Defeated { get; set; }
}

public static class AllySources
{
    public const string Summoning = "summoning";
    public const string FelledAfterlife = "felledAfterlife";
    public const string Story = "story";
}

/// <summary>How a story ally left the last encounter (p. 58: it heals as the adventurers do at the next).</summary>
public sealed record AllyCondition(int Damage, bool Defeated);

/// <summary>What an adventurer's Assemblage Disciplines allow now: summoning, felledAfterlife, earnConduit, conduit.</summary>
public sealed record AllyOption(string Adventurer, string Kind, IReadOnlyList<string> Cards);
