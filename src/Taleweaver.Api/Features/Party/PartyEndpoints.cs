using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Party;

/// <summary>
/// The party between encounters (rulebook p. 10-11): the Active Group chosen when the story says so, and the
/// members marked Injured or Unselectable. Each command returns the new game state.
/// </summary>
public static class PartyEndpoints
{
    public sealed record ActiveGroupRequest(IReadOnlyList<string> Adventurers);

    public sealed record StatusRequest(bool Value);

    public sealed record DisciplineRequest(string Discipline, int Xp);

    public sealed record DamageRequest(int Amount);

    public sealed record StaminaRequest(int Value);

    public static IEndpointRouteBuilder MapPartyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/party").WithTags("Party");
        group.MapPut("/active-group", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, ActiveGroupRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.SetActiveGroup([.. request.Adventurers]), cancellationToken)).ToResponse())
            .WithName("SetActiveGroup");
        group.MapPost("/{adventurer}/disciplines", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, DisciplineRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.LearnDiscipline(adventurer, request.Discipline, request.Xp), cancellationToken)).ToResponse())
            .WithName("LearnDiscipline");
        group.MapDelete("/{adventurer}/disciplines/{discipline}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, string discipline, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.ForgetDiscipline(adventurer, discipline), cancellationToken)).ToResponse())
            .WithName("ForgetDiscipline");
        group.MapPost("/{adventurer}/respec", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Respec(adventurer), cancellationToken)).ToResponse())
            .WithName("Respec");
        // Damage (negative: healed) and SP: in and out of encounters, Damage stays from one to the next (rulebook p. 69).
        group.MapPost("/{adventurer}/damage", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, DamageRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.Damage("adventurer:" + adventurer, request.Amount), cancellationToken)).ToResponse())
            .WithName("DamageAdventurer");
        group.MapPut("/{adventurer}/stamina", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, StaminaRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.SetStamina("adventurer:" + adventurer, request.Value), cancellationToken)).ToResponse())
            .WithName("SetStamina");
        group.MapPut("/{adventurer}/{status}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string adventurer, string status, StatusRequest request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.SetAdventurerStatus(adventurer, status, request.Value), cancellationToken)).ToResponse())
            .WithName("SetAdventurerStatus");
        return app;
    }
}
