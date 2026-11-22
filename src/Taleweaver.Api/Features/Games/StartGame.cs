using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

/// <summary>
/// Starts a scenario. Without a party, it is prepared first within the scenario's rules (roadmap §5.2); with names,
/// it sets out at once and the first node is read.
/// </summary>
public static class StartGame
{
    public sealed record Request(string Scenario, IReadOnlyList<string> Party, GameOptions? Options = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Scenario).NotEmpty();
            RuleFor(r => r.Party).Cascade(CascadeMode.Stop).NotNull()
                .Must(p => p.Count <= 6).WithMessage("A party has at most 6 adventurers.")
                .Must(p => p.Distinct(StringComparer.OrdinalIgnoreCase).Count() == p.Count).WithMessage("Adventurer names must be unique.");
            RuleFor(r => r.Options).Must(o => GameOptions.IsKnownUrgencyLimit(o?.UrgencyLimit)).WithMessage("The Urgency limit is 3, 4 or 5.");
            RuleForEach(r => r.Party).NotEmpty().MaximumLength(40);
        }
    }

    public sealed class Handler(GameRunner runner)
    {
        public Task<Result<Game>> Handle(Request request, CancellationToken cancellationToken) =>
            runner.StartAsync(request.Scenario, request.Party, request.Options, cancellationToken);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async Task<Results<Created<GameView>, ProblemHttpResult>> (Request request, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/games/{result.Value.Id}", GameView.Of(result.Value))
                    : result.Error!.ToProblem();
            })
            .WithValidation<Request>()
            .WithName("StartGame");
}
