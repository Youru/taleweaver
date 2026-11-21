namespace Taleweaver.Api.Features.Items;

/// <summary>
/// The party's items during a game (rulebook ch. 6): hand out a card (bought, looted, drawn), move it between
/// adventurers, Packs and the Party Stash, flip it, use it, discard or sell it. Each command returns the new game state.
/// </summary>
public static class ItemsEndpoints
{
    public static IEndpointRouteBuilder MapItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/items").WithTags("Items");
        AddItem.Map(group);
        MoveItem.Map(group);
        DiscardItem.Map(group);
        FlipItem.Map(group);
        UseItem.Map(group);
        return app;
    }
}
