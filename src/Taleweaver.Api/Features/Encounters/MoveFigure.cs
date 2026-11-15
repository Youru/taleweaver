using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>
/// The GM mirrors a move on the table: a combatant ("fragor-2") or an adventurer ("adventurer:Rook").
/// Optional, and allowed even while a question is pending: the rules do not read the squares.
/// </summary>
public static class MoveFigure
{
    public sealed record Request(int X, int Y);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.X).InclusiveBetween(0, 200);
            RuleFor(r => r.Y).InclusiveBetween(0, 200);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/figures/{figure}/position", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string figure, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Move(figure, new Cell(request.X, request.Y)), cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("MoveFigure");
}
