using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Encounters;

/// <summary>
/// Next card of the initiative track. At the end of the track: Urgency check, then a new shuffled round.
/// An Intelligent group's turn asks the GM its AI conditions one by one.
/// </summary>
public static class NextTurn
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/next-turn", async Task<Results<Ok<GameView>, ProblemHttpResult>> (string id, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id, GameCommand.NextTurn(), cancellationToken)).ToResponse())
            .WithName("NextTurn");
}
