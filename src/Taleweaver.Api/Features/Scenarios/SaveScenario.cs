using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Scenarios;

/// <summary>
/// Creates or replaces a scenario of the workshop ("homebrew:scenario/&lt;name&gt;"); official scenarios
/// are never written. Returns the consistency report, so that the editor shows what is left to fix.
/// </summary>
public static partial class SaveScenario
{
    public static class Errors
    {
        public static Error IdMismatch(string expected) =>
            Error.Validation("Scenarios.IdMismatch", $"The scenario id must be '{expected}'.");

        public static Error BadName(string name) =>
            Error.Validation("Scenarios.BadName", $"'{name}': only lower-case letters, digits and dashes.");
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex NamePattern();

    public sealed class Handler(IContentStore store, CatalogProvider catalogs)
    {
        public async Task<Result<ScenarioReport>> Handle(string name, Scenario scenario, CancellationToken cancellationToken)
        {
            if (!NamePattern().IsMatch(name))
            {
                return Errors.BadName(name);
            }

            var id = $"{Scenario.HomebrewPack}:scenario/{name}";
            if (scenario.Id != id)
            {
                return Errors.IdMismatch(id);
            }

            await store.SaveScenarioAsync(scenario, cancellationToken);
            return ScenarioChecker.Check(scenario, await catalogs.GetAsync(cancellationToken));
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut($"/{Scenario.HomebrewPack}/{{name}}", async Task<Results<Ok<ScenarioReport>, ProblemHttpResult>> (
                string name, Scenario scenario, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(name, scenario, cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
            })
            .WithValidation<Scenario>()
            .WithName("SaveScenario");
}
