using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The Combatant Loot deck (rulebook p. 20, 57; tags of ch. 11) on a small homebrew pack.</summary>
public class LootTests
{
    [Fact]
    public void Defeat_GoldCards_ArePooledAndEarnedAtTheEndEvenWhenThePartyLoses()
    {
        var game = Game([new LootCard("gold", 5, 2)]);

        game.Do(GameCommand.Defeat("brute-1")).Do(GameCommand.Defeat("brute-2"));
        game.Encounter.LootGold.Should().Be(10);
        game.State.Campaign.Gold.Should().Be(0);
        game.Do(GameCommand.ReachEndCondition("lose-all"));

        game.State.Campaign.Gold.Should().Be(10);
    }

    [Fact]
    public void Defeat_DeckEmpty_DrawsNothingUntilTheRestore()
    {
        var game = Game([new LootCard("gold", 5, 1)]);

        game.Do(GameCommand.Defeat("brute-1")).Do(GameCommand.Defeat("brute-2"));

        game.Encounter.LootGold.Should().Be(5);
        game.Codes.Should().Contain("lootDeckEmpty");
    }

    [Fact]
    public void Restore_PutsTheDrawnCardsBackIntoTheDeck()
    {
        var game = Game([new LootCard("gold", 5, 1)], restoreOnFirstDefeat: true);

        game.Do(GameCommand.Defeat("brute-1")).Do(GameCommand.Defeat("brute-2"));

        game.Encounter.LootGold.Should().Be(10);
        game.Encounter.LootDeck.Should().BeEmpty();
        game.Encounter.LootDrawn.Should().Equal("gold:5");
    }

    [Fact]
    public void Defeat_MonsterLoot_GivesTheCardOnceThenIsDiscardedWhenNoneIsLeft()
    {
        var game = Game([new LootCard("monster", null, 1)]);

        game.Do(GameCommand.Defeat("brute-1"));
        game.State.Campaign.Items.Should().Equal("hb:item/brute-tooth");
        game.Encounter.LootDeck.Should().BeEmpty();

        game.Encounter.LootDeck.Add("monster");
        game.Do(GameCommand.Defeat("brute-2"));

        game.State.Campaign.Items.Should().Equal("hb:item/brute-tooth");
        game.Codes.Should().Contain("lootMonsterDiscarded");
    }

    [Fact]
    public void Defeat_MonsterLootNotAvailable_IsShuffledBackAndAnotherCardIsDrawn()
    {
        var game = Game([new LootCard("monster", null, 1), new LootCard("gold", 6, 1)], monsterLoot: []);

        game.Do(GameCommand.Defeat("brute-1"));

        game.Encounter.LootGold.Should().Be(6);
        game.Encounter.LootDeck.Should().Equal("monster");
    }

    [Fact]
    public void Defeat_UniqueLoot_GivesAUniqueItemOfTheEncounterLootLevel()
    {
        var game = Game([new LootCard("unique", null, 1)]);

        game.Do(GameCommand.Defeat("brute-1"));

        game.State.Campaign.Items.Should().Equal("hb:item/crown");
    }

    [Fact]
    public void Defeat_ItemLoot_LeavesTheDiceRollToTheTable()
    {
        var game = Game([new LootCard("item", null, 1)]);

        game.Do(GameCommand.Defeat("brute-1"));

        game.State.Log.Should().Contain(l => l.Code == "lootItem" && l.Args!["lootLevel"] == "common");
    }

    [Fact]
    public void Defeat_Avarice_DrawsExtraCards()
    {
        var game = Game([new LootCard("gold", 5, 3)], tags: ["AVARICE 1"]);

        game.Do(GameCommand.Defeat("brute-1"));

        game.Encounter.LootGold.Should().Be(10);
    }

    [Fact]
    public void Defeat_Gilded_GivesItsMonsterLootWithoutDrawing()
    {
        var game = Game([new LootCard("gold", 5, 3)], tags: ["GILDED"]);

        game.Do(GameCommand.Defeat("brute-1"));

        game.State.Campaign.Items.Should().Equal("hb:item/brute-tooth");
        game.Encounter.LootDeck.Should().HaveCount(3);
    }

    private static EngineDriver Game(LootCard[] deck, string[]? tags = null, string[]? monsterLoot = null, bool restoreOnFirstDefeat = false)
    {
        var catalog = new Catalog(
        [
            new Pack
            {
                Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
                Combatants =
                [
                    new Combatant
                    {
                        Id = "hb:combatant/brute-1-3-common",
                        Family = "brute",
                        Kind = "intelligent",
                        LootLevel = "common",
                        Activation = "1-3",
                        Name = En("Brute"),
                        Stats = new CombatantStats { Health = new StatValue(10) },
                        InitiativeCards = ["hb:initiative/brute"],
                        Tags = tags ?? [],
                        MonsterLoot = monsterLoot ?? ["hb:item/brute-tooth"],
                    },
                ],
                Items =
                [
                    new Item { Id = "hb:item/brute-tooth", Name = En("Brute Tooth"), Type = "relic", Tier = "monster", MonsterOf = "brute" },
                    new Item { Id = "hb:item/crown", Name = En("Crown"), Type = "accessory", Tier = "common", Unique = true },
                    new Item { Id = "hb:item/sceptre", Name = En("Sceptre"), Type = "weapon", Tier = "rare", Unique = true },
                ],
                Rules = new PackRules { LootDecks = [new LootDeck { LootLevel = "common", Cards = deck }] },
            },
        ]);
        var scenario = new Scenario
        {
            Id = "hb:scenario/loot",
            Title = En("Loot"),
            Start = "fight",
            Nodes =
            [
                new Node
                {
                    Id = "fight",
                    Type = Node.EncounterType,
                    LootLevel = "common",
                    Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute" }, new CombatantPlacement { Family = "brute" }] },
                    Events = restoreOnFirstDefeat
                        ? [new ScenarioEvent
                        {
                            Id = "rest",
                            Trigger = new EventTrigger { On = "combatantDefeated", Instance = "brute-1" },
                            Actions = [new ScenarioAction { Do = "restore" }],
                        }]
                        : [],
                    EndConditions = [new EndCondition { Id = "lose-all", Kind = "lose", Next = "@end" }],
                },
            ],
        };
        return new EngineDriver(scenario, catalog);
    }

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
