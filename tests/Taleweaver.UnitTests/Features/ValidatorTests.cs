using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Packs;
using Taleweaver.Api.Features.Scenarios;

namespace Taleweaver.UnitTests.Features;

public class ValidatorTests
{
    [Fact]
    public void SearchCatalogValidator_UnknownKind_Fails()
    {
        var result = new SearchCatalog.Validator().Validate(new SearchCatalog.Request("monster", null, null, null));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Kind");
    }

    [Fact]
    public void SearchCatalogValidator_KnownKindAndQuery_Passes()
    {
        var result = new SearchCatalog.Validator().Validate(new SearchCatalog.Request("item", "sickle", "um1", 20));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateScenarioValidator_NodeWithoutId_Fails()
    {
        var scenario = new Scenario
        {
            Id = "hb:scenario/test",
            Title = new Dictionary<string, string> { ["en"] = "Test" },
            Start = "a",
            Nodes = [new Node { Id = "", Type = "scene" }],
        };

        var result = new ValidateScenario.Validator().Validate(scenario);

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Nodes[0].Id");
    }
}
