using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Features.Games;

/// <summary>After an Ending (rulebook p. 13): a new game, from the beginning or from the last encounter.</summary>
public static class RestartGame
{
    public const string FromBeginning = "beginning";
    public const string FromLastEncounter = "lastEncounter";

    public sealed record Request(string From);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.From).Must(f => f is FromBeginning or FromLastEncounter)
            .WithMessage($"Restart from \"{FromBeginning}\" or \"{FromLastEncounter}\".");
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id}/restart", async Task<Results<Created<GameView>, ProblemHttpResult>> (
                string id, Request request, GameRunner runner, CancellationToken cancellationToken) =>
            {
                var result = await runner.RestartAsync(id, request.From == FromLastEncounter, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/games/{result.Value.Id}", GameView.Of(result.Value))
                    : result.Error!.ToProblem();
            })
            .WithValidation<Request>()
            .WithName("RestartGame");
}
