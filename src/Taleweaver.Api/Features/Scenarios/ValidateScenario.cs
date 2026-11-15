using FluentValidation;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Scenarios;

/// <summary>
/// Checks a scenario against the catalogue of the enabled packs. A malformed scenario (missing ids)
/// is rejected with 400; consistency problems are returned in the report (errors and warnings).
/// </summary>
public static class ValidateScenario
{
    public sealed class Validator : AbstractValidator<Scenario>
    {
        public Validator()
        {
            RuleFor(s => s.Id).NotEmpty();
            RuleFor(s => s.Start).NotEmpty();
            RuleFor(s => s.Nodes).NotEmpty();
            RuleForEach(s => s.Nodes).ChildRules(node =>
            {
                node.RuleFor(n => n.Id).NotEmpty();
                node.RuleFor(n => n.Type).NotEmpty();
            });
        }
    }

    public sealed class Handler(CatalogProvider catalogs)
    {
        public async Task<ScenarioReport> Handle(Scenario scenario, CancellationToken cancellationToken) =>
            ScenarioChecker.Check(scenario, await catalogs.GetAsync(cancellationToken));
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/validate", async (Scenario scenario, Handler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.Handle(scenario, cancellationToken)))
            .WithValidation<Scenario>()
            .WithName("ValidateScenario");
}
