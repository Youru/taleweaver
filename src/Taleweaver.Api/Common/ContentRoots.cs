using Microsoft.Extensions.Options;

namespace Taleweaver.Api.Common;

/// <summary>
/// The folders the content is read from, in order: the main one (<see cref="ContentOptions.RootPath"/>), the extra
/// ones, then each installed content pack. Read again on each call: a pack installed while the app runs is seen.
/// </summary>
public sealed class ContentRoots(IOptions<ContentOptions> options, IHostEnvironment environment)
{
    private readonly ContentOptions _options = options.Value;

    public string Main => Path.GetFullPath(_options.RootPath, environment.ContentRootPath);

    /// <summary>Null when the app installs no packs.</summary>
    public string? InstalledPacks =>
        string.IsNullOrWhiteSpace(_options.InstalledPacksPath) ? null : Path.GetFullPath(_options.InstalledPacksPath, environment.ContentRootPath);

    public IReadOnlyList<string> All
    {
        get
        {
            var roots = new List<string> { Main };
            roots.AddRange(_options.ExtraRoots.Select(r => Path.GetFullPath(r, environment.ContentRootPath)));
            if (InstalledPacks is { } installed && Directory.Exists(installed))
            {
                // A folder being installed ends with .partial: never read half a pack
                roots.AddRange(Directory.EnumerateDirectories(installed).Where(d => !d.EndsWith(".partial", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
            }

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
