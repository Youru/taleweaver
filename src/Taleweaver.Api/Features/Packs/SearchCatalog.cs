using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Packs;

/// <summary>Searches the enabled packs by name, for the editor's pickers.</summary>
public static class SearchCatalog
{
    public static readonly IReadOnlyList<string> Kinds = ["combatant", "item", "item-upgrade", "tile", "token", "initiative"];

    public sealed record Request(string? Kind, string? Q, string? Pack, int? Limit);

    public sealed record Entry(string Id, string Kind, LocalizedText Name, string? Image);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Kind).Must(k => k is null || Kinds.Contains(k))
                .WithMessage($"Kind must be one of: {string.Join(", ", Kinds)}.");
            RuleFor(r => r.Q).MaximumLength(100);
            RuleFor(r => r.Limit).InclusiveBetween(1, 500);
        }
    }

    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<IReadOnlyList<Entry>> Handle(Request request, CancellationToken cancellationToken)
        {
            var catalog = await catalogs.GetAsync(cancellationToken);
            var query = request.Q?.Trim() ?? "";
            return catalog.Packs
                .Where(p => request.Pack is null || p.Id == request.Pack)
                .SelectMany(Entries)
                .Where(e => request.Kind is null || e.Kind == request.Kind)
                .Where(e => query.Length == 0
                            || e.Name.Values.Any(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
                            || e.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(request.Limit ?? 100)
                .ToList();
        }

        private static IEnumerable<Entry> Entries(Pack pack) =>
            pack.Combatants.Select(c => new Entry(c.Id, "combatant", c.Name, c.Image))
                .Concat(pack.Items.Select(i => new Entry(i.Id, "item", i.Name, i.Image)))
                .Concat(pack.ItemUpgrades.Select(u => new Entry(u.Id, "item-upgrade", u.Name, u.Image)))
                .Concat(pack.Tiles.Select(t => new Entry(t.Id, "tile", new Dictionary<string, string> { ["en"] = t.Name }, t.Image)))
                .Concat(pack.Tokens.Select(t => new Entry(t.Id, "token", t.Name, t.Image)))
                .Concat(pack.InitiativeCards.Select(i => new Entry(i.Id, "initiative", new Dictionary<string, string> { ["en"] = i.Label }, i.Image)));
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async ([AsParameters] Request request, Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(request, cancellationToken)))
            .WithValidation<Request>()
            .WithName("SearchCatalog");
}
