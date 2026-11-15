using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

/// <summary>Answers the pending question: an option id ("yes", "continue", "0"…) or a number.</summary>
public static class AnswerQuestion
{
    public sealed record Request(string? Option, int? Value);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r).Must(r => (r.Option is not null) ^ (r.Value is not null)).WithName("answer")
                .WithMessage("Give either an option or a value.");
            RuleFor(r => r.Value).InclusiveBetween(0, 999);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id}/answer", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, Request request, GameRunner runner, CancellationToken cancellationToken) =>
            {
                var command = request.Option is not null ? GameCommand.Answer(request.Option) : GameCommand.AnswerNumber(request.Value!.Value);
                return (await runner.ExecuteAsync(id, command, cancellationToken)).ToResponse();
            })
            .WithValidation<Request>()
            .WithName("AnswerQuestion");
}
