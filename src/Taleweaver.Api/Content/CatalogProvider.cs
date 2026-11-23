using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Content;

/// <summary>Builds the <see cref="Catalog"/> of the enabled packs once, from the content store.</summary>
public sealed class CatalogProvider(IContentStore store, IOptions<ContentOptions> options)
{
    private Lazy<Task<Catalog>> _catalog = Build(store, options);

    public Task<Catalog> GetAsync(CancellationToken cancellationToken) => _catalog.Value.WaitAsync(cancellationToken);

    /// <summary>A content pack was installed or removed: the catalogue is built again on the next call.</summary>
    public void Invalidate()
    {
        store.Invalidate();
        _catalog = Build(store, options);
    }

    private static Lazy<Task<Catalog>> Build(IContentStore store, IOptions<ContentOptions> options) => new(async () =>
    {
        var packs = await store.GetPacksAsync(CancellationToken.None);
        return new Catalog(PackSelection.Choose(packs, options.Value.EnabledPacks).Enabled);
    });
}
