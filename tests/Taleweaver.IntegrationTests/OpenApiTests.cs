using System.Net;

namespace Taleweaver.IntegrationTests;

public class OpenApiTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    [Fact]
    public async Task OpenApiDocument_Development_DescribesEveryEndpoint()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        document.Should().Contain("/api/packs").And.Contain("/api/catalog").And.Contain("/api/scenarios/validate");
    }
}
