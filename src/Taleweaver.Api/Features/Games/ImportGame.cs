using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

/// <summary>Loads an exported game under a new id. Its history is replayed on the current scenario.</summary>
public static class ImportGame
{
    public sealed class Validator : AbstractValidator<Game>
    {
        public Validator()
        {
            RuleFor(g => g.Scenario).NotEmpty();
            RuleFor(g => g.Party).NotEmpty();
            RuleForEach(g => g.Commands).ChildRules(c => c.RuleFor(x => x.Type).NotEmpty());
        }
    }

    public sealed class Handler(GameRunner runner)
    {
        public Task<Result<Game>> Handle(Game game, CancellationToken cancellationToken) =>
            runner.ReplayAsync(game with { Id = Guid.NewGuid().ToString("N") }, cancellationToken);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/import", async Task<Results<Created<GameView>, ProblemHttpResult>> (Game game, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(game, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/games/{result.Value.Id}", GameView.Of(result.Value))
                    : result.Error!.ToProblem();
            })
            .WithValidation<Game>()
            .WithName("ImportGame");
}
