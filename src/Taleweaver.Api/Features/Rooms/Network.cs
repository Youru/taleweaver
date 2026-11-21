using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// Who is the GM: the device that hosts the app (localhost). Behind the Vite dev server, the proxy forwards the
/// phone's address in X-Forwarded-For; it is trusted only when the request itself comes from this machine.
/// </summary>
public static class Network
{
    public static IPAddress? ClientAddress(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && !IPAddress.IsLoopback(remote))
        {
            return remote;
        }

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return forwarded.Length > 0 && IPAddress.TryParse(forwarded[0], out var client) ? client : remote;
    }

    /// <summary>A request from this machine (an in-process test server has no address: local too).</summary>
    public static bool IsLocal(HttpContext context) => ClientAddress(context) is not { } address || IPAddress.IsLoopback(address);

    /// <summary>The private IPv4 addresses of the active interfaces: where the phones of the same Wi-Fi can reach us.</summary>
    public static IReadOnlyList<string> LanAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a))
                .Select(a => a.ToString())
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>
    /// Phones reach only the room: /api/rooms/…, the hub and, while a room is open, the images and the catalogue. Every other route
    /// (the GM's) answers 403 Rooms.GmOnly to a request from another device.
    /// </summary>
    public static IApplicationBuilder UseRoomGuard(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (IsLocal(context) || await AllowedFromAPhone(context))
        {
            await next(context);
            return;
        }

        var problem = Common.ResultExtensions.ToProblem(RoomService.Errors.GmOnly);
        await problem.ExecuteAsync(context);
    });

    private static async Task<bool> AllowedFromAPhone(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/rooms") || path.StartsWithSegments("/hubs/room"))
        {
            return true;
        }

        // The images and the catalogue (item cards, adventurer cards): nothing of the game in them.
        return (path.StartsWithSegments("/api/assets") || path.StartsWithSegments("/api/catalog")) && HttpMethods.IsGet(context.Request.Method)
            && (await context.RequestServices.GetRequiredService<RoomService>().OpenRoomsAsync()).Count > 0;
    }
}
