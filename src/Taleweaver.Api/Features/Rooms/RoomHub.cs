using Microsoft.AspNetCore.SignalR;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// /hubs/room: the server pushes, the clients only listen (every action goes through REST). A phone connects
/// with ?code=KRVA&amp;access_token=&lt;seat token&gt;, the GM's device with ?game=&lt;id&gt; (its tablet adds its
/// screen_token), a table screen with ?table_token=&lt;token&gt;. A screen's group carries its token's hash: revoked,
/// it hears nothing more even if it stays connected.
/// Server → client: PlayerViewChanged(view), RoomClosed(), SeatRemoved(), Shown(picture); to the GM: RoomChanged(room), GameChanged(version); to a table screen: TableChanged(),
/// with nothing in it; to both screens: ScreenRevoked().
/// </summary>
public sealed class RoomHub(RoomService rooms, CodeGuessLimiter guesses) : Hub
{
    private const string SeatKey = "seat";
    private const string RoomKey = "room";

    public static string GmGroup(string gameId) => $"gm:{gameId}";

    public static string ScreenGroup(string tokenHash) => $"screen:{tokenHash}";

    public static string SeatGroup(string seatId) => $"seat:{seatId}";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var query = http.Request.Query;
        if (query["game"].ToString() is { Length: > 0 } gameId)
        {
            var group = GmGroup(gameId);
            if (!Network.IsLocal(http))
            {
                var token = Network.ScreenToken(http);
                if (await rooms.ScreenGameAsync(token) != gameId)
                {
                    throw new HubException(RoomService.Errors.GmOnly.Code);
                }

                group = ScreenGroup(RoomService.Hash(token!));
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            return;
        }

        if (query["table_token"].ToString() is { Length: > 0 } tableToken)
        {
            if (!(await rooms.TableRoomAsync(tableToken)).IsSuccess)
            {
                throw new HubException(RoomService.Errors.ScreenUnlinked.Code);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ScreenGroup(RoomService.Hash(tableToken)));
            return;
        }

        var seat = await rooms.AuthenticateAsync(query["code"].ToString(), query["access_token"].ToString());
        if (!seat.IsSuccess)
        {
            if (seat.Error!.Code == "Rooms.NotFound" && !Network.IsLocal(http))
            {
                guesses.Failed(Network.ClientAddress(http));
            }

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
