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

    public ItemSide? BackSide { get; init; }

    public string? Image { get; init; }

    public string? ImageBack { get; init; }

    public string? Review { get; init; }
}

public sealed record ItemSide(LocalizedText Name, IReadOnlyList<string> Tags, LocalizedText Text);

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

    public string? Image { get; init; }

    public IReadOnlyList<JsonElement> Terrain { get; init; } = [];
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
