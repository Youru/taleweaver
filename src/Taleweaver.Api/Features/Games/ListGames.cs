using Taleweaver.Api.Engine;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

public static class ListGames
{
    public sealed record GameSummary(string Id, string Scenario, IReadOnlyList<string> Party, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, GameStatus Status, string? CurrentNode, bool Preparing);

    public sealed class Handler(IGameStore games)
    {
        public async Task<IReadOnlyList<GameSummary>> Handle(CancellationToken cancellationToken) =>
            (await games.ListAsync(cancellationToken))
            .Select(g => new GameSummary(g.Id, g.Scenario, GameView.PartyOf(g), g.CreatedAt, g.UpdatedAt, g.State.Status, g.State.Campaign.CurrentNode, g.State.Preparation is not null))
            .ToList();
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (Handler handler, CancellationToken cancellationToken) => TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("ListGames");
}
