using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// Who is the GM: the device that hosts the app (localhost). Behind the Vite dev server, the proxy forwards the
/// phone's address in X-Forwarded-For; it is trusted only when the request itself comes from this machine, and
/// only its last value: the proxy appends it after whatever the client wrote in the header itself.
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

        if (!context.Request.Headers.TryGetValue("X-Forwarded-For", out var header))
        {
            return remote;
        }

        // Unreadable: somebody else's address, never the GM's (fail closed)
        var last = header.ToString().Split(',')[^1].Trim();
        return IPAddress.TryParse(last, out var client) ? client : IPAddress.None;
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
        if (IsLocal(context))
        {
            await next(context);
            return;
        }

        var guesses = context.RequestServices.GetRequiredService<CodeGuessLimiter>();
        var device = ClientAddress(context);
        var namesARoom = context.Request.Path.StartsWithSegments("/api/rooms");
        if ((namesARoom || context.Request.Path.StartsWithSegments("/hubs/room")) && guesses.IsBlocked(device))
        {
            context.Response.Headers.RetryAfter = ((int)CodeGuessLimiter.Window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Rooms.TooManyTries", detail: "Too many unknown codes: wait a minute.")
                .ExecuteAsync(context);
            return;
        }

        if (await AllowedFromAPhone(context) || await AllowedFromTheScreen(context))
        {
            await next(context);
            if (namesARoom && context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                guesses.Failed(device);
            }

            return;
        }

        var problem = Common.ResultExtensions.ToProblem(RoomService.Errors.GmOnly);
        await problem.ExecuteAsync(context);
    });

    /// <summary>The second screen's token: "Authorization: Screen &lt;token&gt;", or ?screen_token= for the hub.</summary>
    public static string? ScreenToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Screen ", StringComparison.Ordinal)
            ? header["Screen ".Length..].Trim()
            : context.Request.Query["screen_token"].ToString() is { Length: > 0 } query ? query : null;
    }

    /// <summary>
    /// The GM's second screen acts as the GM for its own game (and reads the catalogue, the scenarios, the images),
    /// while the room is open; never for another game nor the list of games.
    /// </summary>
    private static async Task<bool> AllowedFromTheScreen(HttpContext context)
    {
        if (await context.RequestServices.GetRequiredService<RoomService>().ScreenGameAsync(ScreenToken(context)) is not { } gameId)
        {
            return false;
        }

        var path = context.Request.Path;
        return path.StartsWithSegments($"/api/games/{gameId}")
            || path.StartsWithSegments("/hubs/room")
            || (HttpMethods.IsGet(context.Request.Method) && !path.StartsWithSegments("/api/games"));
    }

    private static readonly string[] PhoneCatalog =
    [
        "/api/catalog/items", "/api/catalog/item-upgrades", "/api/catalog/disciplines", "/api/catalog/adventurers",
        "/api/catalog/effects", "/api/catalog/initiative", "/api/catalog/board",
    ];

    private static async Task<bool> AllowedFromAPhone(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/rooms") || path.StartsWithSegments("/hubs/room"))
        {
            return true;
        }

        // The images and the tables a phone draws with: never the search of the whole catalogue (every combatant, the bosses too).
        var readable = path.StartsWithSegments("/api/assets") || PhoneCatalog.Any(table => path.Equals(table, StringComparison.OrdinalIgnoreCase));
        return readable && HttpMethods.IsGet(context.Request.Method)
            && (await context.RequestServices.GetRequiredService<RoomService>().OpenRoomsAsync()).Count > 0;
    }
}
