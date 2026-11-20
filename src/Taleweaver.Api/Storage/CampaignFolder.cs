using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Storage;

/// <summary>
/// A scenario written as a folder (roadmap §3), for the long ones (an act of the adventure book):
/// <code>
/// campaign.json            the scenario without its steps, plus "chapters": ["chapter-01", …] in order
/// chapter-01/chapter.json  optional: "title", and "nodes": [ids] to order the steps (else by file name)
/// chapter-01/&lt;step&gt;.json  one step (scene or encounter, its diagrams included) per file
/// </code>
/// It is read as one <see cref="Scenario"/>: the engine, the checks and the print see no difference.
/// Ids stay unique across the whole folder; each step knows its <see cref="Node.Chapter"/>.
/// </summary>
public static class CampaignFolder
{
    public const string HeaderFile = "campaign.json";
    public const string ChapterFile = "chapter.json";

    public static bool IsCampaign(string folder) => File.Exists(Path.Combine(folder, HeaderFile));

    public static async Task<Scenario> ReadAsync(string folder, CancellationToken cancellationToken)
    {
        var headerPath = Path.Combine(folder, HeaderFile);
        var header = await ReadAsync<Scenario>(headerPath, cancellationToken);
        var chapters = (await ReadAsync<Header>(headerPath, cancellationToken)).Chapters ?? [];
        var nodes = new List<Node>(header.Nodes);
        foreach (var chapter in chapters)
        {
            var chapterFolder = Path.Combine(folder, chapter);
            if (!Directory.Exists(chapterFolder))
            {
                throw new InvalidDataException($"{headerPath}: chapter folder '{chapter}' not found.");
            }

            var chapterPath = Path.Combine(chapterFolder, ChapterFile);
            var order = File.Exists(chapterPath) ? (await ReadAsync<Chapter>(chapterPath, cancellationToken)).Nodes ?? [] : [];
            var steps = new List<Node>();
            foreach (var file in Directory.EnumerateFiles(chapterFolder, "*.json").Where(f => Path.GetFileName(f) != ChapterFile).Order(StringComparer.Ordinal))
            {
                steps.Add(await ReadAsync<Node>(file, cancellationToken) with { Chapter = chapter });
            }

            int Rank(Node node) => order.IndexOf(node.Id) is var i and >= 0 ? i : int.MaxValue;
            nodes.AddRange(steps.OrderBy(Rank));
        }

        return header with { Nodes = nodes };
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options, cancellationToken)
                   ?? throw new InvalidDataException($"{path} is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path}: {e.Message}", e);
        }
    }

    private sealed record Header(IReadOnlyList<string>? Chapters);

    private sealed record Chapter(LocalizedText? Title, List<string>? Nodes);
}
