using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

/// <summary>Answers the pending question: an option id ("yes", "continue", "0"…) a number, or a round of Abraxis.</summary>
public static class AnswerQuestion
{
    public sealed record Request(string? Option, int? Value, IReadOnlyList<int>? Misses = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r).Must(r => r.Misses is not null ? r.Option is null : (r.Option is not null) ^ (r.Value is not null)).WithName("answer")
                .WithMessage("Give either an option or a value.");
            RuleFor(r => r.Value).InclusiveBetween(0, 999);
            RuleForEach(r => r.Misses).InclusiveBetween(0, 99);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id}/answer", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, Request request, GameRunner runner, CancellationToken cancellationToken) =>
            {
                var command = request.Misses is { } misses ? GameCommand.AnswerAbraxis(misses, request.Value ?? 0)
                    : request.Option is { } option ? GameCommand.Answer(option)
                    : GameCommand.AnswerNumber(request.Value!.Value);
                return (await runner.ExecuteAsync(id, command, cancellationToken)).ToResponse();
            })
            .WithValidation<Request>()
            .WithName("AnswerQuestion");
}
