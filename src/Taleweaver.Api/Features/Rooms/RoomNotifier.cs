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

    public RoomNotifier(IHubContext<RoomHub> hub, RoomService rooms, IServiceScopeFactory scopes, IContentStore content, CatalogProvider catalog)
    {
        _hub = hub;
        _rooms = rooms;
        _scopes = scopes;
        _content = content;
        _catalog = catalog;
        rooms.Changed += RoomChangedAsync;
    }

    public async Task GameChangedAsync(Game game, CancellationToken cancellationToken)
    {
        // The GM's other tabs (an adventurer played as a player) reload what they show.
        await _hub.Clients.Group(RoomHub.GmGroup(game.Id)).SendAsync("GameChanged", game.UpdatedAt.ToUnixTimeMilliseconds(), cancellationToken);
        if (await _rooms.GetAsync(game.Id) is not { IsOpen: true } room)
        {
            return;
        }

        // An adventurer who left the party leaves the seats (this pushes the views again).
        if (await _rooms.DropMissingAsync(game.Id, game.State.Campaign.Party.Select(a => a.Name).ToList(), cancellationToken))
        {
            return;
        }

        await PushSeatsAsync(room, game, cancellationToken);
    }

    /// <summary>The view of a seat, from the game as it is now.</summary>
    public async Task<PlayerView> ViewAsync(Game game, Room? room, Seat? seat, IReadOnlyCollection<string> adventurers, CancellationToken cancellationToken)
    {
        var scenario = await _content.GetScenarioAsync(game.Scenario, cancellationToken);
        var catalog = await _catalog.GetAsync(cancellationToken);
        return PlayerViewProjector.Project(game.Id, game.UpdatedAt.ToUnixTimeMilliseconds(), game.State, scenario?.Title, adventurers, room, seat, catalog);
    }

    private async Task RoomChangedAsync(Room room)
    {
        await _hub.Clients.Group(RoomHub.GmGroup(room.GameId)).SendAsync("RoomChanged", _rooms.ViewOf(room));
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
