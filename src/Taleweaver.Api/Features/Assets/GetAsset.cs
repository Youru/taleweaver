using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.Api.Features.Assets;

/// <summary>
/// Serves the game's images (tiles, tokens, cards, figures) read-only from the local assets folder.
/// The catalogue gives them as "assets/act1/tiles/um01.jpg": the front asks for "/api/assets/act1/tiles/um01.jpg".
/// These images belong to Succubus Publishing: the API only reads them from the user's own disk.
/// </summary>
public static class GetAsset
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>The widths a picture can be asked in (room-spec §10.3): a thumbnail, a phone, a large screen.</summary>
    public static readonly IReadOnlyList<int> Widths = [160, 320, 640, 1280];

    public sealed class Handler(IOptions<ContentOptions> options, IWebHostEnvironment environment)
    {
        private readonly ContentRoots _roots = new(options, environment);

        /// <summary>The assets folders, in the order of the content folders: the first that has the picture serves it.</summary>
        private IEnumerable<string> Folders => _roots.All.Select(r => Path.GetFullPath(Path.Combine(r, options.Value.AssetsDirectory)));

        /// <summary>Resized pictures, next to the saves (never in the repository): rebuilt when the original changes.</summary>
        private readonly string _cache = Path.Combine(
            string.IsNullOrWhiteSpace(options.Value.SavesPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Middara")
                : options.Value.SavesPath,
            "cache",
            "images");

        private readonly SemaphoreSlim _resizing = new(2);

        /// <summary>
        /// The picture at most <paramref name="width"/> wide (each format stays itself: PNG and WebP keep their transparency);
        /// the original when it is already that small or cannot be read.
        /// </summary>
        public async Task<string> ResizedAsync(string original, string name, int width, CancellationToken cancellationToken)
        {
            var relative = Path.ChangeExtension(name["assets/".Length..], Path.GetExtension(original));
            var target = Path.Combine(_cache, width.ToString(System.Globalization.CultureInfo.InvariantCulture), relative);
            if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(original))
            {
                return target;
            }

            await _resizing.WaitAsync(cancellationToken);
            try
            {
                using var source = SKBitmap.Decode(original);
                if (source is null || source.Width <= width)
                {
                    return original;
                }

                var height = (int)Math.Round(source.Height * (double)width / source.Width);
                using var resized = source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (resized is null)
                {
                    return original;
                }

                var (format, quality) = Path.GetExtension(original).ToLowerInvariant() switch
                {
                    ".png" => (SKEncodedImageFormat.Png, 100),
                    ".webp" => (SKEncodedImageFormat.Webp, 82),
                    _ => (SKEncodedImageFormat.Jpeg, 82),
                };
                using var data = resized.Encode(format, quality);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // Written aside then moved: a request at the same time never reads half a file
                var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(temporary, data.ToArray(), cancellationToken);
                File.Move(temporary, target, overwrite: true);
                return target;
            }
            finally
            {
                _resizing.Release();
            }
        }

        /// <summary>"assets/act1/tiles/um01.jpg": a file found, named as the catalogue names it.</summary>
        private static string NameOf(string root, string file) => "assets/" + Path.GetRelativePath(root, file).Replace('\\', '/');

        /// <summary>
        /// The file for a path relative to the assets folder; null when it is outside the folder, not an image or missing.
        /// The packaged app holds most pictures as WebP of the same name: the catalogue and the saves keep theirs,
        /// so Name stays the one that was asked.
        /// </summary>
        public (string Path, string ContentType, string Name)? Find(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal))
            {
                return null;
            }

            foreach (var root in Folders)
            {
                var full = Path.GetFullPath(Path.Combine(root, path));
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !ContentTypes.TryGetContentType(full, out var contentType)
                    || !contentType.StartsWith("image/", StringComparison.Ordinal))
                {
                    return null;
                }

                if (File.Exists(full))
                {
                    return (full, contentType, NameOf(root, full));
                }

                var compressed = Path.ChangeExtension(full, ".webp");
                if (contentType is "image/jpeg" or "image/png" && File.Exists(compressed))
                {
                    return (compressed, "image/webp", NameOf(root, full));
                }
            }

            return null;
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/assets/{**path}", async Task<Results<PhysicalFileHttpResult, NotFound>> (string path, int? w, Handler handler, HttpContext context, PhoneCards phones, CancellationToken ct) =>
            {
                if (handler.Find(path) is not { } file)
                {
                    return TypedResults.NotFound();
                }

                // A phone: only the pictures it may see (a path is easily guessed)
                if (await phones.ForAsync(context, ct) is { } sight && !sight.SeesPicture(file.Name))
                {
                    return TypedResults.NotFound();
                }

                // ?w=320: a smaller copy for the phones and the grids (room-spec §10.3); any other width, the original
                var served = w is { } width && Widths.Contains(width) && file.ContentType is "image/jpeg" or "image/png" or "image/webp"
                    ? await handler.ResizedAsync(file.Path, file.Name, width, ct)
                    : file.Path;

                // The images never change while the app runs.
                context.Response.Headers.CacheControl = "private, max-age=86400";
                return TypedResults.PhysicalFile(served, file.ContentType);
            })
            .WithTags("Assets")
            .WithName("GetAsset")
            .ExcludeFromDescription();

    public static IServiceCollection AddAssetsFeature(this IServiceCollection services) => services.AddSingleton<Handler>();
}
