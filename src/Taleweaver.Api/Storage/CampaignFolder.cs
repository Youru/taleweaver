using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Ids stay unique across the whole folder; each step knows its <see cref="Node.Chapter"/>, and
/// <see cref="Scenario.Chapters"/> lists the chapters with their titles.
/// </summary>
public static class CampaignFolder
{
    public const string HeaderFile = "campaign.json";
    public const string ChapterFile = "chapter.json";

    public static bool IsCampaign(string folder) => File.Exists(Path.Combine(folder, HeaderFile));

    public static async Task<Scenario> ReadAsync(string folder, CancellationToken cancellationToken)
    {
        var headerPath = Path.Combine(folder, HeaderFile);
        // "chapters" is a list of folder names in the file, of { id, title } in the model: read apart.
        var json = await ReadAsync<JsonObject>(headerPath, cancellationToken);
        var chapterIds = json["chapters"]?.Deserialize<List<string>>(JsonDefaults.Options) ?? [];
        json.Remove("chapters");
        var header = Deserialize<Scenario>(json, headerPath);

        var nodes = new List<Node>(header.Nodes);
        var chapters = new List<ScenarioChapter>();
        foreach (var chapter in chapterIds)
        {
            var chapterFolder = Path.Combine(folder, chapter);
            if (!Directory.Exists(chapterFolder))
            {
                throw new InvalidDataException($"{headerPath}: chapter folder '{chapter}' not found.");
            }

            var chapterPath = Path.Combine(chapterFolder, ChapterFile);
            var meta = File.Exists(chapterPath) ? await ReadAsync<Chapter>(chapterPath, cancellationToken) : new Chapter(null, null);
            var order = meta.Nodes ?? [];
            var steps = new List<Node>();
            foreach (var file in StepFiles(chapterFolder))
            {
                steps.Add(await ReadAsync<Node>(file, cancellationToken) with { Chapter = chapter });
            }

            int Rank(Node node) => order.IndexOf(node.Id) is var i and >= 0 ? i : int.MaxValue;
            nodes.AddRange(steps.OrderBy(Rank));
            chapters.Add(new ScenarioChapter(chapter, meta.Title));
        }

        return header with { Nodes = nodes, Chapters = chapters };
    }

    /// <summary>
    /// Writes the scenario as a folder: the steps of each chapter in its folder (in the scenario's order),
    /// the rest in campaign.json. The old step files are removed first, so a renamed or moved step leaves
    /// no orphan; so are the folders of chapters no longer listed (when they hold nothing but JSON files).
    /// </summary>
    public static async Task WriteAsync(string folder, Scenario scenario, CancellationToken cancellationToken)
    {
        var chapters = scenario.Chapters ?? [];
        var ids = chapters.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        bool InChapter(Node node) => node.Chapter is { } c && ids.Contains(c);

        var header = JsonSerializer.SerializeToNode(
            scenario with { Chapters = null, Nodes = scenario.Nodes.Where(n => !InChapter(n)).ToList() },
            JsonDefaults.FileOptions)!.AsObject();
        header["chapters"] = new JsonArray(chapters.Select(c => (JsonNode)JsonValue.Create(c.Id)).ToArray());

        Directory.CreateDirectory(folder);
        foreach (var chapter in chapters)
        {
            var chapterFolder = Path.Combine(folder, chapter.Id);
            var steps = scenario.Nodes.Where(n => n.Chapter == chapter.Id).ToList();
            Directory.CreateDirectory(chapterFolder);
            foreach (var old in StepFiles(chapterFolder))
            {
                File.Delete(old);
            }

            await AtomicFile.WriteJsonAsync(Path.Combine(chapterFolder, ChapterFile), new Chapter(chapter.Title, steps.Select(n => n.Id).ToList()), cancellationToken);
            foreach (var step in steps)
            {
                await AtomicFile.WriteJsonAsync(Path.Combine(chapterFolder, $"{step.Id}.json"), step with { Chapter = null }, cancellationToken);
            }
        }

        foreach (var stale in Directory.EnumerateDirectories(folder).Where(d => !ids.Contains(Path.GetFileName(d))))
        {
            if (Directory.EnumerateFileSystemEntries(stale).All(f => File.Exists(f) && f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                Directory.Delete(stale, recursive: true);
            }
        }

        await AtomicFile.WriteJsonAsync(Path.Combine(folder, HeaderFile), header, cancellationToken);
    }

    private static IEnumerable<string> StepFiles(string chapterFolder) =>
        Directory.EnumerateFiles(chapterFolder, "*.json").Where(f => Path.GetFileName(f) != ChapterFile).Order(StringComparer.Ordinal);

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

    private static T Deserialize<T>(JsonNode json, string path)
    {
        try
        {
            return json.Deserialize<T>(JsonDefaults.Options) ?? throw new InvalidDataException($"{path} is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path}: {e.Message}", e);
        }
    }

    private sealed record Chapter(LocalizedText? Title, List<string>? Nodes);
}
