using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.ContentPacks;

/// <summary>
/// Content packs: a .zip made apart from the app (scripts/package.py --content-pack) that brings packs, scenarios,
/// pictures and rules. The app is published without any game content; whoever owns one adds it here.
/// <para>
/// A pack file holds content-pack.json (id, name, version) and the folders packs/, scenarios/, assets/, reference/.
/// It is unpacked into <see cref="ContentOptions.InstalledPacksPath"/>/&lt;id&gt;, one of the <see cref="ContentRoots"/>.
/// </para>
/// </summary>
public static partial class ContentPackFiles
{
    public const string ManifestName = "content-pack.json";

    /// <summary>A file dropped beside the app (or in <see cref="ContentOptions.DroppedPacksPath"/>) is installed at start.</summary>
    public const string DroppedPattern = "*.taleweaver.zip";

    private const string InstalledName = ".installed.json";

    private static readonly string[] Folders = ["packs/", "scenarios/", "assets/", "reference/"];

    private static readonly string[] Extensions = [".json", ".md", ".png", ".jpg", ".jpeg", ".webp", ".svg"];

    /// <summary>What a pack file declares.</summary>
    public sealed record Manifest(string Id, LocalizedText Name, string? Version);

    /// <param name="Packs">The catalogue packs it brings (packs/&lt;id&gt;/pack.json).</param>
    public sealed record Installed(string Id, LocalizedText Name, string? Version, IReadOnlyList<string> Packs, int Scenarios, int Pictures);

    private sealed record Stamp(string Sha256);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdPattern();

    public static class Errors
    {
        public static readonly Error NotInstalling = Error.Conflict("ContentPacks.NotInstalling", "This app installs no content packs.");

        public static Error Unreadable(string why) => Error.Validation("ContentPacks.Unreadable", $"Not a content pack: {why}");

        public static Error NotFound(string id) => Error.NotFound("ContentPacks.NotFound", $"No content pack '{id}'.");
    }

    public sealed class Installer(ContentRoots roots, CatalogProvider catalogs, IOptions<ContentOptions> options, IHostEnvironment environment, ILogger<Installer> logger)
    {
        private readonly SemaphoreSlim _lock = new(1, 1);

        public IReadOnlyList<Installed> List()
        {
            if (roots.InstalledPacks is not { } folder || !Directory.Exists(folder))
            {
                return [];
            }

            return Directory.EnumerateDirectories(folder)
                .Where(d => !d.EndsWith(".partial", StringComparison.Ordinal) && File.Exists(Path.Combine(d, ManifestName)))
                .Order(StringComparer.Ordinal)
                .Select(Describe)
                .ToList();
        }

        /// <summary>Installs (or replaces) the pack of this file. The file is read whole first: a bad one changes nothing.</summary>
        public async Task<Result<Installed>> InstallAsync(Stream file, CancellationToken cancellationToken)
        {
            if (roots.InstalledPacks is not { } folder)
            {
                return Errors.NotInstalling;
            }

            // ZipArchive needs to seek: the upload goes to a temporary file
            var temporary = Path.GetTempFileName();
            try
            {
                await using (var copy = File.Create(temporary))
                {
                    await file.CopyToAsync(copy, cancellationToken);
                }

                return await InstallFileAsync(temporary, folder, cancellationToken);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        public async Task<Result> RemoveAsync(string id, CancellationToken cancellationToken)
        {
            if (roots.InstalledPacks is not { } folder)
            {
                return Errors.NotInstalling;
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var target = Path.Combine(folder, id);
                if (!IdPattern().IsMatch(id) || !Directory.Exists(target))
                {
                    return Errors.NotFound(id);
                }

                Directory.Delete(target, recursive: true);
                catalogs.Invalidate();
                return Result.Success();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>At start: the pack files dropped beside the app, installed once (again when the file changes).</summary>
        public async Task InstallDroppedAsync(CancellationToken cancellationToken)
        {
            if (roots.InstalledPacks is not { } folder || string.IsNullOrWhiteSpace(options.Value.DroppedPacksPath))
            {
                return;
            }

            var dropped = Path.GetFullPath(options.Value.DroppedPacksPath, environment.ContentRootPath);
            if (!Directory.Exists(dropped))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(dropped, DroppedPattern).Order(StringComparer.Ordinal))
            {
                var hash = await HashAsync(file, cancellationToken);
                if (Directory.EnumerateDirectories(folder).Any(d => ReadStamp(d)?.Sha256 == hash))
                {
                    continue;
                }

                var result = await InstallFileAsync(file, folder, cancellationToken);
                if (result.IsSuccess)
                {
                    logger.LogInformation("Content pack installed: {Pack} ({File})", result.Value.Id, Path.GetFileName(file));
                }
                else
                {
                    logger.LogWarning("Content pack not installed ({File}): {Problem}", Path.GetFileName(file), result.Error!.Message);
                }
            }
        }

        private async Task<Result<Installed>> InstallFileAsync(string file, string folder, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                Manifest manifest;
                using (var archive = OpenOrNull(file))
                {
                    if (archive is null)
                    {
                        return Errors.Unreadable("not a .zip file");
                    }

                    if (Check(archive) is { } problem)
                    {
                        return Errors.Unreadable(problem);
                    }

                    try
                    {
                        await using var stream = archive.GetEntry(ManifestName)!.Open();
                        manifest = await JsonSerializer.DeserializeAsync<Manifest>(stream, JsonDefaults.Options, cancellationToken)
                                   ?? throw new JsonException("empty");
                    }
                    catch (JsonException e)
                    {
                        return Errors.Unreadable($"{ManifestName}: {e.Message}");
                    }

                    if (manifest.Id is null || !IdPattern().IsMatch(manifest.Id))
                    {
                        return Errors.Unreadable($"{ManifestName}: the id must be lowercase letters, digits and dashes");
                    }

                    // Unpacked aside, then swapped: a reader never meets half a pack
                    Directory.CreateDirectory(folder);
                    var partial = Path.Combine(folder, manifest.Id + ".partial");
                    if (Directory.Exists(partial))
                    {
                        Directory.Delete(partial, recursive: true);
                    }

                    archive.ExtractToDirectory(partial);
                    await File.WriteAllTextAsync(Path.Combine(partial, InstalledName),
                        JsonSerializer.Serialize(new Stamp(await HashAsync(file, cancellationToken)), JsonDefaults.Options), cancellationToken);
                    var target = Path.Combine(folder, manifest.Id);
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, recursive: true);
                    }

                    Directory.Move(partial, target);
                }

                catalogs.Invalidate();
                return Describe(Path.Combine(folder, manifest.Id));
            }
            finally
            {
                _lock.Release();
            }
        }

        private static ZipArchive? OpenOrNull(string file)
        {
            try
            {
                return ZipFile.OpenRead(file);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>Only content: known folders, known kinds of file, nothing that climbs out of the folder.</summary>
        private static string? Check(ZipArchive archive)
        {
            if (archive.GetEntry(ManifestName) is null)
            {
                return $"no {ManifestName} at its root";
            }

            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name == ManifestName || name.EndsWith('/'))
                {
                    continue;
                }

                if (name.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(name) || name.StartsWith('/'))
                {
                    return $"'{name}' points out of the pack";
                }

                if (!Folders.Any(f => name.StartsWith(f, StringComparison.Ordinal)))
                {
                    return $"'{name}' is not in {string.Join(", ", Folders)}";
                }

                if (!Extensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
                {
                    return $"'{name}': only {string.Join(", ", Extensions)} files";
                }
            }

            return null;
        }

        private static Installed Describe(string directory)
        {
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory, ManifestName)), JsonDefaults.Options)!;
            var packs = Path.Combine(directory, "packs");
            var scenarios = Path.Combine(directory, "scenarios");
            var assets = Path.Combine(directory, "assets");
            return new Installed(
                manifest.Id,
                manifest.Name,
                manifest.Version,
                Directory.Exists(packs)
                    ? Directory.EnumerateFiles(packs, "pack.json", SearchOption.AllDirectories).Select(p => Path.GetFileName(Path.GetDirectoryName(p)!)).Order(StringComparer.Ordinal).ToList()
                    : [],
                Directory.Exists(scenarios) ? Directory.EnumerateFiles(scenarios, "*.json", SearchOption.AllDirectories).Count() : 0,
                Directory.Exists(assets) ? Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories).Count() : 0);
        }

        private static Stamp? ReadStamp(string directory)
        {
            var path = Path.Combine(directory, InstalledName);
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<Stamp>(File.ReadAllText(path), JsonDefaults.Options) : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static async Task<string> HashAsync(string file, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(file);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        }
    }

    public static IServiceCollection AddContentPacksFeature(this IServiceCollection services) =>
        services.AddSingleton<ContentRoots>().AddSingleton<Installer>();

    /// <summary>The GM's only (Network.UseRoomGuard): no phone or tablet reaches these routes.</summary>
    public static IEndpointRouteBuilder MapContentPacksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content-packs").WithTags("ContentPacks");
        group.MapGet("/", (Installer installer) => TypedResults.Ok(installer.List())).WithName("ListContentPacks");

        // The file is the body (application/zip): a pack weighs hundreds of MB, more than a form allows
        group.MapPost("/", async Task<Results<Ok<Installed>, ProblemHttpResult>> (HttpRequest request, Installer installer, CancellationToken cancellationToken) =>
            {
                var result = await installer.InstallAsync(request.Body, cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
            })
            .WithMetadata(new Microsoft.AspNetCore.Mvc.DisableRequestSizeLimitAttribute())
            .WithName("InstallContentPack");

        group.MapDelete("/{id}", async Task<Results<NoContent, ProblemHttpResult>> (string id, Installer installer, CancellationToken cancellationToken) =>
            {
                var result = await installer.RemoveAsync(id, cancellationToken);
                return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
            })
            .WithName("RemoveContentPack");
        return app;
    }
}
