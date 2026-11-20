using System.Text.Json;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Storage;

/// <summary>
/// Reads packs/&lt;pack&gt;/pack.json and its catalogue files, and scenarios/**/*.json.
/// Packs are read once and kept in memory; scenarios are read on each call, since the editor changes them.
/// </summary>
public sealed class JsonContentStore(IOptions<ContentOptions> options, IHostEnvironment environment, ILogger<JsonContentStore>? logger = null)
    : IContentStore
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);
    private readonly ContentOptions _options = options.Value;
    private readonly SemaphoreSlim _packsLock = new(1, 1);
    private readonly SemaphoreSlim _scenariosLock = new(1, 1);
    private IReadOnlyList<Pack>? _packs;

    private string PacksPath => Path.Combine(_root, _options.PacksDirectory);

    private string ScenariosPath => Path.Combine(_root, _options.ScenariosDirectory);

    private string HomebrewPath => Path.GetFullPath(Path.Combine(_root, _options.HomebrewDirectory));

    private bool IsHomebrew(string path) =>
        Path.GetFullPath(path).StartsWith(HomebrewPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<Pack>> GetPacksAsync(CancellationToken cancellationToken)
    {
        if (_packs is not null)
        {
            return _packs;
        }

        await _packsLock.WaitAsync(cancellationToken);
        try
        {
            if (_packs is null)
            {
                var packs = new List<Pack>();
                foreach (var manifestPath in Directory.EnumerateFiles(PacksPath, "pack.json", SearchOption.AllDirectories).Order())
                {
                    packs.Add(await LoadPackAsync(manifestPath, cancellationToken));
                }

                _packs = packs;
            }

            return _packs;
        }
        finally
        {
            _packsLock.Release();
        }
    }

    public async Task<IReadOnlyList<ScenarioSummary>> ListScenariosAsync(CancellationToken cancellationToken)
    {
        var index = await IndexScenariosAsync(cancellationToken);
        return index.Values
            .Select(entry => new ScenarioSummary(entry.Scenario.Id, entry.Scenario.Title, entry.Scenario.Kind, entry.Scenario.Packs, IsHomebrew(entry.Path)))
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<Scenario?> GetScenarioAsync(string id, CancellationToken cancellationToken)
    {
        var index = await IndexScenariosAsync(cancellationToken);
        return index.TryGetValue(id, out var entry) ? entry.Scenario : null;
    }

    public async Task SaveScenarioAsync(Scenario scenario, CancellationToken cancellationToken)
    {
        await _scenariosLock.WaitAsync(cancellationToken);
        try
        {
            var index = await IndexScenariosAsync(cancellationToken);
            var path = index.TryGetValue(scenario.Id, out var existing) ? existing.Path : NewScenarioPath(scenario);
            // With chapters, a campaign folder (CampaignFolder); without, one file. Switching between the two
            // moves the scenario: <name>.json ↔ <name>/.
            var isFolder = path.EndsWith(Path.DirectorySeparatorChar);
            var asFolder = scenario.Chapters is { Count: > 0 };
            var target = (isFolder, asFolder) switch
            {
                (false, true) => Path.ChangeExtension(path, null) + Path.DirectorySeparatorChar,
                (true, false) => path.TrimEnd(Path.DirectorySeparatorChar) + ".json",
                _ => path,
            };
            if (asFolder)
            {
                await CampaignFolder.WriteAsync(target, scenario, cancellationToken);
            }
            else
            {
                await AtomicFile.WriteJsonAsync(target, scenario, cancellationToken);
            }

            if (target != path)
            {
                DeletePath(path);
            }
        }
        finally
        {
            _scenariosLock.Release();
        }
    }

    public async Task<bool> DeleteScenarioAsync(string id, CancellationToken cancellationToken)
    {
        await _scenariosLock.WaitAsync(cancellationToken);
        try
        {
            var index = await IndexScenariosAsync(cancellationToken);
            if (!index.TryGetValue(id, out var entry) || !IsHomebrew(entry.Path))
            {
                return false;
            }

            DeletePath(entry.Path);
            return true;
        }
        finally
        {
            _scenariosLock.Release();
        }
    }

    /// <summary>A scenario file, or a campaign folder (its path ends with a separator).</summary>
    private static void DeletePath(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            System.IO.File.Delete(path);
        }
    }

    /// <summary>
    /// "um1:scenario/blight-of-rhamsted" → scenarios/um1/blight-of-rhamsted.json;
    /// "homebrew:scenario/my-bounty" → the workshop folder (scenarios/homebrew/my-bounty.json).
    /// </summary>
    private string NewScenarioPath(Scenario scenario)
    {
        var id = ContentId.Parse(scenario.Id);
        if (id.Pack == Scenario.HomebrewPack)
        {
            return Path.Combine(HomebrewPath, $"{id.Name}.json");
        }

        var folder = id.Pack ?? scenario.Packs.FirstOrDefault() ?? Scenario.HomebrewPack;
        return Path.Combine(ScenariosPath, folder, $"{id.Name}.json");
    }

    private async Task<Pack> LoadPackAsync(string manifestPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(manifestPath)!;
        var manifest = await ReadAsync<PackManifest>(manifestPath, cancellationToken);

        async Task<T?> File<T>(string kind)
            where T : class =>
            manifest.Files.TryGetValue(kind, out var file)
                ? await ReadAsync<T>(Path.Combine(directory, file), cancellationToken)
                : null;

        var combatants = await File<CombatantsFile>("combatants");
        var items = await File<ItemsFile>("items");
        var tiles = await File<TilesFile>("tiles");
        var tokens = await File<TokensFile>("tokens");
        var initiative = await File<InitiativeFile>("initiative");
        var dice = await File<DiceFile>("dice");
        return new Pack
        {
            Manifest = manifest,
            Combatants = combatants?.Combatants ?? [],
            Items = items?.Items ?? [],
            ItemUpgrades = items?.ItemUpgrades ?? [],
            Tiles = tiles?.Tiles ?? [],
            Tokens = tokens?.Tokens ?? [],
            TokenImageVariants = tokens?.ImageVariants ?? new Dictionary<string, IReadOnlyList<string>>(),
            InitiativeCards = initiative?.InitiativeCards ?? [],
            Dice = dice?.Dice ?? [],
            Rules = await File<PackRules>("rules"),
        };
    }

    private async Task<Dictionary<string, (Scenario Scenario, string Path)>> IndexScenariosAsync(CancellationToken cancellationToken)
    {
        var index = new Dictionary<string, (Scenario, string)>(StringComparer.Ordinal);
        // The workshop folder may live outside the scenarios folder (e.g. a temporary one for tests): then the
        // default one (scenarios/homebrew) is not read as official scenarios.
        var defaultHomebrew = Path.GetFullPath(Path.Combine(ScenariosPath, Scenario.HomebrewPack)) + Path.DirectorySeparatorChar;

        // A folder with a campaign.json is one scenario (CampaignFolder): its files are not scenarios of their own
        var campaigns = new[] { ScenariosPath, HomebrewPath }
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
            .Where(CampaignFolder.IsCampaign)
            .Select(folder => Path.GetFullPath(folder) + Path.DirectorySeparatorChar)
            .Where(folder => IsHomebrew(folder) || !folder.StartsWith(defaultHomebrew, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = new[] { ScenariosPath, HomebrewPath }
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            .Select(Path.GetFullPath)
            .Where(path => IsHomebrew(path) || !path.StartsWith(defaultHomebrew, StringComparison.OrdinalIgnoreCase))
            .Where(path => !campaigns.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            .Concat(campaigns)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order();
        foreach (var path in files)
        {
            Scenario scenario;
            try
            {
                scenario = path.EndsWith(Path.DirectorySeparatorChar)
                    ? await CampaignFolder.ReadAsync(path, cancellationToken)
                    : await ReadAsync<Scenario>(path, cancellationToken);
            }
            catch (InvalidDataException e)
            {
                // One unreadable file (hand-written, half-saved) must not hide every other scenario.
                logger?.LogWarning("Scenario file skipped: {Problem}", e.Message);
                continue;
            }

            if (!index.TryAdd(scenario.Id, (scenario, path)))
            {
                throw new InvalidOperationException($"Scenario id '{scenario.Id}' is used by two files: {index[scenario.Id].Item2} and {path}.");
            }
        }

        return index;
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = System.IO.File.OpenRead(path);
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

    // Layout of the catalogue files (one top-level list per file).
    private sealed record CombatantsFile(IReadOnlyList<Combatant> Combatants);

    private sealed record ItemsFile(IReadOnlyList<Item>? Items, IReadOnlyList<ItemUpgrade>? ItemUpgrades);

    private sealed record TilesFile(IReadOnlyList<Tile> Tiles);

    private sealed record TokensFile(
        IReadOnlyList<TokenType> Tokens,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? ImageVariants);

    private sealed record InitiativeFile(IReadOnlyList<InitiativeCard> InitiativeCards);

    private sealed record DiceFile(IReadOnlyList<Die> Dice);
}
