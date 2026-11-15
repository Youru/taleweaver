using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Features.Assets;

/// <summary>
/// Serves the game's images (tiles, tokens, cards, figures) read-only from the local assets folder.
/// The catalogue gives them as "assets/act1/tiles/um01.jpg": the front asks for "/api/assets/act1/tiles/um01.jpg".
/// These images belong to Succubus Publishing: the API only reads them from the user's own disk.
/// </summary>
public static class GetAsset
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public sealed class Handler(IOptions<ContentOptions> options, IWebHostEnvironment environment)
    {
        private readonly string _root = Path.GetFullPath(
            Path.Combine(Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath), options.Value.AssetsDirectory));

        /// <summary>The file for a path relative to the assets folder; null when it is outside the folder, not an image or missing.</summary>
        public (string Path, string ContentType)? Find(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal))
            {
                return null;
            }

            var full = Path.GetFullPath(Path.Combine(_root, path));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !ContentTypes.TryGetContentType(full, out var contentType)
                || !contentType.StartsWith("image/", StringComparison.Ordinal)
                || !File.Exists(full))
            {
                return null;
            }

            return (full, contentType);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/assets/{**path}", Results<PhysicalFileHttpResult, NotFound> (string path, Handler handler, HttpContext context) =>
            {
                if (handler.Find(path) is not { } file)
                {
                    return TypedResults.NotFound();
                }

                // The images never change while the app runs.
                context.Response.Headers.CacheControl = "private, max-age=86400";
                return TypedResults.PhysicalFile(file.Path, file.ContentType);
            })
            .WithTags("Assets")
            .WithName("GetAsset")
            .ExcludeFromDescription();

    public static IServiceCollection AddAssetsFeature(this IServiceCollection services) => services.AddSingleton<Handler>();
}
