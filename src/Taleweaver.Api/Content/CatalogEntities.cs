using System.Text.Json;

namespace Taleweaver.Api.Content;

public sealed record Item
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    /// <summary>mundane, common, uncommon, rare, story, wandering, monster, or null.</summary>
    public string? Tier { get; init; }

    public required string Type { get; init; }

    public bool Unique { get; init; }

    public bool Story { get; init; }

    /// <summary>Family whose monster loot this item is.</summary>
    public string? MonsterOf { get; init; }

    public int? Cost { get; init; }

    public int? Copies { get; init; }

    public string? Rarity { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public LocalizedText? Text { get; init; }

    /// <summary>The icons printed on the front (dice, hands, range, Armor, Defense…); null when it prints none.</summary>
    public ItemStats? Stats { get; init; }

    public ItemSide? BackSide { get; init; }

    public string? Image { get; init; }

    public string? ImageBack { get; init; }

    public string? Review { get; init; }
}

public sealed record ItemSide(LocalizedText Name, IReadOnlyList<string> Tags, LocalizedText Text)
{
    public ItemStats? Stats { get; init; }
}

/// <summary>
/// The icons of an item card (rulebook p. 30 and 89): a weapon's combat dice, hands and Melee/Ranged icon,
/// and the bonuses to the Armor, Defense, Health and Movement of who equips it.
/// </summary>
public sealed record ItemStats
{
    /// <summary>Combat dice by colour; a black die is always rolled (Empowered).</summary>
    public IReadOnlyList<string>? Dice { get; init; }

    public int? Hands { get; init; }

    /// <summary>melee, ranged, or either (the Melee/Ranged icon: it takes the other weapon's).</summary>
    public string? Attack { get; init; }

    /// <summary>The Ranged icon's value.</summary>
    public int? Range { get; init; }

    public int? Armor { get; init; }

    /// <summary>A number, or "X" when the card's text sets it.</summary>
    public JsonElement? Defense { get; init; }

    public int? Health { get; init; }

    public int? Movement { get; init; }
}

public sealed record ItemUpgrade
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public int? Cost { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public LocalizedText? Text { get; init; }

    public string? Image { get; init; }
}

/// <summary>A map tile side. Tiles are 6×6 squares.</summary>
public sealed record Tile
{
    /// <summary>Width and height of a tile, in squares.</summary>
    public const int SquaresPerSide = 6;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? PairedSide { get; init; }

    public TileSize? Size { get; init; }

    /// <summary>The scenery of this side (forest, crypt…): tiles of one encounter should match.</summary>
    public string? Theme { get; init; }

    public string? Image { get; init; }

    /// <summary>
    /// The terrain printed on this side, tile not rotated: 6 rows (top to bottom) of 6 squares (left to
    /// right). <c>.</c> normal, <c>h</c> hindering, <c>w</c> water, <c>m</c> muck, <c>o</c> obstructing,
    /// <c>d</c> dangerous, <c>s</c> special. Empty when not surveyed.
    /// </summary>
    public IReadOnlyList<string> Terrain { get; init; } = [];

    /// <summary>The Elevation of each square, as <see cref="Terrain"/> (digits); empty when the side is flat.</summary>
    public IReadOnlyList<string> Elevation { get; init; } = [];

    /// <summary>
    /// The colour of each square, measured on the image, as <see cref="Terrain"/>: <c>g</c> green,
    /// <c>y</c> gold, ochre or brown, <c>b</c> blue, <c>r</c> red, <c>k</c> grey or dark.
    /// </summary>
    public IReadOnlyList<string> Palette { get; init; } = [];

    /// <summary>The colour of each square once laid on the board, rotated as the tile; nothing while the rotation is unknown.</summary>
    public IEnumerable<(Cell Cell, char Colour)> LaidPalette(MapTile laid)
    {
        if (laid.Rotation is not { } rotation)
        {
            yield break;
        }

        for (var y = 0; y < Palette.Count && y < SquaresPerSide; y++)
        {
            for (var x = 0; x < Palette[y].Length && x < SquaresPerSide; x++)
            {
                var (dx, dy) = Rotate(x, y, rotation);
                yield return (new Cell(laid.X + dx, laid.Y + dy), Palette[y][x]);
            }
        }
    }

    private static readonly Dictionary<char, string> TerrainKinds = new()
    {
        ['h'] = "hindering",
        ['w'] = "water",
        ['m'] = "muck",
        ['o'] = "obstructing",
        ['d'] = "dangerous",
        ['s'] = "special",
    };

    /// <summary>
    /// The printed terrain of this side once laid on the board, rotated clockwise as the tile; nothing
    /// while the rotation is unknown.
    /// </summary>
    public IEnumerable<(Cell Cell, string Kind)> PrintedTerrain(MapTile laid)
    {
        if (laid.Rotation is not { } rotation)
        {
            yield break;
        }

        for (var y = 0; y < Terrain.Count && y < SquaresPerSide; y++)
        {
            for (var x = 0; x < Terrain[y].Length && x < SquaresPerSide; x++)
            {
                if (TerrainKinds.TryGetValue(Terrain[y][x], out var kind))
                {
                    var (dx, dy) = Rotate(x, y, rotation);
                    yield return (new Cell(laid.X + dx, laid.Y + dy), kind);
                }
            }
        }
    }

    /// <summary>Where a square of the tile lands once the tile is turned clockwise by <paramref name="rotation"/> degrees.</summary>
    public static (int X, int Y) Rotate(int x, int y, int rotation)
    {
        const int last = SquaresPerSide - 1;
        var quarters = (((rotation / 90) % 4) + 4) % 4;
        return quarters switch
        {
            1 => (last - y, x),
            2 => (last - x, last - y),
            3 => (y, last - x),
            _ => (x, y),
        };
    }
}

public sealed record TileSize(int W, int H);

/// <summary>A custom die (by colour, "white", "black"…): its six faces.</summary>
public sealed record Die
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public string? Image { get; init; }

    public IReadOnlyList<DieFace> Faces { get; init; } = [];
}

/// <summary>One face: the number rolled and the symbols it shows. The black die only shows symbols, or the skull.</summary>
public sealed record DieFace(int Value, int Shield = 0, int Book = 0, int Star = 0, bool Skull = false);

public sealed record TokenType
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public required string Category { get; init; }

    public string? Size { get; init; }

    public string? Image { get; init; }

    public string? ImageBack { get; init; }

    /// <summary>Coloured variants (objectives, exits, loot…): colour → image.</summary>
    public IReadOnlyDictionary<string, string>? Colors { get; init; }

    /// <summary>Other pictures of the same token (another print, another sheet): a map token may pick one (<see cref="MapToken.Image"/>).</summary>
    public IReadOnlyList<string> ImageVariants { get; init; } = [];

    public string? Terrain { get; init; }

    public bool? BlocksLineOfSight { get; init; }

    public string? Effect { get; init; }

    public string? Ability { get; init; }

    public string? Variant { get; init; }
}

public sealed record InitiativeCard
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public bool Adventurer { get; init; }

    /// <summary>Activation range printed on the card ([1, 3], [4, 6]), or null.</summary>
    public IReadOnlyList<int>? Group { get; init; }

    public string? Family { get; init; }

    public bool Hidden { get; init; }

    public string? Border { get; init; }

    public bool Lowres { get; init; }

    public string? Image { get; init; }
}

/// <summary>rules.json: game vocabulary (effects, tags, terrains, loot decks…).</summary>
public sealed record PackRules
{
    public IReadOnlyList<RuleEntry> Effects { get; init; } = [];

    public IReadOnlyList<RuleEntry> AbilityTokens { get; init; } = [];

    public IReadOnlyList<RuleEntry> TerrainTypes { get; init; } = [];

    public IReadOnlyList<RuleEntry> Tags { get; init; } = [];

    public IReadOnlyList<RuleEntry> CombatantTokens { get; init; } = [];

    public IReadOnlyList<LootDeck> LootDecks { get; init; } = [];

    public JsonElement? Currents { get; init; }
}

public sealed record RuleEntry
{
    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public LocalizedText? Text { get; init; }

    public string? Polarity { get; init; }

    public string? Condition { get; init; }

    public string? Kind { get; init; }

    public bool? Discard { get; init; }

    public int? Limit { get; init; }
}

public sealed record LootDeck
{
    public required string LootLevel { get; init; }

    public IReadOnlyList<LootCard> Cards { get; init; } = [];

    public int? SellPrice { get; init; }

    public int? UniqueSellPrice { get; init; }

    public int? UpgradeCost { get; init; }
}

public sealed record LootCard(string Kind, int? Value, int Count);
