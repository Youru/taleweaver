using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Scenarios;

/// <summary>Deletes a scenario of the workshop; official scenarios cannot be deleted.</summary>
public static class DeleteScenario
{
    public sealed class Handler(IContentStore store)
    {
        public async Task<Result> Handle(string name, CancellationToken cancellationToken)
        {
            var id = $"{Scenario.HomebrewPack}:scenario/{name}";
            return await store.DeleteScenarioAsync(id, cancellationToken) ? Result.Success() : GetScenario.Errors.NotFound(id);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete($"/{Scenario.HomebrewPack}/{{name}}", async Task<Results<NoContent, ProblemHttpResult>> (
                string name, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(name, cancellationToken);
                return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
            })
            .WithName("DeleteScenario");
}
