namespace Taleweaver.Api.Content;

/// <summary>pack.json: header of a content pack (core box, expansion or homebrew).</summary>
public sealed record PackManifest
{
    public int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required LocalizedText Name { get; init; }

    public string Kind { get; init; } = "core";

    public IReadOnlyList<string> DependsOn { get; init; } = [];

    public IReadOnlyList<string> Overrides { get; init; } = [];

    /// <summary>Catalogue files of the pack, by kind ("combatants" → "combatants.json").</summary>
    public IReadOnlyDictionary<string, string> Files { get; init; } = new Dictionary<string, string>();

    public string? AdventureSheet { get; init; }

    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();

    /// <summary>Every figure (standee) of the pack, by name ("rook", "enslaved-spirit-3").</summary>
    public IReadOnlyDictionary<string, PackFigure> Figures { get; init; } = new Dictionary<string, PackFigure>();

    /// <summary>The round portrait the books draw a figure with on their diagrams, by family or name ("cave-sickle", "rook").</summary>
    public IReadOnlyDictionary<string, string> Portraits { get; init; } = new Dictionary<string, string>();
}

public sealed record PackFigure(string Image, string? BaseSize);

/// <summary>A loaded pack: its header and the content of its catalogue files.</summary>
public sealed record Pack
{
    public required PackManifest Manifest { get; init; }

    public IReadOnlyList<Combatant> Combatants { get; init; } = [];

    public IReadOnlyList<Item> Items { get; init; } = [];

    public IReadOnlyList<ItemUpgrade> ItemUpgrades { get; init; } = [];

    public IReadOnlyList<Tile> Tiles { get; init; } = [];

    public IReadOnlyList<TokenType> Tokens { get; init; } = [];

    /// <summary>Other pictures this pack prints for the tokens of another pack (token id → images): BE1's cave-style Dangerous terrain.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TokenImageVariants { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyList<InitiativeCard> InitiativeCards { get; init; } = [];

    public IReadOnlyList<Die> Dice { get; init; } = [];

    public PackRules? Rules { get; init; }

    public string Id => Manifest.Id;
}
