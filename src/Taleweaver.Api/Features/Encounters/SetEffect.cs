using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>Adds or removes an Effect (poison, burn…) on a combatant; STATUS effects are recalled at its turn.</summary>
public static class SetEffect
{
    public sealed record Request(string Effect, bool Active = true);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(r => r.Effect).NotEmpty().MaximumLength(40);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/combatants/{instance}/effects", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string instance, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.SetEffect(instance, request.Effect, request.Active), cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("SetEffect");
}
