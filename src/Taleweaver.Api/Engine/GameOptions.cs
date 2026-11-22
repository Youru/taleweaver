namespace Taleweaver.Api.Engine;

/// <summary>
/// The optional rules chosen when the game is launched (rulebook p. 86-88, roadmap §8.4): all off by default, kept
/// with the game and never changed afterwards (replaying the history gives the same game). Those the engine cannot
/// play (the board and the dice are the table's) are only reminded to the GM.
/// </summary>
public sealed record GameOptions
{
    // Played by the engine

    /// <summary>A Defeated adventurer adds an Urgency token and comes back on their next turn (p. 86).</summary>
    public bool NoPlayerElimination { get; init; }

    /// <summary>With No Player Elimination: the Urgency tokens that lose the encounter (3 hard, 4, 5 easy).</summary>
    public int? UrgencyLimit { get; init; }

    /// <summary>Non-BOSS opponents have 50% more HP.</summary>
    public bool TougherMobs { get; init; }

    /// <summary>Always the lowest Loot LVL card of a family.</summary>
    public bool NoScaling { get; init; }

    /// <summary>The track is not shuffled again at the end of each round.</summary>
    public bool StaticMonsters { get; init; }

    /// <summary>A card Spawned goes to the back of the track.</summary>
    public bool SlowMonsters { get; init; }

    /// <summary>No Alternate nor Variation card unless the scenario names it.</summary>
    public bool LessVariety { get; init; }

    public bool RemoveUrgency { get; init; }

    /// <summary>The first Re-Spec of each adventurer is free.</summary>
    public bool XpLeniency { get; init; }

    // Reminded to the GM

    public bool ForgivingTerrain { get; init; }

    public bool RandomCounters { get; init; }

    public bool DeadlierBosses { get; init; }

    public bool BossRage { get; init; }

    public bool NobodyLeftBehind { get; init; }

    public bool FavorableConditions { get; init; }

    public bool Reconnaissance { get; init; }

    public const int DefaultUrgencyLimit = 4;

    public static bool IsKnownUrgencyLimit(int? limit) => limit is null or 3 or 4 or 5;
}
