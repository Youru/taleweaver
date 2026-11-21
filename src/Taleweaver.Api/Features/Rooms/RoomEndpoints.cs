using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// The room (room-spec §10.2, increment R1). GM routes under /api/games/{id}/room (this machine only, see
/// <see cref="Network.UseRoomGuard"/>); phone routes under /api/rooms/{code}, the seat token in
/// "Authorization: Seat &lt;token&gt;".
/// </summary>
public static class RoomEndpoints
{
    public sealed record JoinRequest(string DisplayName, string? Color);

    public sealed record JoinResponse(string SeatId, string SeatToken, PlayerView View);

    /// <summary>The second screen's token, given once: it goes in the QR code the GM shows the tablet.</summary>
    public sealed record ScreenResponse(string Token);

    public sealed record AssignRequest(IReadOnlyList<string> Adventurers);

    public sealed record RefuseRequest(string? Reason);

    public sealed record SettingsRequest(string Autonomy);

    public sealed record SeatUpdate(string? DisplayName, string? Color, string? Followed);

    public static IServiceCollection AddRoomsFeature(this IServiceCollection services)
    {
        services.AddSignalR().AddJsonProtocol(o => JsonDefaults.Configure(o.PayloadSerializerOptions));
        return services.AddSingleton<RoomStore>()
            .AddSingleton<RoomService>()
            .AddSingleton<RoomNotifier>()
            .AddSingleton<IGameObserver>(sp => sp.GetRequiredService<RoomNotifier>());
    }

    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHub<RoomHub>("/hubs/room");
        MapGm(app.MapGroup("/api/games/{id}/room").WithTags("Rooms"));
        MapPhone(app.MapGroup("/api/rooms/{code}").WithTags("Rooms"));
        return app;
    }

    private static void MapGm(RouteGroupBuilder group)
    {
        group.MapPost("", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, GameRunner runner, RoomService rooms, CancellationToken ct) =>
            {
                var game = await runner.LoadAsync(id, ct);
                return game.IsSuccess ? TypedResults.Ok(rooms.ViewOf(await rooms.OpenAsync(id, ct))) : game.Error!.ToProblem();
            })
            .WithName("OpenRoom");
        group.MapDelete("", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, RoomService rooms, CancellationToken ct) =>
                ToResponse(await rooms.CloseAsync(id, ct), rooms))
            .WithName("CloseRoom");
        group.MapGet("", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, RoomService rooms) =>
                await rooms.GetAsync(id) is { } room ? TypedResults.Ok(rooms.ViewOf(room)) : RoomService.Errors.NoRoom(id).ToProblem())
            .WithName("GetRoom");
        group.MapPut("/seats/{seat}/adventurers", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (
                string id, string seat, AssignRequest request, GameRunner runner, RoomService rooms, CancellationToken ct) =>
            {
                var game = await runner.LoadAsync(id, ct);
                if (!game.IsSuccess)
                {
                    return game.Error!.ToProblem();
                }

                var party = game.Value.State.Campaign.Party.Select(a => a.Name).ToList();
                return ToResponse(await rooms.AssignAsync(id, seat, request.Adventurers, party, ct), rooms);
            })
            .WithName("AssignAdventurers");
        group.MapDelete("/seats/{seat}", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, string seat, RoomService rooms, CancellationToken ct) =>
                ToResponse(await rooms.RemoveSeatAsync(id, seat, ct), rooms))
            .WithName("RemoveSeat");
        group.MapPut("/settings", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, SettingsRequest request, RoomService rooms, CancellationToken ct) =>
                ToResponse(await rooms.SetAutonomyAsync(id, request.Autonomy, ct), rooms))
            .WithName("SetRoomSettings");
        // A player's request (room-spec §6.2): accepted, it goes the GM's own way (GameRunner), into the history,
        // undone like any command; if it no longer applies, the player gets the reason.
        group.MapPost("/requests/{request}/accept", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (
                string id, string request, RoomService rooms, GameRunner runner, CancellationToken ct) =>
            {
                var taken = await rooms.ResolveAsync(id, request, RequestStatus.Accepted, null, null, ct);
                if (!taken.IsSuccess)
                {
                    return taken.Error!.ToProblem();
                }

                var (room, accepted) = taken.Value;
                var applied = await runner.ExecuteAsync(id, accepted.Command.ToGameCommand(accepted.By), ct);
                if (!applied.IsSuccess)
                {
                    await rooms.FailAsync(room, accepted, applied.Error!.Code, ct);
                }

                return TypedResults.Ok(rooms.ViewOf(room));
            })
            .WithName("AcceptRequest");
        group.MapPost("/requests/{request}/refuse", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (
                string id, string request, RefuseRequest body, RoomService rooms, CancellationToken ct) =>
            {
                var refused = await rooms.ResolveAsync(id, request, RequestStatus.Refused, body.Reason?.Trim() is { Length: > 0 } reason ? reason[..Math.Min(reason.Length, 120)] : null, null, ct);
                return refused.IsSuccess ? TypedResults.Ok(rooms.ViewOf(refused.Value.Room)) : refused.Error!.ToProblem();
            })
            .WithName("RefuseRequest");
        group.MapPost("/screen", async Task<Results<Ok<ScreenResponse>, ProblemHttpResult>> (string id, RoomService rooms, CancellationToken ct) =>
            {
                var token = await rooms.CreateScreenAsync(id, ct);
                return token.IsSuccess ? TypedResults.Ok(new ScreenResponse(token.Value)) : token.Error!.ToProblem();
            })
            .WithName("CreateScreen");
        group.MapDelete("/screen", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, RoomService rooms, CancellationToken ct) =>
                ToResponse(await rooms.RevokeScreenAsync(id, ct), rooms))
            .WithName("RevokeScreen");

        // The GM plays an adventurer as a player, in a tab of their own (room-spec §7): no seat, no room needed.
        group.MapGet("/impersonate/{adventurer}", async Task<Results<Ok<PlayerView>, ProblemHttpResult>> (
                string id, string adventurer, GameRunner runner, RoomService rooms, RoomNotifier notifier, CancellationToken ct) =>
            {
                var game = await runner.LoadAsync(id, ct);
                if (!game.IsSuccess)
                {
                    return game.Error!.ToProblem();
                }

                if (game.Value.State.Campaign.Party.All(a => a.Name != adventurer))
                {
                    return RoomService.Errors.NotInParty(adventurer).ToProblem();
                }

                return TypedResults.Ok(await notifier.ViewAsync(game.Value, await rooms.GetAsync(id), null, [adventurer], ct));
            })
            .WithName("ImpersonateAdventurer");
    }

    private static void MapPhone(RouteGroupBuilder group)
    {
        group.MapGet("", async Task<Results<Ok<RoomSummary>, ProblemHttpResult>> (string code, RoomService rooms, IGameStore games, IContentStore content, CancellationToken ct) =>
            {
                var room = await rooms.FindAsync(code);
                if (!room.IsSuccess)
                {
                    return room.Error!.ToProblem();
                }

                var game = await games.GetAsync(room.Value.GameId, ct);
                var scenario = game is null ? null : await content.GetScenarioAsync(game.Scenario, ct);
                return TypedResults.Ok(new RoomSummary(room.Value.Code, game?.Scenario ?? "", scenario?.Title, room.Value.Seats.Select(s => s.DisplayName).ToList()));
            })
            .WithName("FindRoom");
        group.MapPost("/seats", async Task<Results<Ok<JoinResponse>, ProblemHttpResult>> (
                string code, JoinRequest request, RoomService rooms, GameRunner runner, RoomNotifier notifier, CancellationToken ct) =>
            {
                var joined = await rooms.JoinAsync(code, request.DisplayName, request.Color, ct);
                if (!joined.IsSuccess)
                {
                    return joined.Error!.ToProblem();
                }

                var (room, seat, token) = joined.Value;
                var view = await ViewOf(room, seat, runner, notifier, ct);
                return view.IsSuccess ? TypedResults.Ok(new JoinResponse(seat.Id, token, view.Value)) : view.Error!.ToProblem();
            })
            .WithName("JoinRoom");
        group.MapGet("/me", async Task<Results<Ok<PlayerView>, ProblemHttpResult>> (
                string code, HttpContext http, RoomService rooms, GameRunner runner, RoomNotifier notifier, CancellationToken ct) =>
            {
                var seat = await rooms.AuthenticateAsync(code, SeatToken(http));
                if (!seat.IsSuccess)
                {
                    return seat.Error!.ToProblem();
                }

                var view = await ViewOf(seat.Value.Room, seat.Value.Seat, runner, notifier, ct);
                return view.IsSuccess ? TypedResults.Ok(view.Value) : view.Error!.ToProblem();
            })
            .WithName("GetMyView");
        group.MapPut("/me", async Task<Results<Ok<PlayerView>, ProblemHttpResult>> (
                string code, SeatUpdate request, HttpContext http, RoomService rooms, GameRunner runner, RoomNotifier notifier, CancellationToken ct) =>
            {
                var seat = await rooms.AuthenticateAsync(code, SeatToken(http));
                if (!seat.IsSuccess)
                {
                    return seat.Error!.ToProblem();
                }

                var updated = await rooms.UpdateSeatAsync(seat.Value.Room, seat.Value.Seat, request.DisplayName, request.Color, request.Followed, ct);
                if (!updated.IsSuccess)
                {
                    return updated.Error!.ToProblem();
                }

                var view = await ViewOf(seat.Value.Room, updated.Value, runner, notifier, ct);
                return view.IsSuccess ? TypedResults.Ok(view.Value) : view.Error!.ToProblem();
            })
            .WithName("UpdateMySeat");
        // A request: checked now (the player knows at once if it cannot be), then waiting for the GM, or applied
        // at once when the room's autonomy allows it (room-spec §6.3).
        group.MapPost("/me/requests", async Task<Results<Ok<PlayerView>, ProblemHttpResult>> (
                string code, PlayerCommand command, HttpContext http, RoomService rooms, GameRunner runner, RoomNotifier notifier, CancellationToken ct) =>
            {
                var seat = await rooms.AuthenticateAsync(code, SeatToken(http));
                if (!seat.IsSuccess)
                {
                    return seat.Error!.ToProblem();
                }

                var (room, me) = seat.Value;
                var game = await runner.LoadAsync(room.GameId, ct);
                if (!game.IsSuccess)
                {
                    return game.Error!.ToProblem();
                }

                if (PlayerCommands.Check(command, me.Adventurers, game.Value.State.Campaign) is { } refused)
                {
                    return refused.ToProblem();
                }

                // A purchase: only a card of the Store open now, at its price (never the phone's)
                if (command.Type == PlayerCommand.BuyItem)
                {
                    var shop = (await notifier.ViewAsync(game.Value, room, me, me.Adventurers, ct)).Shop;
                    if (shop?.FirstOrDefault(i => i.Item == command.Item) is not { } sold)
                    {
                        return PlayerCommands.Errors.NotInStore(command.Item ?? "").ToProblem();
                    }

                    command = command with { Price = sold.Price };
                }

                var engine = command.ToGameCommand(me.DisplayName);
                if (PlayerCommands.IsFree(command, room.Autonomy, me.Adventurers, game.Value.State))
                {
                    var applied = await runner.ExecuteAsync(room.GameId, engine, ct);
                    if (!applied.IsSuccess)
                    {
                        return applied.Error!.ToProblem();
                    }

                    await rooms.AddRequestAsync(room, me, command, RequestStatus.Applied, ct);
                }
                else
                {
                    var check = await runner.CheckAsync(room.GameId, engine, ct);
                    if (!check.IsSuccess)
                    {
                        return check.Error!.ToProblem();
                    }

                    await rooms.AddRequestAsync(room, me, command, RequestStatus.Pending, ct);
                }

                var view = await ViewOf(room, me, runner, notifier, ct);
                return view.IsSuccess ? TypedResults.Ok(view.Value) : view.Error!.ToProblem();
            })
            .WithName("MakeRequest");
        group.MapDelete("/me/requests/{request}", async Task<Results<Ok<PlayerView>, ProblemHttpResult>> (
                string code, string request, HttpContext http, RoomService rooms, GameRunner runner, RoomNotifier notifier, CancellationToken ct) =>
            {
                var seat = await rooms.AuthenticateAsync(code, SeatToken(http));
                if (!seat.IsSuccess)
                {
                    return seat.Error!.ToProblem();
                }

                var cancelled = await rooms.ResolveAsync(seat.Value.Room.GameId, request, RequestStatus.Cancelled, null, seat.Value.Seat.Id, ct);
                if (!cancelled.IsSuccess)
                {
                    return cancelled.Error!.ToProblem();
                }

                var view = await ViewOf(seat.Value.Room, seat.Value.Seat, runner, notifier, ct);
                return view.IsSuccess ? TypedResults.Ok(view.Value) : view.Error!.ToProblem();
            })
            .WithName("CancelRequest");
    }

    private static string? SeatToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Seat ", StringComparison.OrdinalIgnoreCase) ? header[5..].Trim() : null;
    }

    private static async Task<Result<PlayerView>> ViewOf(Room room, Seat seat, GameRunner runner, RoomNotifier notifier, CancellationToken ct)
    {
        var game = await runner.LoadAsync(room.GameId, ct);
        return game.IsSuccess ? await notifier.ViewAsync(game.Value, room, seat, seat.Adventurers, ct) : game.Error!;
    }

    private static Results<Ok<RoomView>, ProblemHttpResult> ToResponse(Result<Room> result, RoomService rooms) =>
        result.IsSuccess ? TypedResults.Ok(rooms.ViewOf(result.Value)) : result.Error!.ToProblem();
}
