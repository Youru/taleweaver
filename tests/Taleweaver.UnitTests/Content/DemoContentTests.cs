using Taleweaver.Api.Content;

namespace Taleweaver.UnitTests.Content;

/// <summary>taleweaver/demo: the content of the app the CI publishes, and of the e2e without the game's content.</summary>
public class DemoContentTests
{
    private static string DemoRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var demo = Path.Combine(dir.FullName, "demo");
                if (File.Exists(Path.Combine(demo, "packs", "demo", "pack.json")))
                {
                    return demo;
                }
            }

            throw new InvalidOperationException("taleweaver/demo not found.");
        }
    }

    [Fact]
    public async Task DemoScenario_WithTheDemoPack_HasNoError()
    {
        var store = RepositoryContent.CreateStore(DemoRoot);
        var catalog = new Catalog(PackSelection.Choose(await store.GetPacksAsync(TestContext.Current.CancellationToken), []).Enabled);
        var scenario = await store.GetScenarioAsync("demo:scenario/bandit-clearing", TestContext.Current.CancellationToken);

        scenario.Should().NotBeNull();
        var report = ScenarioChecker.Check(scenario!, catalog);
        report.Errors.Should().BeEmpty();
        report.Warnings.Should().BeEmpty();
    }
}
