using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Allies;

/// <summary>
/// Command Combatants (rulebook p. 58, 75-77). Their Damage, Effects, moves and Defeat go by the encounter's routes,
/// the figure being "ally:&lt;id&gt;".
/// </summary>
public static class AlliesEndpoints
{
    public sealed record SummonRequest(string Adventurer, string Card);

    public sealed record AddRequest(string Card, string? Owner = null);

    /// <summary><paramref name="IgnoreDamage"/>: dismissed instead of taking its Damage, it counts as Defeated.</summary>
    public sealed record DismissRequest(bool IgnoreDamage = false);

    public sealed record EarnRequest(string Card);

    public sealed record FormRequest(string? Card = null);

    public sealed class SummonValidator : AbstractValidator<SummonRequest>
    {
        public SummonValidator()
        {
            RuleFor(r => r.Adventurer).NotEmpty();
            RuleFor(r => r.Card).NotEmpty();
        }
    }

    public sealed class AddValidator : AbstractValidator<AddRequest>
    {
        public AddValidator() => RuleFor(r => r.Card).NotEmpty();
    }

    public sealed class EarnValidator : AbstractValidator<EarnRequest>
    {
        public EarnValidator() => RuleFor(r => r.Card).NotEmpty();
    }

    public static IEndpointRouteBuilder MapAlliesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/allies").WithTags("Allies");
        group.MapPost("/summons", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, SummonRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Summon(request.Adventurer, request.Card), cancellationToken)).ToResponse())
            .WithValidation<SummonRequest>()
            .WithName("Summon");
        group.MapPost("/", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, AddRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.AddAlly(request.Card, request.Owner), cancellationToken)).ToResponse())
            .WithValidation<AddRequest>()
            .WithName("AddAlly");
        group.MapPost("/{ally}/dismissal", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string ally, DismissRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.DismissAlly(ally, request.IgnoreDamage), cancellationToken)).ToResponse())
            .WithName("DismissAlly");
        group.MapPost("/conduits/{adventurer}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, EarnRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.EarnConduit(adventurer, request.Card), cancellationToken)).ToResponse())
            .WithValidation<EarnRequest>()
            .WithName("EarnConduit");
        group.MapPut("/conduits/{adventurer}/form", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, FormRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.ConduitForm(adventurer, request.Card), cancellationToken)).ToResponse())
            .WithName("TakeConduitForm");
        return app;
    }
}
