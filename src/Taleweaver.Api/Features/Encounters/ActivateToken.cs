using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>An Adventurer activates a token ("objective:red", "totem:blue"…): its hidden text is read.</summary>
public static class ActivateToken
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/tokens/{token}/activation", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string token, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.ActivateToken(token), cancellationToken)).ToResponse())
            .WithName("ActivateToken");
}
