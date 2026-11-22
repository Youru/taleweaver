using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using SkiaSharp;
using Taleweaver.Api.Hosting;

namespace Taleweaver.IntegrationTests;

/// <summary>The app without Vite: its own front, its compressed pictures, and what it refuses to other sites.</summary>
public sealed class HostingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("taleweaver-hosting-").FullName;
    private readonly WebApplicationFactory<Program> _factory;

    public HostingTests()
    {
        Write("wwwroot/index.html", "<html>front</html>"u8.ToArray());
        Write("wwwroot/assets/app-1a2b.js", "//"u8.ToArray());
        Write("secret.txt", "secret"u8.ToArray());
        Directory.CreateDirectory(Path.Combine(_root, "packs"));
        using var bitmap = new SKBitmap(400, 200);
        bitmap.Erase(SKColors.Red);
        using var picture = bitmap.Encode(SKEncodedImageFormat.Webp, 82);
        Write("assets/tiles/one.webp", picture.ToArray());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseContentRoot(_root)
            .UseSetting("Content:RootPath", _root)
            .UseSetting("Content:SavesPath", Path.Combine(_root, "saves")));
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/join/KRVA")]
    [InlineData("/games/abc")]
    public async Task APhone_GetsThePage(string path)
    {
        var response = await Phone().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("front");
    }

    [Fact]
    public async Task APhone_GetsTheBuiltFilesAndNothingElse()
    {
        var phone = Phone();

        (await phone.GetAsync("/assets/app-1a2b.js", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await phone.GetAsync("/secret.txt", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await phone.GetAsync("/%2e%2e%2fsecret.txt", TestContext.Current.CancellationToken)).StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await phone.GetAsync("/api/games", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await phone.GetAsync("/api/assets/tiles/one.png", TestContext.Current.CancellationToken)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Theory]
    [InlineData("one.jpg")]
    [InlineData("one.png?w=160")]
    public async Task ACompressedPicture_AnswersToItsName(string name)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/assets/tiles/{name}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
    }

    [Theory]
    [InlineData("https://example.invalid", false)]
    [InlineData("null", false)]
    [InlineData("http://localhost:5173", true)]
    [InlineData(null, true)]
    public async Task AnotherSite_DoesNotAct(string? origin, bool allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/games/none/undo");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        var response = await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(allowed ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnotherSite_DoesNotOpenTheHub()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/hubs/room");
        request.Headers.Add("Origin", "https://example.invalid");

        (await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("192.168.1.19", true)]
    [InlineData("[::1]", true)]
    [InlineData("table.local", true)]
    [InlineData("evil.example", false)]
    [InlineData("localhost.evil.example", false)]
    public void AName_IsNotAKnownHost(string host, bool known) => RequestGuards.IsKnownHost(host, "table.local").Should().Be(known);

    [Fact]
    public void TheSameAddress_IsItsOwnOrigin() =>
        RequestGuards.IsOwnOrigin("http://192.168.1.19:5180", new HostString("192.168.1.19", 5180)).Should().BeTrue();

    private HttpClient Phone()
    {
        var phone = _factory.CreateClient();
        phone.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.20");
        return phone;
    }

    private void Write(string path, byte[] content)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
    }
}
