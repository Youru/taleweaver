using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Content;

/// <summary>Builds the <see cref="Catalog"/> of the enabled packs once, from the content store.</summary>
public sealed class CatalogProvider(IContentStore store, IOptions<ContentOptions> options)
{
    private readonly Lazy<Task<Catalog>> _catalog = new(async () =>
    {
        var packs = await store.GetPacksAsync(CancellationToken.None);
        return new Catalog(PackSelection.Choose(packs, options.Value.EnabledPacks).Enabled);
    });

    public Task<Catalog> GetAsync(CancellationToken cancellationToken) => _catalog.Value.WaitAsync(cancellationToken);
}
