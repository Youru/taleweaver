using System.Text.Json;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>rooms/&lt;gameId&gt;.json in the saves folder: the seats survive a restart of the server.</summary>
public sealed class RoomStore(IOptions<ContentOptions> options)
{
    private readonly string _folder = Path.Combine(
        string.IsNullOrWhiteSpace(options.Value.SavesPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Middara", "saves")
            : options.Value.SavesPath,
        "rooms");

    public IEnumerable<Room> LoadAll()
    {
        if (!Directory.Exists(_folder))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(_folder, "*.json"))
        {
            Room? room;
            try
            {
                room = JsonSerializer.Deserialize<Room>(File.ReadAllText(path), JsonDefaults.Options);
            }
            catch (JsonException)
            {
                continue;
            }

            if (room is not null)
            {
                yield return room;
            }
        }
    }

    public Task SaveAsync(Room room, CancellationToken cancellationToken) =>
        AtomicFile.WriteJsonAsync(PathOf(room.GameId), room, cancellationToken);

    public void Delete(string gameId)
    {
        var path = PathOf(gameId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathOf(string gameId) =>
        gameId.Length is > 0 and <= 64 && gameId.All(char.IsAsciiLetterOrDigit)
            ? Path.Combine(_folder, $"{gameId}.json")
            : throw new ArgumentException($"Invalid game id '{gameId}'.");
}
