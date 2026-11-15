using NSubstitute;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.Scenarios;
using Taleweaver.Api.Storage;

namespace Taleweaver.UnitTests.Features;

public class GetScenarioTests
{
    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        var store = Substitute.For<IContentStore>();
        store.GetScenarioAsync("hb:scenario/missing", Arg.Any<CancellationToken>()).Returns((Scenario?)null);

        var result = await new GetScenario.Handler(store).Handle("hb:scenario/missing", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Scenarios.NotFound");
    }
}
