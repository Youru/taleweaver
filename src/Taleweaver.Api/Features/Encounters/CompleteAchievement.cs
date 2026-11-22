using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>An adventurer completed one of the encounter's Achievements: its text, then its reward.</summary>
public static class CompleteAchievement
{
    public sealed record Request(string Adventurer);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/achievements/{achievement}", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string achievement, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.CompleteAchievement(achievement, request.Adventurer), cancellationToken)).ToResponse())
            .WithName("CompleteAchievement");
}
