using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// What the GM's tablet may reach, by white list (room-spec §7.6): playing its game. Deleting, exporting, restarting,
/// opening or closing the room, linking screens, the other scenarios and the workshop stay on the GM's computer.
/// A new route is refused until it is written here.
/// </summary>
public static class ScreenAccess
{
    /// <summary>Under /api/games/{id}/: the groups that only hold commands of the game.</summary>
    private static readonly string[] PlayGroups = ["allies", "encounter", "items", "party", "play", "preparation", "session"];

    private static readonly string[] PlayReads = ["", "hidden-cards", "room"];

    private static readonly string[] PlayPosts = ["answer", "undo", "revert", "room/show", "room/requests/accept-all"];

    /// <summary>What play reads beyond a phone's tables (<see cref="Network.UseRoomGuard"/>).</summary>
    private static readonly string[] Catalog = ["/api/catalog/dice", "/api/catalog/allies"];

    public static async Task<bool> AllowsAsync(HttpContext context)
    {
        if (await context.RequestServices.GetRequiredService<RoomService>().ScreenGameAsync(Network.ScreenToken(context)) is not { } gameId)
        {
            return false;
        }

        var method = context.Request.Method;
        var parts = (context.Request.Path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["api", "games", var game, .. var rest])
        {
            return game == gameId && AllowsInGame(method, rest);
        }

        if (!HttpMethods.IsGet(method))
        {
            return false;
        }

        return parts switch
        {
            ["api", "packs", _, "combatants", _] => true,
            ["api", "scenarios", var pack, var name] => await IsPlayedAsync(context, gameId, $"{pack}:scenario/{name}"),
            _ => Catalog.Contains(context.Request.Path.Value),
        };
    }

    public static bool AllowsInGame(string method, string[] rest)
    {
        var get = HttpMethods.IsGet(method);
        var post = HttpMethods.IsPost(method);
        var put = HttpMethods.IsPut(method);
        var delete = HttpMethods.IsDelete(method);
        if (rest is [var group, _, ..] && PlayGroups.Contains(group))
        {
            return true;
        }

        // A group's own route ("POST …/items", "POST …/allies")
        if (rest is ["items" or "allies"])
        {
            return post;
        }

        var route = string.Join('/', rest);
        if ((get && PlayReads.Contains(route)) || (post && PlayPosts.Contains(route)))
        {
            return true;
        }

        return rest switch
        {
            ["room", "settings"] => put,
            ["room", "seats", _] => delete,
            ["room", "seats", _, "adventurers" or "trust"] => put,
            ["room", "requests", _, "accept" or "refuse"] => post,
            ["room", "impersonate", _] => get,
            _ => false,
        };
    }

    private static async Task<bool> IsPlayedAsync(HttpContext context, string gameId, string scenario)
    {
        var game = await context.RequestServices.GetRequiredService<GameRunner>().LoadAsync(gameId, context.RequestAborted);
        return game.IsSuccess && (game.Value.Scenario == scenario || game.Value.State.Bounty?.Scenario == scenario);
    }
}
