using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.Api.Features.Rulebook;

/// <summary>
/// The cleaned rulebook, to read and to search (rules-spec), kept out of the web bundle. Whole for the GM and the
/// tablet; a phone or a table screen, while a room is open, gets <see cref="PlayerRulebook"/>.
/// </summary>
public static class GetRulebook
{
    public sealed record Chapter(string Id, string Markdown);

    /// <summary>The glossary gives the French words that find the English text.</summary>
    public sealed record Response(IReadOnlyList<Chapter> Chapters, string Glossary);

    public static IEndpointRouteBuilder MapRulebookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rulebook", HandleAsync).WithTags("Rules").WithName("GetRulebook");
        return app;
    }

    private static async Task<Ok<Response>> HandleAsync(IOptions<ContentOptions> options, IWebHostEnvironment environment,
        PhoneCards phones, CatalogProvider catalogs, HttpContext context, CancellationToken cancellationToken)
    {
        // The first content folder that has one (the main folder, or an installed content pack)
        var books = new ContentRoots(options, environment).All.Select(r => Path.Combine(r, "reference", "rules", "rulebook")).ToList();
        var book = books.FirstOrDefault(b => File.Exists(Path.Combine(b, "glossary.md"))) ?? books[0];
        var folder = Path.Combine(book, "chapters");
        var chapters = new List<Chapter>();
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal))
            {
                // "07-an-adventurer-s-turn.md" is chapter 07: the id survives a change of title
                var id = Path.GetFileName(file).Split('-')[0];
                chapters.Add(new Chapter(id, await File.ReadAllTextAsync(file, cancellationToken)));
            }
        }

        var glossary = Path.Combine(book, "glossary.md");
        var whole = new Response(chapters, File.Exists(glossary) ? await File.ReadAllTextAsync(glossary, cancellationToken) : "");
        return TypedResults.Ok(await phones.ForAsync(context, cancellationToken) is null
            ? whole
            : new PlayerRulebook(await catalogs.GetAsync(cancellationToken)).For(whole));
    }
}
