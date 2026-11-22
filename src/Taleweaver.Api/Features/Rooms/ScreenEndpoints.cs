using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// The screens (room-spec §7.6). The GM's computer makes and revokes QR codes under /api/games/{id}/room/screens;
/// a device exchanges one at /api/screens/claim; a table reads /api/screens/table with "Authorization: Table &lt;token&gt;".
/// </summary>
public static class ScreenEndpoints
{
    public const string Path = "/api/screens";

    /// <summary>The QR code's token, given once.</summary>
    public sealed record PairingResponse(string Token);

    public sealed record ClaimRequest(string? Token);

    public sealed record ClaimResponse(string GameId, string Role, string Token);

    public static void Map(IEndpointRouteBuilder app)
    {
        var host = app.MapGroup("/api/games/{id}/room/screens").WithTags("Rooms");
        host.MapPost("/{role}", async Task<Results<Ok<PairingResponse>, ProblemHttpResult>> (string id, string role, RoomService rooms, CancellationToken ct) =>
            {
                var token = await rooms.CreatePairingAsync(id, role, ct);
                return token.IsSuccess ? TypedResults.Ok(new PairingResponse(token.Value)) : token.Error!.ToProblem();
            })
            .WithName("CreateScreenPairing");
        host.MapDelete("/{role}", async Task<Results<Ok<RoomView>, ProblemHttpResult>> (string id, string role, RoomService rooms, CancellationToken ct) =>
            {
                var room = await rooms.RevokeScreensAsync(id, role, ct);
                return room.IsSuccess ? TypedResults.Ok(rooms.ViewOf(room.Value)) : room.Error!.ToProblem();
            })
            .WithName("RevokeScreens");

        var device = app.MapGroup(Path).WithTags("Rooms");
        device.MapPost("/claim", async Task<Results<Ok<ClaimResponse>, ProblemHttpResult>> (ClaimRequest request, RoomService rooms, CancellationToken ct) =>
            {
                var claimed = await rooms.ClaimScreenAsync(request.Token, ct);
                return claimed.IsSuccess
                    ? TypedResults.Ok(new ClaimResponse(claimed.Value.Room.GameId, claimed.Value.Role, claimed.Value.Token))
                    : claimed.Error!.ToProblem();
            })
            .WithName("ClaimScreen");
        device.MapGet("/table", async Task<Results<Ok<TableScreenView>, ProblemHttpResult>> (
                HttpContext http, RoomService rooms, GameRunner runner, RoomNotifier notifier, CancellationToken ct) =>
            {
                var room = await rooms.TableRoomAsync(TableToken(http));
                if (!room.IsSuccess)
                {
                    return room.Error!.ToProblem();
                }

                var game = await runner.LoadAsync(room.Value.GameId, ct);
                return game.IsSuccess ? TypedResults.Ok(await notifier.TableViewAsync(game.Value, room.Value, ct)) : game.Error!.ToProblem();
            })
            .WithName("GetTableScreen");
    }

    private static string? TableToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Table ", StringComparison.Ordinal) ? header["Table ".Length..].Trim() : null;
    }
}
