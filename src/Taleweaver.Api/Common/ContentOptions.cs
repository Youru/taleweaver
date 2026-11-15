namespace Taleweaver.Api.Common;

/// <summary>Where the game content lives. Relative paths are resolved against <see cref="RootPath"/>,
/// itself relative to the application content root.</summary>
public sealed class ContentOptions
{
    public const string Section = "Content";

    public string RootPath { get; set; } = ".";

    public string PacksDirectory { get; set; } = "packs";

    public string ScenariosDirectory { get; set; } = "scenarios";

    public string AssetsDirectory { get; set; } = "assets";

    public string[] EnabledPacks { get; set; } = [];

    /// <summary>Folder of the saved games; empty → %APPDATA%/Middara/saves (outside the repository).</summary>
    public string SavesPath { get; set; } = "";
}
