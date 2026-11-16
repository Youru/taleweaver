using Taleweaver.Api.Content;

namespace Taleweaver.UnitTests.Content;

public class PackSelectionTests
{
    [Fact]
    public void Choose_NoneNamed_EnablesEveryPackAfterItsDependencies()
    {
        var packs = new[] { Pack("be1", "um1"), Pack("um1") };

        var choice = PackSelection.Choose(packs, []);

        choice.Enabled.Select(p => p.Id).Should().Equal("um1", "be1");
        choice.Missing.Should().BeEmpty();
    }

    [Fact]
    public void Choose_DependencyNotEnabled_LeavesThePackOutAndSaysWhy()
    {
        var packs = new[] { Pack("um1"), Pack("be1", "um1"), Pack("be1-extra", "be1") };

        var choice = PackSelection.Choose(packs, ["be1", "be1-extra"]);

        choice.Enabled.Should().BeEmpty();
        choice.Missing["be1"].Should().Equal("um1");
        choice.Missing["be1-extra"].Should().Equal("be1");
    }

    [Fact]
    public void Choose_CycleOfDependencies_StillEnablesEachPackOnce()
    {
        var packs = new[] { Pack("a", "b"), Pack("b", "a") };

        var choice = PackSelection.Choose(packs, []);

        choice.Enabled.Select(p => p.Id).Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void Catalog_PackOverridingACard_ReplacesItOnlyWhenItsManifestSaysSo()
    {
        var core = Pack("um1") with { Combatants = [Card("um1:combatant/sickle", "old"), Card("um1:combatant/wurm", "old")] };
        var expansion = Pack("be1", "um1") with
        {
            Manifest = Pack("be1", "um1").Manifest with { Overrides = ["um1:combatant/sickle"] },
            Combatants = [Card("um1:combatant/sickle", "1.2"), Card("um1:combatant/wurm", "stray")],
        };

        var catalog = new Catalog(PackSelection.Choose([expansion, core], []).Enabled);

        catalog.FindCombatant("um1:combatant/sickle")!.Note.Should().Be("1.2");
        catalog.FindCombatant("um1:combatant/wurm")!.Note.Should().Be("old");
    }

    private static Pack Pack(string id, params string[] dependsOn) => new()
    {
        Manifest = new PackManifest { Id = id, Name = new Dictionary<string, string> { ["en"] = id }, DependsOn = dependsOn },
    };

    private static Combatant Card(string id, string note) => new()
    {
        Id = id, Family = id.Split('/')[^1], Kind = "intelligent", Name = new Dictionary<string, string> { ["en"] = id }, Note = note,
    };
}
