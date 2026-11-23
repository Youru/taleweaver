namespace Taleweaver.Api.Common;

/// <summary>Where the game content lives. Relative paths are resolved against <see cref="RootPath"/>,
/// itself relative to the application content root.</summary>
public sealed class ContentOptions
{
    public const string Section = "Content";

    public string RootPath { get; set; } = ".";

    public string PacksDirectory { get; set; } = "packs";

    public string ScenariosDirectory { get; set; } = "scenarios";

    /// <summary>Where the workshop keeps the scenarios made by the user (the only ones it may change).</summary>
    public string HomebrewDirectory { get; set; } = "scenarios/homebrew";

    public string AssetsDirectory { get; set; } = "assets";

    public string[] EnabledPacks { get; set; } = [];

    /// <summary>Folder of the saved games; empty → %APPDATA%/Middara/saves (outside the repository).</summary>
    public string SavesPath { get; set; } = "";

    /// <summary>Other content folders (packs/, scenarios/, assets/, reference/), read after <see cref="RootPath"/>.</summary>
    public string[] ExtraRoots { get; set; } = [];

    /// <summary>
    /// Where the content packs installed by the user are unpacked, one folder each (Features/ContentPacks); empty: none.
    /// The packaged app sets it, beside its own content.
    /// </summary>
    public string InstalledPacksPath { get; set; } = "";

    /// <summary>A folder where the user may drop content pack files (.zip): installed at start. Empty: none.</summary>
    public string DroppedPacksPath { get; set; } = "";
}
