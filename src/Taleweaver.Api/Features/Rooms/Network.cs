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

    /// <summary>Forces the address of the QR code (IP or host name) when the detection gets it wrong (VPN, unknown adapter).</summary>
    public const string HostVariable = "TALEWEAVER_ROOM_HOST";

    /// <summary>
    /// Where the phones of the same Wi-Fi can reach us: the forced address first, if any, then the private IPv4
    /// addresses of the active interfaces.
    /// </summary>
    public static IReadOnlyList<string> LanAddresses()
    {
        var detected = DetectedAddresses();
        return Environment.GetEnvironmentVariable(HostVariable)?.Trim() is { Length: > 0 } forced
            ? [forced, .. detected.Where(a => a != forced)]
            : detected;
    }

    private static IReadOnlyList<string> DetectedAddresses()
    {
        try
        {
            // Virtual adapters (VMware, VirtualBox, Hyper-V, WSL, Docker) are left out: no phone is on them. The
            // interfaces with a gateway (the Wi-Fi, the box) come first: the QR code uses the first address.
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && !IsVirtual(n))
                .OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                .ThenByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
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

    private static readonly string[] VirtualAdapters = ["vmware", "virtualbox", "hyper-v", "vethernet", "wsl", "docker", "loopback", "tap-", "tunnel"];

    private static bool IsVirtual(NetworkInterface network) =>
        VirtualAdapters.Any(v => network.Name.Contains(v, StringComparison.OrdinalIgnoreCase) || network.Description.Contains(v, StringComparison.OrdinalIgnoreCase));

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
