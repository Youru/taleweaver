using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Features.ContentPacks;
using Taleweaver.Api.Features.Packs;
using Taleweaver.Api.Storage;

namespace Taleweaver.IntegrationTests;

/// <summary>A content pack, made apart from the app, installed and removed while it runs.</summary>
public sealed class ContentPacksEndpointsTests : IClassFixture<HomebrewFactory>, IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _installed = Directory.CreateTempSubdirectory("taleweaver-content-packs-").FullName;
    private readonly HttpClient _client;

    public ContentPacksEndpointsTests(HomebrewFactory factory) =>
        _client = factory.WithWebHostBuilder(b => b
                .UseSetting("Content:InstalledPacksPath", _installed)
                .UseSetting("Content:EnabledPacks:1", "demo"))
            .CreateClient();

    public void Dispose() => Directory.Delete(_installed, recursive: true);

    [Fact]
    public async Task Install_TheDemoAsAPack_BringsItsCatalogueScenarioAndPictures_UntilRemoved()
    {
        (await _client.GetFromJsonAsync<List<ContentPackFiles.Installed>>("/api/content-packs", JsonDefaults.Options, Token)).Should().BeEmpty();

        var response = await PostAsync(DemoPack());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var installed = await response.Content.ReadFromJsonAsync<ContentPackFiles.Installed>(JsonDefaults.Options, Token);
        installed!.Id.Should().Be("demo-content");
        installed.Packs.Should().Equal("demo");
        installed.Scenarios.Should().Be(1);
        (await Packs()).Should().Contain(p => p.Id == "demo" && p.Enabled);
        (await _client.GetFromJsonAsync<List<ScenarioSummary>>("/api/scenarios", JsonDefaults.Options, Token))
            .Should().Contain(s => s.Id == "demo:scenario/bandit-clearing");
        (await _client.GetAsync("/api/assets/demo/clearing.png", Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.DeleteAsync("/api/content-packs/demo-content", Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Packs()).Should().NotContain(p => p.Id == "demo");
        (await _client.GetAsync("/api/assets/demo/clearing.png", Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("../outside.json", "points out of the pack")]
    [InlineData("tools/run.exe", "is not in")]
    [InlineData("assets/run.exe", "only")]
    public async Task Install_AFileOutsideTheContent_IsRefusedAndNothingIsInstalled(string entry, string why)
    {
        var response = await PostAsync(Zip(("content-pack.json", """{ "id": "bad", "name": { "en": "Bad" } }"""), (entry, "x")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        problem!.Title.Should().Be("ContentPacks.Unreadable");
        problem.Detail.Should().Contain(why);
        Directory.EnumerateFileSystemEntries(_installed).Should().BeEmpty();
    }

    [Fact]
    public async Task Install_NotAZip_IsRefused()
    {
        var response = await PostAsync([1, 2, 3]);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<List<ListPacks.PackResponse>> Packs() =>
        (await _client.GetFromJsonAsync<List<ListPacks.PackResponse>>("/api/packs", JsonDefaults.Options, Token))!;

    private Task<HttpResponseMessage> PostAsync(byte[] file)
    {
        var body = new ByteArrayContent(file);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        return _client.PostAsync("/api/content-packs", body, Token);
    }

    /// <summary>taleweaver/demo, as scripts/package.py --content-pack would make it.</summary>
    private static byte[] DemoPack()
    {
        var demo = DemoRoot();
        var files = Directory.EnumerateFiles(demo, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(demo, f).Replace('\\', '/'))
            .Where(f => f.Contains('/'))
            .Select(f => (f, (object)File.ReadAllBytes(Path.Combine(demo, f))));
        return Zip([("content-pack.json", """{ "id": "demo-content", "name": { "en": "Demo" }, "version": "1" }"""), .. files]);
    }

    private static byte[] Zip(params (string Name, object Data)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(data is string text ? System.Text.Encoding.UTF8.GetBytes(text) : (byte[])data);
            }
        }

        return memory.ToArray();
    }

    private static string DemoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "demo", "packs", "demo", "pack.json")))
            {
                return Path.Combine(dir.FullName, "demo");
            }
        }

        throw new InvalidOperationException("taleweaver/demo not found.");
    }
}
