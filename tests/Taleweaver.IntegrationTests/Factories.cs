using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Taleweaver.IntegrationTests;

/// <summary>The API on the homebrew test content (Fixtures/), with only the "hb" pack enabled and a temporary saves folder.</summary>
public sealed class HomebrewFactory : WebApplicationFactory<Program>
{
    private readonly string _saves = Directory.CreateTempSubdirectory("taleweaver-saves-").FullName;

    public static string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Content:RootPath", FixturesPath);
        builder.UseSetting("Content:EnabledPacks:0", "hb");
        builder.UseSetting("Content:SavesPath", _saves);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Directory.Delete(_saves, recursive: true);
    }
}

/// <summary>The API on the real content of the repository, with a temporary saves folder.</summary>
public sealed class RepositoryFactory : WebApplicationFactory<Program>
{
    private readonly string _saves = Directory.CreateTempSubdirectory("taleweaver-saves-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("Content:SavesPath", _saves);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Directory.Delete(_saves, recursive: true);
    }
}
