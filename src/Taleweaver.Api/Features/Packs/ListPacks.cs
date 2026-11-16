using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Packs;

public static class ListPacks
{
    /// <param name="MissingDependencies">Why an asked-for pack is not enabled: the packs it needs that are not.</param>
    public sealed record PackResponse(
        string Id, LocalizedText Name, string Kind, IReadOnlyList<string> DependsOn, bool Enabled, IReadOnlyDictionary<string, int> Counts,
        IReadOnlyList<string> MissingDependencies);

    public sealed class Handler(IContentStore store, IOptions<ContentOptions> options)
    {
        public async Task<IReadOnlyList<PackResponse>> Handle(CancellationToken cancellationToken)
        {
            var packs = await store.GetPacksAsync(cancellationToken);
            var choice = PackSelection.Choose(packs, options.Value.EnabledPacks);
            var enabled = choice.Enabled.Select(p => p.Id).ToHashSet();
            return packs
                .Select(p => new PackResponse(
                    p.Id, p.Manifest.Name, p.Manifest.Kind, p.Manifest.DependsOn, enabled.Contains(p.Id), p.Manifest.Counts,
                    choice.Missing.GetValueOrDefault(p.Id) ?? []))
                .ToList();
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("ListPacks");
}
