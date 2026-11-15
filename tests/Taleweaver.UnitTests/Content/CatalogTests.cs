using Taleweaver.Api.Content;

namespace Taleweaver.UnitTests.Content;

public class CatalogTests
{
    private static readonly Catalog Catalog = new(
    [
        new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = new Dictionary<string, string> { ["en"] = "Homebrew" } },
            Combatants =
            [
                Card("brute-mundane", "brute", "mundane"),
                Card("brute-uncommon", "brute", "uncommon"),
                Card("brute-command", "brute", "any", kind: "command"),
                Card("wisp", "wisp", "any"),
            ],
        },
    ]);

    [Theory]
    [InlineData("mundane", "hb:combatant/brute-mundane")]
    [InlineData("common", "hb:combatant/brute-mundane")]
    [InlineData("uncommon", "hb:combatant/brute-uncommon")]
    [InlineData("rare", "hb:combatant/brute-uncommon")]
    public void CardFor_EncounterLootLevel_PicksHighestCardNotAboveIt(string lootLevel, string expected)
    {
        var card = Catalog.CardFor("brute", lootLevel);

        card!.Id.Should().Be(expected);
    }

    [Fact]
    public void CardFor_CardOfAnyLevel_AlwaysQualifies()
    {
        var card = Catalog.CardFor("wisp", "rare");

        card!.Id.Should().Be("hb:combatant/wisp");
    }

    [Fact]
    public void CardFor_UnknownFamily_ReturnsNull()
    {
        var card = Catalog.CardFor("dragon", "rare");

        card.Should().BeNull();
    }

    [Fact]
    public void IntelligentCards_FamilyWithCommandCard_ExcludesIt()
    {
        var cards = Catalog.IntelligentCards("brute");

        cards.Should().HaveCount(2).And.OnlyContain(c => c.Kind == "intelligent");
    }

    [Fact]
    public void Contains_IdOfAnotherPack_ReturnsFalse()
    {
        var found = Catalog.Contains("um1:combatant/brute-mundane");

        found.Should().BeFalse();
    }

    private static Combatant Card(string name, string family, string lootLevel, string kind = "intelligent") => new()
    {
        Id = $"hb:combatant/{name}",
        Family = family,
        Name = new Dictionary<string, string> { ["en"] = name },
        Kind = kind,
        LootLevel = lootLevel,
    };
}
