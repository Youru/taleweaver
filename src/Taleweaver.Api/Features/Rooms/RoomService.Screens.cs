using System.Security.Cryptography;
using System.Text;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// Linking screens: the QR code carries a token good once, exchanged for the device's own. A photograph of a code
/// already scanned is worth nothing; only hashes are kept.
/// </summary>
public sealed partial class RoomService
{
    public const int MaxTables = 8;

    public static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(10);

    /// <summary>A screen lost its link: it is told at once (the argument is its token's hash).</summary>
    public event Func<string, Task>? ScreenRevoked;

    public async Task<Result<string>> CreatePairingAsync(string gameId, string role, CancellationToken cancellationToken)
    {
        if (role is not (ScreenRole.Gm or ScreenRole.Table))
        {
            return Errors.UnknownRole;
        }

        var token = NewToken();
        var (room, error) = await Locked<(Room?, Error?)>(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { IsOpen: true } room)
            {
                return (null, Errors.NoRoom(gameId));
            }

            if (role == ScreenRole.Table && room.TableHashes.Count >= MaxTables)
            {
                return (null, Errors.TooManyTables);
            }

            room.Pairings = [.. room.Pairings.Where(p => p.Role != role), new ScreenPairing(role, Hash(token), time.GetUtcNow() + PairingLifetime)];
            return (room, null);
        });
        if (error is not null)
        {
            return error;
        }

        await SaveAndNotify(room!, cancellationToken);
        return token;
    }

    /// <summary>The scan: the QR code's token against the device's. A new GM tablet replaces the previous one, only now.</summary>
    public async Task<Result<(Room Room, string Role, string Token)>> ClaimScreenAsync(string? pairingToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pairingToken))
        {
            return Errors.ScreenLinkUsed;
        }

        var scanned = Hash(pairingToken);
        var token = NewToken();
        var (room, role, replaced) = await Locked<(Room?, string, string?)>(rooms =>
        {
            foreach (var room in rooms.Values.Where(r => r.IsOpen))
            {
                if (room.Pairings.FirstOrDefault(p => p.ExpiresAt > time.GetUtcNow() && Same(p.TokenHash, scanned)) is not { } pairing)
                {
                    continue;
                }

                string? replaced = null;
                if (pairing.Role == ScreenRole.Gm)
                {
                    replaced = room.GmScreenHash;
                    room.GmScreenHash = Hash(token);
                }
                else
                {
                    room.TableHashes = [.. room.TableHashes, Hash(token)];
                }

                room.Pairings = [.. room.Pairings.Where(p => p != pairing)];
                return (room, pairing.Role, replaced);
            }

            return (null, "", null);
        });
        if (room is null)
        {
            return Errors.ScreenLinkUsed;
        }

        await SaveAndNotify(room, cancellationToken);
        await TellRevoked([replaced]);
        return (room, role, token);
    }

    /// <summary>The screens of a role stop working at once, and so does its QR code.</summary>
    public async Task<Result<Room>> RevokeScreensAsync(string gameId, string role, CancellationToken cancellationToken)
    {
        if (role is not (ScreenRole.Gm or ScreenRole.Table))
        {
            return Errors.UnknownRole;
        }

        var (room, revoked) = await Locked<(Room?, List<string?>)>(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return (null, []);
            }

            List<string?> revoked = role == ScreenRole.Gm ? [room.GmScreenHash] : [.. room.TableHashes];
            if (role == ScreenRole.Gm)
            {
                room.GmScreenHash = null;
            }
            else
            {
                room.TableHashes = [];
            }

            room.Pairings = [.. room.Pairings.Where(p => p.Role != role)];
            return (room, revoked);
        });
        if (room is null)
        {
            return Errors.NoRoom(gameId);
        }

        await SaveAndNotify(room, cancellationToken);
        await TellRevoked(revoked);
        return room;
    }

    /// <summary>The game whose open room has this GM tablet's token, or null.</summary>
    public async Task<string?> ScreenGameAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var hash = Hash(token);
        return await Locked(rooms => rooms.Values.FirstOrDefault(r => r.IsOpen && r.GmScreenHash is { } known && Same(known, hash))?.GameId);
    }

    /// <summary>The open room of a table screen's token.</summary>
    public async Task<Result<Room>> TableRoomAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Errors.ScreenUnlinked;
        }

        var hash = Hash(token);
        var room = await Locked(rooms => rooms.Values.FirstOrDefault(r => r.TableHashes.Any(known => Same(known, hash))));
        return room switch
        {
            null => Errors.ScreenUnlinked,
            { IsOpen: false } => Errors.Closed(room.Code),
            _ => room,
        };
    }

    private ScreensView ScreensOf(Room room) => new(
        room.GmScreenHash is not null,
        room.TableHashes.Count,
        room.Pairings.Where(p => p.ExpiresAt > time.GetUtcNow()).Select(p => p.Role).ToList());

    private async Task TellRevoked(IEnumerable<string?> hashes)
    {
        foreach (var hash in hashes.OfType<string>())
        {
            await (ScreenRevoked?.Invoke(hash) ?? Task.CompletedTask);
        }
    }

    private static bool Same(string known, string given) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(known), Encoding.ASCII.GetBytes(given));
}
