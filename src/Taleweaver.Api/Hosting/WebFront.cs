using Microsoft.Extensions.FileProviders;

namespace Taleweaver.Api.Hosting;

/// <summary>
/// The built front (wwwroot of the content root) when there is one; in development Vite serves it.
/// It runs before the room guard: the pages hold no secret, everything secret comes from /api.
/// </summary>
public static class WebFront
{
    public static WebApplication UseWebFront(this WebApplication app)
    {
        var folder = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (!File.Exists(Path.Combine(folder, "index.html")))
        {
            return app;
        }

        app.UseWhen(IsPage, front =>
        {
            front.Use((context, next) =>
            {
                // A path without an extension is a route of the SPA
                if (!Path.HasExtension(context.Request.Path.Value))
                {
                    context.Request.Path = "/index.html";
                }

                // The built files carry a hash in their name; the page that names them must always be fresh
                context.Response.Headers.CacheControl = context.Request.Path.StartsWithSegments("/assets")
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
                return next(context);
            });
            front.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(folder) });
            front.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        });
        return app;
    }

    private static bool IsPage(HttpContext context) =>
        (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        && !context.Request.Path.StartsWithSegments("/api")
        && !context.Request.Path.StartsWithSegments("/hubs")
        && !context.Request.Path.StartsWithSegments("/openapi");
}
