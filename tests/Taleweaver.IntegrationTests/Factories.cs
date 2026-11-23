using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Taleweaver.IntegrationTests;

/// <summary>The API on the homebrew test content (Fixtures/), with only the "hb" pack enabled and a temporary saves folder.</summary>
public sealed class HomebrewFactory : WebApplicationFactory<Program>
{
    private readonly string _saves = Directory.CreateTempSubdirectory("taleweaver-saves-").FullName;
    private readonly string _homebrew = Directory.CreateTempSubdirectory("taleweaver-homebrew-").FullName;

    public static string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>The temporary workshop folder of this API.</summary>
    public string HomebrewPath => _homebrew;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Content:RootPath", FixturesPath);
        builder.UseSetting("Content:EnabledPacks:0", "hb");
        builder.UseSetting("Content:SavesPath", _saves);
        builder.UseSetting("Content:HomebrewDirectory", _homebrew);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Directory.Delete(_saves, recursive: true);
        Directory.Delete(_homebrew, recursive: true);
    }
}

/// <summary>The API on the game's content (<see cref="ContentRoot"/>), with a temporary saves folder.</summary>
public sealed class RepositoryFactory : WebApplicationFactory<Program>
{
    private readonly string _saves = Directory.CreateTempSubdirectory("taleweaver-saves-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Content:SavesPath", _saves);
        if (ContentRoot.Path is { } root)
        {
            builder.UseSetting("Content:RootPath", root);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Directory.Delete(_saves, recursive: true);
    }
}
