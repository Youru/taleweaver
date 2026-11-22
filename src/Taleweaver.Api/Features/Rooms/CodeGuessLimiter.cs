using System.Collections.Concurrent;
using System.Net;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>Room codes cannot be guessed by trying them: a device that names too many unknown rooms waits a minute.</summary>
public sealed class CodeGuessLimiter(TimeProvider time)
{
    public const int MaxFailures = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<IPAddress, (int Count, DateTimeOffset Since)> _failures = new();

    public bool IsBlocked(IPAddress? device) =>
        device is not null && _failures.TryGetValue(device, out var failed) && failed.Count >= MaxFailures && time.GetUtcNow() - failed.Since < Window;

    public void Failed(IPAddress? device)
    {
        if (device is null)
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var (address, failed) in _failures)
        {
            if (now - failed.Since >= Window)
            {
                _failures.TryRemove(address, out _);
            }
        }

        _failures.AddOrUpdate(device, (1, now), (_, failed) => (failed.Count + 1, failed.Since));
    }
}
