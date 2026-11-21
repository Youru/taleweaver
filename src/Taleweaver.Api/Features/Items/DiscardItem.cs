using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Items;

/// <summary>The card leaves the party: discarded, or sold for <c>Gold</c> (added to the party's).</summary>
public static class DiscardItem
{
    public sealed record Request(int? Gold = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.Gold).InclusiveBetween(0, 100_000);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{item}/discard", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string item, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.DiscardItem(item, request.Gold), cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("DiscardItem");
}
