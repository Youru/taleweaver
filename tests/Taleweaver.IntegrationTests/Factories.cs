using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Taleweaver.IntegrationTests;

/// <summary>The API on the homebrew test content (Fixtures/), with only the "hb" pack enabled.</summary>
public sealed class HomebrewFactory : WebApplicationFactory<Program>
{
    public static string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Content:RootPath", FixturesPath);
        builder.UseSetting("Content:EnabledPacks:0", "hb");
    }
}

/// <summary>The API on the real content of the repository (default settings).</summary>
public sealed class RepositoryFactory : WebApplicationFactory<Program>;
