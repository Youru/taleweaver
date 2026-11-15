namespace Taleweaver.Api.Content;

/// <summary>Loot levels, in increasing order (rulebook p. 79).</summary>
public static class LootLevels
{
    public const string Any = "any";

    private static readonly string[] OrderedLevels = ["mundane", "common", "uncommon", "rare"];

    public static IReadOnlyList<string> Ordered => OrderedLevels;

    /// <summary>Rank of a level, or -1 when it is not one of the four ordered levels.</summary>
    public static int Rank(string? level) => level is null ? -1 : Array.IndexOf(OrderedLevels, level);

    public static bool IsKnown(string? level) => Rank(level) >= 0;
}
