using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;

namespace Taleweaver.Api.Hosting;

/// <summary>
/// The app as it is given away (taleweaver/scripts/package.py): one executable that carries the front and the content.
/// It unpacks them beside the user's data at the first launch, whatever folder it is started from.
/// Without that bundle (dotnet run, tests), none of this applies.
/// </summary>
public sealed class PackagedApp
{
    /// <summary>Where the games, scenarios and unpacked content go, in place of the user's folders.</summary>
    public const string DataVariable = "TALEWEAVER_DATA";

    /// <summary>The first port to try.</summary>
    public const string PortVariable = "TALEWEAVER_PORT";

    /// <summary>Set: the browser is not opened.</summary>
    public const string NoBrowserVariable = "TALEWEAVER_NO_BROWSER";

    private const string BundleName = "content.zip";
    private const int DefaultPort = 5180;
    private const int PortsTried = 20;

    private readonly FileStream _lock;
    private readonly string _root;
    private readonly string _data;
    private readonly string _local;
    private readonly int _port;

    private PackagedApp(FileStream instanceLock, string root, string data, string local, int port)
    {
        _lock = instanceLock;
        _root = root;
        _data = data;
        _local = local;
        _port = port;
    }

    public static bool IsPackaged => typeof(PackagedApp).Assembly.GetManifestResourceInfo(BundleName) is not null;

    private string Url => $"http://localhost:{_port}";

    /// <summary>Null when the app already runs: its page is opened instead.</summary>
    public static PackagedApp? Start()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var chosen = Environment.GetEnvironmentVariable(DataVariable);
        var custom = !string.IsNullOrWhiteSpace(chosen);
        var data = custom ? Path.GetFullPath(chosen!) : UserFolder(Environment.SpecialFolder.ApplicationData);
        var local = custom ? data : UserFolder(Environment.SpecialFolder.LocalApplicationData);
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(local);

        var address = Path.Combine(data, "app.url");
        FileStream instanceLock;
        try
        {
            instanceLock = new FileStream(Path.Combine(data, "app.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            Console.WriteLine("Taleweaver est déjà lancé. / Taleweaver is already running.");
            // Only ever our own page: the file is not a way to launch something else
            if (File.Exists(address) && int.TryParse(File.ReadAllText(address), out var running))
            {
                OpenBrowser($"http://localhost:{running}");
            }

            return null;
        }

        Console.WriteLine("Taleweaver démarre (un peu plus long la première fois)… / Starting (a little longer the first time)…");
        var root = Unpack(Path.Combine(local, "app"));
        var port = FreePort(int.TryParse(Environment.GetEnvironmentVariable(PortVariable), out var first) ? first : DefaultPort);
        File.WriteAllText(address, port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new PackagedApp(instanceLock, root, data, local, port);
    }

    /// <summary>The settings come from here, never from an appsettings.json lying where the executable was started.</summary>
    public WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _root, EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Content:RootPath"] = _root,
            ["Content:SavesPath"] = Path.Combine(_data, "saves"),
            ["Content:HomebrewDirectory"] = Path.Combine(_data, "scenarios"),
            // Content packs: imported from the app, or a *.taleweaver.zip dropped beside the executable
            ["Content:InstalledPacksPath"] = Path.Combine(_local, "content-packs"),
            ["Content:DroppedPacksPath"] = AppContext.BaseDirectory,
            // HostGuard checks the names: the addresses of the Wi-Fi are not known in advance
            ["AllowedHosts"] = "*",
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.WebHost.UseUrls($"http://*:{_port}");
        return builder;
    }

    public void Run(WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var version = typeof(PackagedApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Console.WriteLine($"""

                Taleweaver {version}
                  {Url}

                Gardez cette fenêtre ouverte pendant la partie ; fermez-la pour quitter.
                Keep this window open while you play; close it to quit.
                Données / Data : {_data}

                """);
            foreach (var address in Features.Rooms.Network.LanAddresses())
            {
                Console.WriteLine($"  Téléphones, tablette / Phones, tablet : http://{address}:{_port}/join");
            }

            OpenBrowser(Url);
        });
        app.Run();
        _lock.Dispose();
    }

    private static string UserFolder(Environment.SpecialFolder folder) =>
        // Create: on Linux the folder is "" as long as it does not exist
        Path.Combine(Environment.GetFolderPath(folder, Environment.SpecialFolderOption.Create), "Middara");

    /// <summary>One folder per bundle: an update never mixes two contents; the older ones are removed.</summary>
    private static string Unpack(string folder)
    {
        using var bundle = typeof(PackagedApp).Assembly.GetManifestResourceStream(BundleName)!;
        var id = Convert.ToHexStringLower(SHA256.HashData(bundle))[..16];
        var target = Path.Combine(folder, id);
        if (!Directory.Exists(target))
        {
            var partial = target + ".partial";
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }

            bundle.Position = 0;
            using (var zip = new ZipArchive(bundle, ZipArchiveMode.Read))
            {
                zip.ExtractToDirectory(partial);
            }

            Directory.Move(partial, target);
        }

        // Only what this method made: 16 hexadecimal characters
        foreach (var old in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(old);
            if (name != id && name.Length >= 16 && name[..16].All(char.IsAsciiHexDigitLower))
            {
                try
                {
                    Directory.Delete(old, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"Ancienne version gardée / Older version kept: {old}");
                }
            }
        }

        return target;
    }

    internal static int FreePort(int first)
    {
        // Binding is not proof: Windows lets an IPv4 socket over a port held on [::] or on one address only,
        // and Kestrel then fails. So any listener on the port, whatever its address, makes it taken.
        var taken = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(l => l.Port).ToHashSet();
        for (var port = first; port < first + PortsTried; port++)
        {
            if (!taken.Contains(port) && CanListen(IPAddress.Any, port))
            {
                return port;
            }
        }

        throw new InvalidOperationException($"No free port from {first} to {first + PortsTried - 1}: set {PortVariable}.");
    }

    private static bool CanListen(IPAddress address, int port)
    {
        try
        {
            using var listener = new TcpListener(address, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void OpenBrowser(string url)
    {
        if (Environment.GetEnvironmentVariable(NoBrowserVariable) is { Length: > 0 })
        {
            return;
        }

        try
        {
            using var started = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"Ouvrez / Open {url}");
        }
    }
}
