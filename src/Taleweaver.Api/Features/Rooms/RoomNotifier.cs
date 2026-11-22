using Microsoft.AspNetCore.SignalR;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// After each change of a game or of its room: the whole projected view to each seat (no diff to keep: a phone
/// that comes back gets the same thing), the room to the GM.
/// </summary>
public sealed class RoomNotifier : IGameObserver
{
    private readonly IHubContext<RoomHub> _hub;
    private readonly RoomService _rooms;
    private readonly IServiceScopeFactory _scopes;
    private readonly IContentStore _content;
    private readonly CatalogProvider _catalog;
    private readonly PhoneCards _cards;

    public RoomNotifier(IHubContext<RoomHub> hub, RoomService rooms, IServiceScopeFactory scopes, IContentStore content, CatalogProvider catalog, PhoneCards cards)
    {
        _hub = hub;
        _rooms = rooms;
        _scopes = scopes;
        _content = content;
        _catalog = catalog;
        _cards = cards;
        rooms.Changed += RoomChangedAsync;
        rooms.ScreenRevoked += hash => _hub.Clients.Group(RoomHub.ScreenGroup(hash)).SendAsync("ScreenRevoked");
        rooms.SeatRemoved += seat => _hub.Clients.Group(RoomHub.SeatGroup(seat)).SendAsync("SeatRemoved");
    }

    public async Task GameChangedAsync(Game game, CancellationToken cancellationToken)
    {
        _cards.Changed();
        // The GM's other tabs (an adventurer played as a player) reload what they show.
        await _hub.Clients.Group(RoomHub.GmGroup(game.Id)).SendAsync("GameChanged", game.UpdatedAt.ToUnixTimeMilliseconds(), cancellationToken);
        if (await _rooms.GetAsync(game.Id) is not { IsOpen: true } room)
        {
            return;
        }

        if (room.GmScreenHash is { } tablet)
        {
            await _hub.Clients.Group(RoomHub.ScreenGroup(tablet)).SendAsync("GameChanged", game.UpdatedAt.ToUnixTimeMilliseconds(), cancellationToken);
        }

        await TablesChangedAsync(room, cancellationToken);

        // An adventurer who left the party leaves the seats (this pushes the views again).
        if (await _rooms.DropMissingAsync(game.Id, game.State.Campaign.Party.Select(a => a.Name).ToList(), cancellationToken))
        {
            return;
        }

        await PushSeatsAsync(room, game, cancellationToken);
    }

    /// <summary>A picture pushed by the GM (room-spec §7.5): shown at once, not kept.</summary>
    public async Task ShowAsync(string gameId, IEnumerable<Seat> seats, string image, string? caption)
    {
        // Readable before the phone asks for it
        _cards.Shown(gameId, image);
        foreach (var seat in seats)
        {
            await _hub.Clients.Group(RoomHub.SeatGroup(seat.Id)).SendAsync("Shown", new { image, caption });
        }
    }

    /// <summary>What is shown to every player is shown at the table too.</summary>
    public async Task ShowTablesAsync(Room room, string image, string? caption)
    {
        _cards.Shown(room.GameId, image);
        foreach (var table in room.TableHashes)
        {
            await _hub.Clients.Group(RoomHub.ScreenGroup(table)).SendAsync("Shown", new { image, caption });
        }
    }

    /// <summary>What a table screen receives: the phones' view cut down to what everybody at the table sees.</summary>
    public async Task<TableScreenView> TableViewAsync(Game game, Room room, CancellationToken cancellationToken) =>
        TableScreenView.Of(await ViewAsync(game, room, null, [], cancellationToken));

    // Only a signal: each screen reads its view again with its token
    private async Task TablesChangedAsync(Room room, CancellationToken cancellationToken)
    {
        foreach (var table in room.TableHashes)
        {
            await _hub.Clients.Group(RoomHub.ScreenGroup(table)).SendAsync("TableChanged", cancellationToken);
        }
    }

    /// <summary>The view of a seat, from the game as it is now.</summary>
    public async Task<PlayerView> ViewAsync(Game game, Room? room, Seat? seat, IReadOnlyCollection<string> adventurers, CancellationToken cancellationToken)
    {
        var scenario = await _content.GetScenarioAsync(game.Scenario, cancellationToken);
        // During a Bounty, the encounter's map is in the Bounty's own scenario
        var played = game.State.Bounty is { } bounty ? await _content.GetScenarioAsync(bounty.Scenario, cancellationToken) : scenario;
        var catalog = await _catalog.GetAsync(cancellationToken);
        var view = PlayerViewProjector.Project(game.Id, game.UpdatedAt.ToUnixTimeMilliseconds(), game.State, scenario?.Title, adventurers, room, seat, catalog, scenario, played);
        return view with { Cards = await _cards.FingerprintAsync(game, cancellationToken), Paused = game.Table.Paused };
    }

    private async Task RoomChangedAsync(Room room)
    {
        _cards.Changed();
        var view = _rooms.ViewOf(room);
        await _hub.Clients.Group(RoomHub.GmGroup(room.GameId)).SendAsync("RoomChanged", view);
        if (room.GmScreenHash is { } tablet)
        {
            await _hub.Clients.Group(RoomHub.ScreenGroup(tablet)).SendAsync("RoomChanged", view);
        }

        await TablesChangedAsync(room, CancellationToken.None);
        if (!room.IsOpen)
        {
            foreach (var seat in room.Seats)
            {
                await _hub.Clients.Group(RoomHub.SeatGroup(seat.Id)).SendAsync("RoomClosed");
            }

            return;
        }

        using var scope = _scopes.CreateScope();
        var game = await scope.ServiceProvider.GetRequiredService<GameRunner>().LoadAsync(room.GameId, CancellationToken.None);
        if (game.IsSuccess)
        {
            await PushSeatsAsync(room, game.Value, CancellationToken.None);
        }
    }

    private async Task PushSeatsAsync(Room room, Game game, CancellationToken cancellationToken)
    {
        foreach (var seat in room.Seats.ToList())
        {
            var view = await ViewAsync(game, room, seat, seat.Adventurers, cancellationToken);
            await _hub.Clients.Group(RoomHub.SeatGroup(seat.Id)).SendAsync("PlayerViewChanged", view, cancellationToken);
        }
    }
}
