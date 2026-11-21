using Microsoft.AspNetCore.SignalR;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// /hubs/room: the server pushes, the clients only listen (every action goes through REST). A phone connects
/// with ?code=KRVA&amp;access_token=&lt;seat token&gt;, the GM's device with ?game=&lt;id&gt;.
/// Server → client: PlayerViewChanged(view), RoomClosed(); to the GM: RoomChanged(room), GameChanged(version).
/// </summary>
public sealed class RoomHub(RoomService rooms) : Hub
{
    private const string SeatKey = "seat";
    private const string RoomKey = "room";

    public static string GmGroup(string gameId) => $"gm:{gameId}";

    public static string SeatGroup(string seatId) => $"seat:{seatId}";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var query = http.Request.Query;
        if (query["game"].ToString() is { Length: > 0 } gameId)
        {
            if (!Network.IsLocal(http))
            {
                throw new HubException(RoomService.Errors.GmOnly.Code);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GmGroup(gameId));
            return;
        }

        var seat = await rooms.AuthenticateAsync(query["code"].ToString(), query["access_token"].ToString());
        if (!seat.IsSuccess)
        {
            throw new HubException(seat.Error!.Code);
        }

        Context.Items[SeatKey] = seat.Value.Seat.Id;
        Context.Items[RoomKey] = seat.Value.Room;
        await Groups.AddToGroupAsync(Context.ConnectionId, SeatGroup(seat.Value.Seat.Id));
        rooms.Connected(seat.Value.Seat.Id);
        await rooms.NotifyAsync(seat.Value.Room);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(SeatKey, out var seat) && seat is string seatId && Context.Items[RoomKey] is Room room)
        {
            rooms.Disconnected(seatId);
            await rooms.NotifyAsync(room);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
