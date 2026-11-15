using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Scenarios;

public static class ListScenarios
{
    public sealed class Handler(IContentStore store)
    {
        public Task<IReadOnlyList<ScenarioSummary>> Handle(CancellationToken cancellationToken) => store.ListScenariosAsync(cancellationToken);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(cancellationToken)))
            .WithName("ListScenarios");
}
