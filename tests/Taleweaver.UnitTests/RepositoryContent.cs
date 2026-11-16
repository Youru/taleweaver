using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Storage;

namespace Taleweaver.UnitTests;

/// <summary>The real content of the repository (packs/, scenarios/), read once for all the tests.</summary>
public static class RepositoryContent
{
    private static readonly Lazy<string> RootPath = new(() =>
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "packs", "um1", "pack.json")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (packs/um1/pack.json) not found.");
    });

    private static readonly Lazy<Task<Catalog>> SharedCatalog = new(async () => new Catalog(PackSelection.Choose(await CreateStore().GetPacksAsync(CancellationToken.None), []).Enabled));

    public static string Root => RootPath.Value;

    public static JsonContentStore CreateStore(string? root = null)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(root ?? Root);
        return new JsonContentStore(Options.Create(new ContentOptions { RootPath = root ?? Root }), environment);
    }

    public static Task<Catalog> CatalogAsync() => SharedCatalog.Value;

    public static async Task<Scenario> BlightAsync() =>
        await CreateStore().GetScenarioAsync("um1:scenario/blight-of-rhamsted", CancellationToken.None)
        ?? throw new InvalidOperationException("Blight of Rhamsted not found.");
}
