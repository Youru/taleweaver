using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using SkiaSharp;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Assets;

namespace Taleweaver.UnitTests.Features;

/// <summary>The phones get smaller copies of the cards (room-spec §10.3), made once and kept next to the saves.</summary>
public sealed class ResizedAssetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tw-assets-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task AWideCard_IsServedAtTheWidthAsked_FromTheCacheTheSecondTime()
    {
        var original = Picture("cards/wide.jpg", 800, 400);
        var handler = Handler();

        var resized = await handler.ResizedAsync(original, 320, TestContext.Current.CancellationToken);

        resized.Should().NotBe(original).And.StartWith(Path.Combine(_root, "saves", "cache", "images", "320"));
        using (var bitmap = SKBitmap.Decode(resized))
        {
            (bitmap.Width, bitmap.Height).Should().Be((320, 160));
        }

        var written = File.GetLastWriteTimeUtc(resized);
        (await handler.ResizedAsync(original, 320, TestContext.Current.CancellationToken)).Should().Be(resized);
        File.GetLastWriteTimeUtc(resized).Should().Be(written, "made once");
    }

    [Fact]
    public async Task ASmallPicture_IsServedAsItIs()
    {
        var original = Picture("tokens/small.png", 100, 100);

        (await Handler().ResizedAsync(original, 320, TestContext.Current.CancellationToken)).Should().Be(original);
    }

    private GetAsset.Handler Handler()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(_root);
        var options = Options.Create(new ContentOptions { RootPath = _root, SavesPath = Path.Combine(_root, "saves") });
        return new GetAsset.Handler(options, environment);
    }

    private string Picture(string relative, int width, int height)
    {
        var path = Path.Combine(_root, new ContentOptions().AssetsDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.IndianRed);
        using var data = bitmap.Encode(path.EndsWith(".png", StringComparison.Ordinal) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
