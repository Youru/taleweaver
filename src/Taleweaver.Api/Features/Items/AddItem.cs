using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Items;

/// <summary>
/// The GM hands out a card: bought at the shop (<c>Price</c> Gold taken from the party), found on a Loot card,
/// or the card drawn for an item still to be drawn (<c>Replaces</c>: Item Loot, "1 common consumable").
/// <c>Owner</c>: an adventurer's name ("equipped" or "pack" <c>Slot</c>), "stash" or "unassigned".
/// </summary>
public static class AddItem
{
    public sealed record Request(string Item, string Owner, string? Slot = null, int? Price = null, string? Replaces = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Item).NotEmpty();
            RuleFor(r => r.Owner).NotEmpty();
            RuleFor(r => r.Slot).Must(ItemRequests.IsSlot).WithMessage("Slot must be \"equipped\" or \"pack\".");
            RuleFor(r => r.Price).InclusiveBetween(0, 100_000);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.AddItem(request.Item, request.Owner, request.Slot, request.Price, request.Replaces), cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("AddItem");
}

internal static class ItemRequests
{
    public static bool IsSlot(string? slot) => slot is null or "equipped" or "pack";
}
