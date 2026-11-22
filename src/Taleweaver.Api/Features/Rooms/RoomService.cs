using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// The rooms, in memory and on disk (one file per game), behind one lock: opening, closing, joining, giving the
/// adventurers to the seats. Which seats are connected comes from the hub, never from the file.
/// </summary>
public sealed partial class RoomService(RoomStore store, TimeProvider time)
{
    public const int MaxSeats = 8;

    /// <summary>No 0/O/1/I/L: nothing to mistake when read aloud or typed on a phone.</summary>
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>6 letters (decided on 2026-11-21): 148 million codes, out of reach of guessing even beyond the Wi-Fi.</summary>
    public const int CodeLength = 6;

    public static readonly IReadOnlyList<string> Colors = ["teal", "amber", "rose", "violet", "lime", "sky"];

    public static class Errors
    {
        public static Error NotFound(string code) => Error.NotFound("Rooms.NotFound", $"No room '{code}'.");

        public static Error NoRoom(string gameId) => Error.NotFound("Rooms.NotFound", $"Game '{gameId}' has no room.");

        public static Error Closed(string code) => Error.Gone("Rooms.Closed", $"Room '{code}' is closed.");

        public static readonly Error Full = Error.Conflict("Rooms.Full", $"The room already has {MaxSeats} seats.");

        public static readonly Error InvalidSeatToken = Error.Unauthorized("Rooms.InvalidSeatToken", "Unknown seat: join the room again.");

        public static readonly Error GmOnly = Error.Forbidden("Rooms.GmOnly", "Only the GM's device may do this.");

        public static Error SeatNotFound(string seat) => Error.NotFound("Rooms.SeatNotFound", $"No seat '{seat}'.");

        public static Error AdventurerNotAccessible(string adventurer) =>
            Error.Forbidden("Rooms.AdventurerNotAccessible", $"'{adventurer}' is not one of this seat's adventurers.");

        public static Error NotInParty(string adventurer) => Error.Validation("Rooms.NotInParty", $"'{adventurer}' is not in the party.");

        public static readonly Error InvalidName = Error.Validation("Rooms.InvalidName", "A name of 1 to 24 characters is needed.");
    }

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, int> _connections = new();
    private Dictionary<string, Room>? _rooms;

    /// <summary>Raised after every change of a room (seats, adventurers, status), outside the lock.</summary>
    public event Func<Room, Task>? Changed;

    /// <summary>The GM sent a seat away: its phone is told at once.</summary>
    public event Func<string, Task>? SeatRemoved;

    public async Task<Room?> GetAsync(string gameId) => await Locked(rooms => rooms.GetValueOrDefault(gameId));

    public async Task<IReadOnlyList<Room>> OpenRoomsAsync() => await Locked(rooms => rooms.Values.Where(r => r.IsOpen).ToList());

    /// <summary>An open room from its code (any case).</summary>
    public async Task<Result<Room>> FindAsync(string code)
    {
        var room = await Locked(rooms => rooms.Values.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase)));
        return room switch
        {
            null => Errors.NotFound(code),
            { IsOpen: false } => Errors.Closed(code),
            _ => room,
        };
    }

    /// <summary>Opens (or opens again, with a new code) the game's room. The seats and what they hold are kept.</summary>
    public async Task<Room> OpenAsync(string gameId, CancellationToken cancellationToken)
    {
        var room = await Locked(rooms =>
        {
            if (rooms.TryGetValue(gameId, out var existing) && existing.IsOpen)
            {
                return existing;
            }

            var code = NewCode(rooms.Values.Where(r => r.IsOpen).Select(r => r.Code).ToHashSet());
            var opened = existing ?? new Room { GameId = gameId, Code = code };
            opened.Code = code;
            opened.Status = RoomStatus.Open;
            rooms[gameId] = opened;
            return opened;
        });
        await SaveAndNotify(room, cancellationToken);
        return room;
    }

    public async Task<Result<Room>> CloseAsync(string gameId, CancellationToken cancellationToken)
    {
        var room = await Locked(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return null;
            }

            room.Status = RoomStatus.Closed;
            return room;
        });
        if (room is null)
        {
            return Errors.NoRoom(gameId);
        }

        await SaveAndNotify(room, cancellationToken);
        return room;
    }

    /// <summary>The game is gone: so is its room.</summary>
    public async Task ForgetAsync(string gameId)
    {
        await Locked(rooms => rooms.Remove(gameId));
        store.Delete(gameId);
    }

    /// <summary>A new seat; the token is returned once, only its hash is kept.</summary>
    public async Task<Result<(Room Room, Seat Seat, string Token)>> JoinAsync(string code, string displayName, string? color, CancellationToken cancellationToken)
    {
        var name = displayName.Trim();
        if (name.Length is 0 or > 24)
        {
            return Errors.InvalidName;
        }

        var found = await FindAsync(code);
        if (!found.IsSuccess)
        {
            return found.Error!;
        }

        var token = NewToken();
        var joined = await Locked(rooms =>
        {
            var room = found.Value;
            if (room.Seats.Count >= MaxSeats)
            {
                return null;
            }

            var seat = new Seat
            {
                Id = "s-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3)),
                DisplayName = name,
                Color = color is not null && Colors.Contains(color) ? color : Colors[room.Seats.Count % Colors.Count],
                TokenHash = Hash(token),
                LastSeen = time.GetUtcNow(),
            };
            room.Seats.Add(seat);
            return seat;
        });
        if (joined is null)
        {
            return Errors.Full;
        }

        await SaveAndNotify(found.Value, cancellationToken);
        return (found.Value, joined, token);
    }

    /// <summary>The seat of a token, in an open room.</summary>
    public async Task<Result<(Room Room, Seat Seat)>> AuthenticateAsync(string code, string? token)
    {
        var found = await FindAsync(code);
        if (!found.IsSuccess)
        {
            return found.Error!;
        }

        if (string.IsNullOrEmpty(token))
        {
            return Errors.InvalidSeatToken;
        }

        var hash = Hash(token);
        var seat = await Locked(_ =>
        {
            var seat = found.Value.Seats.FirstOrDefault(s => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(s.TokenHash), Encoding.ASCII.GetBytes(hash)));
            if (seat is not null)
            {
                seat.LastSeen = time.GetUtcNow();
            }

            return seat;
        });
        return seat is null ? Errors.InvalidSeatToken : (found.Value, seat);
    }

    /// <summary>
    /// Gives these adventurers to the seat (and only these): each one is taken from the seat that held it.
    /// <paramref name="party"/>: the party's members, the only names allowed.
    /// </summary>
    public async Task<Result<Room>> AssignAsync(string gameId, string seatId, IReadOnlyList<string> adventurers, IReadOnlyCollection<string> party, CancellationToken cancellationToken)
    {
        if (adventurers.FirstOrDefault(a => !party.Contains(a)) is { } stranger)
        {
            return Errors.NotInParty(stranger);
        }

        var outcome = await Locked<Result<Room>>(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return Errors.NoRoom(gameId);
            }

            if (room.Seats.FirstOrDefault(s => s.Id == seatId) is not { } seat)
            {
                return Errors.SeatNotFound(seatId);
            }

            foreach (var other in room.Seats.Where(s => s != seat))
            {
                other.Adventurers.RemoveAll(adventurers.Contains);
                FixFollowed(other);
            }

            seat.Adventurers = adventurers.Distinct().ToList();
            FixFollowed(seat);
            return room;
        });
        if (outcome.IsSuccess)
        {
            await SaveAndNotify(outcome.Value, cancellationToken);
        }

        return outcome;
    }

    public async Task<Result<Room>> RemoveSeatAsync(string gameId, string seatId, CancellationToken cancellationToken)
    {
        var outcome = await Locked<Result<Room>>(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return Errors.NoRoom(gameId);
            }

            if (room.Seats.RemoveAll(s => s.Id == seatId) == 0)
            {
                return Errors.SeatNotFound(seatId);
            }

            foreach (var asked in room.Requests.Where(r => r.SeatId == seatId && r.Status == RequestStatus.Pending))
            {
                asked.Status = RequestStatus.Cancelled;
            }

            return room;
        });
        if (outcome.IsSuccess)
        {
            await SaveAndNotify(outcome.Value, cancellationToken);
            await (SeatRemoved?.Invoke(seatId) ?? Task.CompletedTask);
        }

        return outcome;
    }

    /// <summary>The phone changes its name, colour or the adventurer it shows.</summary>
    public async Task<Result<Seat>> UpdateSeatAsync(Room room, Seat seat, string? displayName, string? color, string? followed, CancellationToken cancellationToken)
    {
        if (displayName is not null && displayName.Trim().Length is 0 or > 24)
        {
            return Errors.InvalidName;
        }

        if (followed is not null && !seat.Adventurers.Contains(followed))
        {
            return Errors.AdventurerNotAccessible(followed);
        }

        await Locked(_ =>
        {
            seat.DisplayName = displayName?.Trim() ?? seat.DisplayName;
            seat.Color = color is not null && Colors.Contains(color) ? color : seat.Color;
            seat.Followed = followed ?? seat.Followed;
            return seat;
        });
        await SaveAndNotify(room, cancellationToken);
        return seat;
    }

    /// <summary>Members gone from the party are taken from the seats (an adventurer leaves the story).</summary>
    public async Task<bool> DropMissingAsync(string gameId, IReadOnlyCollection<string> party, CancellationToken cancellationToken)
    {
        var room = await Locked(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return null;
            }

            var changed = false;
            foreach (var seat in room.Seats)
            {
                changed |= seat.Adventurers.RemoveAll(a => !party.Contains(a)) > 0;
                FixFollowed(seat);
            }

            return changed ? room : null;
        });
        if (room is null)
        {
            return false;
        }

        await SaveAndNotify(room, cancellationToken);
        return true;
    }

    public void Connected(string seatId) => _connections.AddOrUpdate(seatId, 1, (_, n) => n + 1);

    public void Disconnected(string seatId) => _connections.AddOrUpdate(seatId, 0, (_, n) => Math.Max(0, n - 1));

    public bool IsConnected(string seatId) => _connections.GetValueOrDefault(seatId) > 0;

    public RoomView ViewOf(Room room) => new(
        room.GameId,
        room.Code,
        room.Status,
        room.Seats.Select(s => new SeatView(s.Id, s.DisplayName, s.Color, s.Adventurers, s.Followed, s.LastSeen, IsConnected(s.Id))).ToList(),
        Network.LanAddresses(),
        room.ScreenTokenHash is not null,
        room.Autonomy,
        // Every pending request, then the last answered ones, newest first
        room.Requests.Where(r => r.Status == RequestStatus.Pending)
            .Concat(room.Requests.Where(r => r.Status != RequestStatus.Pending).TakeLast(15).Reverse())
            .ToList());

    /// <summary>
    /// A new token for the GM's second screen (room-spec §7): it replaces the previous one, if any. Returned once,
    /// only its hash is kept.
    /// </summary>
    public async Task<Result<string>> CreateScreenAsync(string gameId, CancellationToken cancellationToken)
    {
        var token = NewToken();
        var room = await Locked(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { IsOpen: true } room)
            {
                return null;
            }

            room.ScreenTokenHash = Hash(token);
            return room;
        });
        if (room is null)
        {
            return Errors.NoRoom(gameId);
        }

        await SaveAndNotify(room, cancellationToken);
        return token;
    }

    /// <summary>The second screen stops working at once.</summary>
    public async Task<Result<Room>> RevokeScreenAsync(string gameId, CancellationToken cancellationToken)
    {
        var room = await Locked(rooms =>
        {
            if (rooms.GetValueOrDefault(gameId) is not { } room)
            {
                return null;
            }

            room.ScreenTokenHash = null;
            return room;
        });
        if (room is null)
        {
            return Errors.NoRoom(gameId);
        }

        await SaveAndNotify(room, cancellationToken);
        return room;
    }

    /// <summary>The game whose open room has this second-screen token, or null.</summary>
    public async Task<string?> ScreenGameAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var hash = Encoding.ASCII.GetBytes(Hash(token));
        return await Locked(rooms => rooms.Values.FirstOrDefault(r =>
            r.IsOpen && r.ScreenTokenHash is { } known && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(known), hash))?.GameId);
    }

    /// <summary>Lets the hub tell the GM a phone came or went.</summary>
    public Task NotifyAsync(Room room) => Changed?.Invoke(room) ?? Task.CompletedTask;

    private static void FixFollowed(Seat seat)
    {
        if (seat.Followed is null || !seat.Adventurers.Contains(seat.Followed))
        {
            seat.Followed = seat.Adventurers.FirstOrDefault();
        }
    }

    private async Task SaveAndNotify(Room room, CancellationToken cancellationToken)
    {
        await store.SaveAsync(room, cancellationToken);
        await NotifyAsync(room);
    }

    private static string NewCode(HashSet<string> taken)
    {
        while (true)
        {
            var code = string.Concat(Enumerable.Range(0, CodeLength).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
            if (!taken.Contains(code))
            {
                return code;
            }
        }
    }

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<T> Locked<T>(Func<Dictionary<string, Room>, T> action)
    {
        await _lock.WaitAsync();
        try
        {
            _rooms ??= store.LoadAll().ToDictionary(r => r.GameId);
            return action(_rooms);
        }
        finally
        {
            _lock.Release();
        }
    }
}
