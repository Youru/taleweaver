namespace Taleweaver.Api.Engine;

/// <summary>The only source of chance (initiative shuffle, random loot). Combat dice stay physical.</summary>
public interface IRandomSource
{
    /// <summary>A number in [0, maxExclusive), the <paramref name="draw"/>-th of the game.</summary>
    int Next(int draw, int maxExclusive);
}

/// <summary>Draws depend only on the game's seed and the draw number, so replaying the history gives the same game.</summary>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    public int Next(int draw, int maxExclusive)
    {
        // SplitMix64 of (seed, draw)
        var z = unchecked(((ulong)(uint)seed << 32) + (ulong)(uint)draw + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        return (int)(z % (ulong)maxExclusive);
    }
}
