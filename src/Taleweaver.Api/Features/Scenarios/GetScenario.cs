using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Scenarios;

/// <summary>Full content of a scenario, by "pack" and "name" (id "pack:scenario/name").</summary>
public static class GetScenario
{
    public static class Errors
    {
        public static Error NotFound(string id) => Error.NotFound("Scenarios.NotFound", $"No scenario '{id}'.");
    }

    public sealed class Handler(IContentStore store)
    {
        public async Task<Result<Scenario>> Handle(string id, CancellationToken cancellationToken) =>
            await store.GetScenarioAsync(id, cancellationToken) is { } scenario ? scenario : Errors.NotFound(id);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{pack}/{name}", async Task<Results<Ok<Scenario>, ProblemHttpResult>> (
                string pack, string name, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle($"{pack}:scenario/{name}", cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
            })
            .WithName("GetScenario");
}
