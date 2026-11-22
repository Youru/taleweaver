using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>
/// Damage dealt to a combatant or an ally (negative: healed). Reaching its Health defeats it. For a Loyal Esper:
/// whether an opponent dealt it (its summoner takes it too), and the Summon tokens discarded.
/// </summary>
public static class ApplyDamage
{
    public sealed record Request(int Amount, bool? FromOpponent = null, int? Tokens = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Amount).NotEqual(0).InclusiveBetween(-999, 999);
            RuleFor(r => r.Tokens).InclusiveBetween(0, 9);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/combatants/{instance}/damage", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string instance, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Damage(instance, request.Amount) with { FromOpponent = request.FromOpponent, Tokens = request.Tokens }, cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("ApplyDamage");
}
