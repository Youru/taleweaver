using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>
/// An item or an ability moves a card on the initiative track (rulebook p. 36): <c>By</c> places, negative
/// toward the front. Allowed while a question is pending: the figure playing goes on.
/// </summary>
public static class MoveInitiativeCard
{
    public sealed record Request(string Card, int By);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Card).NotEmpty();
            RuleFor(r => r.By).NotEqual(0).InclusiveBetween(-30, 30);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/initiative/moves", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.MoveInitiative(request.Card, request.By), cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("MoveInitiativeCard");
}
