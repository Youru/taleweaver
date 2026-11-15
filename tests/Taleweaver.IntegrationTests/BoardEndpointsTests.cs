using System.Net;
using System.Net.Http.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Packs;

namespace Taleweaver.IntegrationTests;

/// <summary>What the map view reads: the board pieces of the enabled packs and their images.</summary>
public class BoardEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetBoardPieces_HomebrewContent_ReturnsTilesTokensAndFigures()
    {
        var pieces = await _client.GetFromJsonAsync<GetBoardPieces.Response>("/api/catalog/board", JsonDefaults.Options, TestContext.Current.CancellationToken);

        pieces!.Tiles["hb:tile/meadow"].Should().Be(new GetBoardPieces.TilePiece("Meadow", 6, 6, "assets/hb/pixel.png"));
        pieces.Tokens.Should().ContainKey("hb:token/objective");
        pieces.Figures["brute"].Image.Should().Be("assets/hb/pixel.png");
    }

    [Fact]
    public async Task GetAsset_Image_ReturnsTheFile()
    {
        var response = await _client.GetAsync("/api/assets/hb/pixel.png", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Theory]
    [InlineData("/api/assets/hb/missing.png")]
    [InlineData("/api/assets/hb/notes.txt")]
    [InlineData("/api/assets/hb/..%2F..%2Fpacks%2Fhb%2Fpack.json")]
    [InlineData("/api/assets/..%2Fscenarios%2Fhb%2Fmeadow-ambush.json")]
    public async Task GetAsset_MissingNotAnImageOrOutsideTheFolder_ReturnsNotFound(string url)
    {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
