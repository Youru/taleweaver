using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Items;

/// <summary>A card changes hands: to another adventurer, into a Pack, back to the Party Stash, or out of the items to hand out.</summary>
public static class MoveItem
{
    public sealed record Request(string Owner, string? Slot = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Owner).NotEmpty();
            RuleFor(r => r.Slot).Must(ItemRequests.IsSlot).WithMessage("Slot must be \"equipped\" or \"pack\".");
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{item}/owner", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string item, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.MoveItem(item, request.Owner, request.Slot), cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("MoveItem");
}
