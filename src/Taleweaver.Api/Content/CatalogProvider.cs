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
        var enabled = options.Value.EnabledPacks;
        return new Catalog(enabled.Length == 0 ? packs : packs.Where(p => enabled.Contains(p.Id)));
    });

    public Task<Catalog> GetAsync(CancellationToken cancellationToken) => _catalog.Value.WaitAsync(cancellationToken);
}
