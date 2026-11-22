using System.Net;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.Api.Hosting;

/// <summary>What protects the GM's computer from the web sites its browser visits.</summary>
public static class RequestGuards
{
    public static class Errors
    {
        public static readonly Error ForeignHost = Error.Forbidden("App.ForeignHost", "Open the app by localhost or by the address of this computer.");

        public static readonly Error ForeignOrigin = Error.Forbidden("App.ForeignOrigin", "Another web site cannot act on this app.");
    }

    /// <summary>
    /// The packaged app listens to the Wi-Fi, so any address is a good Host; a name is not (DNS rebinding: a site
    /// whose name is pointed at this computer), except localhost and the one forced for the room.
    /// </summary>
    public static bool IsKnownHost(string host, string? forced) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || IPAddress.TryParse(host.Trim('[', ']'), out _)
        || (forced is { Length: > 0 } && host.Equals(forced.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The GM has no token: being on this computer is enough, so a page of another site in the same browser must not
    /// write nor open the hub. Phones and linked screens send tokens, which no other site has.
    /// </summary>
    public static bool IsOwnOrigin(string origin, HostString host) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var from)
        && from.Scheme is "http" or "https"
        && (from.IsLoopback || from.Authority.Equals(host.Value, StringComparison.OrdinalIgnoreCase));

    public static IApplicationBuilder UseHostGuard(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (IsKnownHost(context.Request.Host.Host, Environment.GetEnvironmentVariable(Network.HostVariable)))
        {
            await next(context);
            return;
        }

        await Errors.ForeignHost.ToProblem().ExecuteAsync(context);
    });

    public static IApplicationBuilder UseOriginGuard(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var request = context.Request;
        var acts = !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) || request.Path.StartsWithSegments("/hubs");
        var origin = request.Headers.Origin.ToString();
        if (!acts || origin.Length == 0 || !Network.IsLocal(context) || IsOwnOrigin(origin, request.Host))
        {
            await next(context);
            return;
        }

        await Errors.ForeignOrigin.ToProblem().ExecuteAsync(context);
    });
}
