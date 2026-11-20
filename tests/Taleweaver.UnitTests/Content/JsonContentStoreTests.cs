using System.Text.Json;
using System.Text.Json.Nodes;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.UnitTests.Content;

public class JsonContentStoreTests
{
    [Fact]
    public async Task GetPacksAsync_RepositoryContent_LoadsEveryCatalogueFileOfUm1()
    {
        var store = RepositoryContent.CreateStore();

        var packs = await store.GetPacksAsync(TestContext.Current.CancellationToken);

        var um1 = packs.Should().ContainSingle(p => p.Id == "um1").Subject;
        var counts = um1.Manifest.Counts;
        um1.Combatants.Should().HaveCount(counts["combatant"]);
        um1.Items.Should().HaveCount(counts["item"]);
        um1.ItemUpgrades.Should().HaveCount(counts["item-upgrade"]);
        um1.Tiles.Should().HaveCount(counts["tile"]);
        um1.Tokens.Should().HaveCount(counts["token"]);
        um1.InitiativeCards.Should().HaveCount(counts["initiative"]);
        um1.Rules!.LootDecks.Should().HaveCount(counts["lootDecks"]);
    }

    [Fact]
    public async Task GetPacksAsync_CombatantWithVariableStats_ReadsStarAsVariable()
    {
        var catalog = await RepositoryContent.CatalogAsync();

        var agares = catalog.FindCombatant("um1:combatant/agares-command");

        agares!.Stats!.Health!.Value.IsVariable.Should().BeTrue();
        agares.Stats.Movement.Should().Be(new StatValue(4));
    }

    [Fact]
    public async Task ListScenariosAsync_RepositoryContent_ListsBlightOfRhamsted()
    {
        var store = RepositoryContent.CreateStore();

        var scenarios = await store.ListScenariosAsync(TestContext.Current.CancellationToken);

        scenarios.Should().Contain(s => s.Id == "um1:scenario/blight-of-rhamsted" && s.Kind == "bounty");
    }

    [Fact]
    public async Task GetScenarioAsync_Blight_KeepsEveryFieldOfTheFile()
    {
        var path = Path.Combine(RepositoryContent.Root, "scenarios", "um1", "blight-of-rhamsted.json");
        var original = Normalized(JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)));

        var scenario = await RepositoryContent.BlightAsync();

        var roundTrip = Normalized(JsonSerializer.SerializeToNode(scenario, JsonDefaults.FileOptions));
        Differences(roundTrip, original, "$").Should().BeEmpty("the model must not lose anything the editor saves back");
    }

    [Fact]
    public async Task SaveScenarioAsync_NewScenario_WritesItUnderItsPackAndReadsItBack()
    {
        var root = Directory.CreateTempSubdirectory("taleweaver-").FullName;
        try
        {
            var store = RepositoryContent.CreateStore(root);
            var scenario = new Scenario
            {
                Id = "hb:scenario/first-steps",
                Title = new Dictionary<string, string> { ["en"] = "First steps" },
                Start = "intro",
                Nodes = [new Node { Id = "intro", Type = "scene", Next = "@end" }],
            };

            await store.SaveScenarioAsync(scenario, TestContext.Current.CancellationToken);

            File.Exists(Path.Combine(root, "scenarios", "hb", "first-steps.json")).Should().BeTrue();
            var read = await store.GetScenarioAsync(scenario.Id, TestContext.Current.CancellationToken);
            read!.Nodes.Should().ContainSingle().Which.Next.Should().Be("@end");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetScenarioAsync_CampaignFolder_ReadsItsChaptersAsOneScenarioInOrder()
    {
        var root = Directory.CreateTempSubdirectory("taleweaver-").FullName;
        try
        {
            var act = Directory.CreateDirectory(Path.Combine(root, "scenarios", "hb", "act1")).FullName;
            await File.WriteAllTextAsync(Path.Combine(act, "campaign.json"),
                """{ "id": "hb:scenario/act1", "title": { "en": "Act 1" }, "start": "b", "chapters": ["one", "two"] }""",
                TestContext.Current.CancellationToken);
            Directory.CreateDirectory(Path.Combine(act, "one"));
            Directory.CreateDirectory(Path.Combine(act, "two"));
            await File.WriteAllTextAsync(Path.Combine(act, "one", "chapter.json"), """{ "nodes": ["b", "a"] }""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(act, "one", "a.json"), """{ "id": "a", "type": "scene", "next": "c" }""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(act, "one", "b.json"), """{ "id": "b", "type": "scene", "next": "a" }""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(act, "two", "c.json"), """{ "id": "c", "type": "scene", "next": "@end" }""", TestContext.Current.CancellationToken);
            var store = RepositoryContent.CreateStore(root);

            var scenario = await store.GetScenarioAsync("hb:scenario/act1", TestContext.Current.CancellationToken);
            var list = await store.ListScenariosAsync(TestContext.Current.CancellationToken);

            scenario!.Nodes.Select(n => (n.Id, n.Chapter)).Should().Equal(("b", "one"), ("a", "one"), ("c", "two"));
            list.Should().ContainSingle(s => s.Id == "hb:scenario/act1", "the files of a campaign folder are not scenarios of their own");
            scenario.Chapters.Should().Equal(new ScenarioChapter("one"), new ScenarioChapter("two"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveScenarioAsync_HomebrewWithChapters_WritesAFolderAndReadsItBack()
    {
        var root = Directory.CreateTempSubdirectory("taleweaver-").FullName;
        try
        {
            var store = RepositoryContent.CreateStore(root);
            var ct = TestContext.Current.CancellationToken;

            await store.SaveScenarioAsync(Campaign(), ct);

            var folder = Path.Combine(root, "scenarios", "homebrew", "saga");
            File.Exists(Path.Combine(folder, "campaign.json")).Should().BeTrue();
            File.Exists(Path.Combine(folder, "ch-2", "c.json")).Should().BeTrue();
            File.ReadAllText(Path.Combine(folder, "ch-1", "a.json")).Should().NotContain("chapter", "the folder says it");
            var read = await store.GetScenarioAsync("homebrew:scenario/saga", ct);
            read!.Nodes.Select(n => (n.Id, n.Chapter)).Should().Equal(("b", "ch-1"), ("a", "ch-1"), ("c", "ch-2"));
            read.Chapters![0].Title!["en"].Should().Be("One");
            (await store.ListScenariosAsync(ct)).Should().ContainSingle(s => s.Id == "homebrew:scenario/saga").Which.Editable.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveScenarioAsync_StepMovedAndChapterDropped_LeavesNoOrphanFile()
    {
        var root = Directory.CreateTempSubdirectory("taleweaver-").FullName;
        try
        {
            var store = RepositoryContent.CreateStore(root);
            var ct = TestContext.Current.CancellationToken;
            var campaign = Campaign();
            await store.SaveScenarioAsync(campaign, ct);

            await store.SaveScenarioAsync(campaign with
            {
                Chapters = [campaign.Chapters![0]],
                Nodes = campaign.Nodes.Select(n => n with { Chapter = "ch-1" }).ToList(),
            }, ct);

            var folder = Path.Combine(root, "scenarios", "homebrew", "saga");
            Directory.Exists(Path.Combine(folder, "ch-2")).Should().BeFalse();
            var read = await store.GetScenarioAsync("homebrew:scenario/saga", ct);
            read!.Nodes.Select(n => (n.Id, n.Chapter)).Should().Equal(("b", "ch-1"), ("a", "ch-1"), ("c", "ch-1"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveScenarioAsync_ChaptersAddedOrRemoved_MovesBetweenFileAndFolder()
    {
        var root = Directory.CreateTempSubdirectory("taleweaver-").FullName;
        try
        {
            var store = RepositoryContent.CreateStore(root);
            var ct = TestContext.Current.CancellationToken;
            var homebrew = Path.Combine(root, "scenarios", "homebrew");
            var single = Campaign() with { Chapters = null, Nodes = Campaign().Nodes.Select(n => n with { Chapter = null }).ToList() };
            await store.SaveScenarioAsync(single, ct);

            await store.SaveScenarioAsync(Campaign(), ct);
            File.Exists(Path.Combine(homebrew, "saga.json")).Should().BeFalse();
            Directory.Exists(Path.Combine(homebrew, "saga")).Should().BeTrue();

            await store.SaveScenarioAsync(single, ct);
            File.Exists(Path.Combine(homebrew, "saga.json")).Should().BeTrue();
            Directory.Exists(Path.Combine(homebrew, "saga")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteScenarioAsync_HomebrewCampaign_RemovesTheFolder()
    {
        var root = Directory.CreateTempSubdirectory("taleweaver-").FullName;
        try
        {
            var store = RepositoryContent.CreateStore(root);
            var ct = TestContext.Current.CancellationToken;
            await store.SaveScenarioAsync(Campaign(), ct);

            var deleted = await store.DeleteScenarioAsync("homebrew:scenario/saga", ct);

            deleted.Should().BeTrue();
            Directory.Exists(Path.Combine(root, "scenarios", "homebrew", "saga")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Scenario Campaign() => new()
    {
        Id = "homebrew:scenario/saga",
        Title = new Dictionary<string, string> { ["en"] = "Saga" },
        Start = "b",
        Chapters = [new ScenarioChapter("ch-1", new Dictionary<string, string> { ["en"] = "One" }), new ScenarioChapter("ch-2")],
        Nodes =
        [
            new Node { Id = "b", Type = "scene", Chapter = "ch-1", Next = "a" },
            new Node { Id = "a", Type = "scene", Chapter = "ch-1", Next = "c" },
            new Node { Id = "c", Type = "scene", Chapter = "ch-2", Next = "@end" },
        ],
    };

    private static IEnumerable<string> Differences(JsonNode? actual, JsonNode? expected, string path)
    {
        switch (actual, expected)
        {
            case (JsonObject a, JsonObject e):
                foreach (var key in a.Select(p => p.Key).Union(e.Select(p => p.Key)))
                {
                    foreach (var difference in Differences(a[key], e[key], $"{path}.{key}"))
                    {
                        yield return difference;
                    }
                }

                break;
            case (JsonArray a, JsonArray e) when a.Count == e.Count:
                for (var i = 0; i < a.Count; i++)
                {
                    foreach (var difference in Differences(a[i], e[i], $"{path}[{i}]"))
                    {
                        yield return difference;
                    }
                }

                break;
            default:
                if (!JsonNode.DeepEquals(actual, expected))
                {
                    yield return $"{path}: {actual?.ToJsonString() ?? "missing"} instead of {expected?.ToJsonString() ?? "missing"}";
                }

                break;
        }
    }

    /// <summary>A missing property, an explicit null and an empty list mean the same thing in the format.</summary>
    private static JsonNode? Normalized(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (value is null or JsonArray { Count: 0 })
                    {
                        obj.Remove(key);
                    }
                    else
                    {
                        Normalized(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Normalized(item);
                }

                break;
        }

        return node;
    }
}
