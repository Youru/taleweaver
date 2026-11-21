using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Items;

/// <summary>
/// The party's items during a game (rulebook ch. 6): hand out a card (bought, looted, drawn), move it between
/// adventurers, Packs and the Party Stash, flip it, use it, discard or sell it. Each command returns the new game state.
/// </summary>
public static class ItemsEndpoints
{
    public sealed record UpgradeRequest(string Upgrade, int? Price = null);

    public static IEndpointRouteBuilder MapItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/items").WithTags("Items");
        AddItem.Map(group);
        MoveItem.Map(group);
        DiscardItem.Map(group);
        FlipItem.Map(group);
        UseItem.Map(group);
        group.MapPost("/upgrades/unlocked", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, UpgradeRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.UnlockUpgrade(request.Upgrade), cancellationToken)).ToResponse())
            .WithName("UnlockUpgrade");
        group.MapPost("/{item}/upgrades", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string item, UpgradeRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.AttachUpgrade(item, request.Upgrade, request.Price), cancellationToken)).ToResponse())
            .WithName("AttachUpgrade");
        group.MapDelete("/{item}/upgrades/{upgrade}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string item, string upgrade, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.DetachUpgrade(item, upgrade), cancellationToken)).ToResponse())
            .WithName("DetachUpgrade");
        return app;
    }
}
