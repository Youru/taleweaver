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
    /// <summary>
    /// A chapter in English, and its translations by language ("fr"): each one is the English line for line (same
    /// headings, page marks, tables), so the sections, the page references and the phones' filter come from the
    /// English. A line not translated is the English one, marked <c>&lt;!-- EN --&gt;</c>.
    /// </summary>
    public sealed record Chapter(string Id, string Markdown, IReadOnlyDictionary<string, string>? Translations = null);

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
                var markdown = await File.ReadAllTextAsync(file, cancellationToken);
                chapters.Add(new Chapter(id, markdown, await TranslationsAsync(book, Path.GetFileName(file), markdown, cancellationToken)));
            }
        }

        var glossary = Path.Combine(book, "glossary.md");
        var whole = new Response(chapters, File.Exists(glossary) ? await File.ReadAllTextAsync(glossary, cancellationToken) : "");
        return TypedResults.Ok(await phones.ForAsync(context, cancellationToken) is null
            ? whole
            : new PlayerRulebook(await catalogs.GetAsync(cancellationToken)).For(whole));
    }

    private static readonly string[] Languages = ["fr"];

    /// <summary>
    /// The translations of a chapter, in &lt;root&gt;/reference/&lt;lang&gt;/rules/rulebook/chapters (the book being
    /// &lt;root&gt;/reference/rules/rulebook). One that does not have the English's lines is left out.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>?> TranslationsAsync(string book, string file, string english,
        CancellationToken cancellationToken)
    {
        var reference = Path.GetDirectoryName(Path.GetDirectoryName(book))!;
        var lines = english.ReplaceLineEndings("\n").Split('\n').Length;
        var found = new Dictionary<string, string>();
        foreach (var language in Languages)
        {
            var path = Path.Combine(reference, language, "rules", "rulebook", "chapters", file);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(path, cancellationToken);
            if (text.ReplaceLineEndings("\n").Split('\n').Length == lines)
            {
                found[language] = text;
            }
        }

        return found.Count == 0 ? null : found;
    }
}
