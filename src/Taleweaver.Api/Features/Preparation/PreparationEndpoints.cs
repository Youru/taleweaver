using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Preparation;

/// <summary>
/// The GM's side of the departure (roadmap §5.2): who comes and with what, then the departure. The phones' choices
/// come through the room. Each command returns the new game state.
/// </summary>
public static class PreparationEndpoints
{
    /// <summary>What is left out is kept; an empty kit takes the kit away.</summary>
    public sealed record ChoiceRequest(string Adventurer, string? Kit = null, IReadOnlyList<string>? Items = null, IReadOnlyList<string>? Disciplines = null);

    public sealed record HiddenRequest(bool Shown);

    public sealed class ChoiceValidator : AbstractValidator<ChoiceRequest>
    {
        public ChoiceValidator()
        {
            RuleFor(r => r.Adventurer).NotEmpty().MaximumLength(80);
            RuleFor(r => r.Items).Must(i => i is null || i.Count <= 30).WithMessage("Too many cards.");
            RuleFor(r => r.Disciplines).Must(d => d is null || d.Count <= 30).WithMessage("Too many Disciplines.");
        }
    }

    public static IEndpointRouteBuilder MapPreparationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/preparation").WithTags("Preparation");
        group.MapPut("/adventurers", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, ChoiceRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.ChooseAdventurer(request.Adventurer, request.Kit, request.Items, request.Disciplines), cancellationToken)).ToResponse())
            .WithValidation<ChoiceRequest>()
            .WithName("ChooseAdventurer");
        group.MapDelete("/adventurers/{adventurer}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.RemoveAdventurer(adventurer), cancellationToken)).ToResponse())
            .WithName("RemoveAdventurer");
        group.MapPut("/hidden", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, HiddenRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.ShowHiddenAdventurers(request.Shown), cancellationToken)).ToResponse())
            .WithName("ShowHiddenAdventurers");
        group.MapPost("/departure", async Task<Results<Ok<GameView>, ProblemHttpResult>> (string id, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.SetOut(), cancellationToken)).ToResponse())
            .WithName("SetOut");
        return app;
    }
}
