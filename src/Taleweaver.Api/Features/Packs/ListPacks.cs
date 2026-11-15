using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Packs;

public static class ListPacks
{
    public sealed record PackResponse(string Id, LocalizedText Name, string Kind, IReadOnlyList<string> DependsOn, bool Enabled, IReadOnlyDictionary<string, int> Counts);

    public sealed class Handler(IContentStore store, IOptions<ContentOptions> options)
    {
        public async Task<IReadOnlyList<PackResponse>> Handle(CancellationToken cancellationToken)
        {
            var enabled = options.Value.EnabledPacks;
            var packs = await store.GetPacksAsync(cancellationToken);
            return packs
                .Select(p => new PackResponse(
                    p.Id, p.Manifest.Name, p.Manifest.Kind, p.Manifest.DependsOn, enabled.Length == 0 || enabled.Contains(p.Id), p.Manifest.Counts))
                .ToList();
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("ListPacks");
}
