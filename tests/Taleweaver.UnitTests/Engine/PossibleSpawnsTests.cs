using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Possible Spawns, Alternate and Variation cards (rulebook ch. 9, p. 79-82) on a small homebrew pack.</summary>
public class PossibleSpawnsTests
{
    private const string Lair = "hb:scenario/lair";

    [Fact]
    public void Setup_NeverConsidersPossibleSpawns()
    {
        var game = Game(setupBrutes: 3, attempted: true);

        game.Encounter.Combatants.Select(c => (c.Instance, c.Card)).Should().Equal(
            ("brute-1", "hb:combatant/brute-1-2"), ("brute-2", "hb:combatant/brute-1-2"), ("brute-3", "hb:combatant/brute-3-4"));
        game.Codes.Should().NotContain("possibleSpawn");
    }

    [Fact]
    public void Spawn_CardInUseWithAFreeNumber_CompletesItsGroupWithoutDrawing()
    {
        var game = Game(setupBrutes: 1, attempted: true);

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Combatant("brute-2").Card.Should().Be("hb:combatant/brute-1-2");
        game.Codes.Should().NotContain("possibleSpawn");
    }

    [Fact]
    public void Spawn_NewCardRequiredAndBaseGroupsInUse_DrawsTheAlternate()
    {
        var game = Game(setupBrutes: 4, attempted: true);

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Combatant("ogre-1").Card.Should().Be("hb:combatant/ogre");
        game.Encounter.InitiativeTrack.Should().Contain("hb:initiative/ogre");
        game.State.Log.Should().Contain(l => l.Code == "possibleSpawn" && l.Args!["card"] == "hb:combatant/ogre");
    }

    [Fact]
    public void Spawn_PrerequisiteNotMet_LeavesTheAlternateOut()
    {
        var game = Game(setupBrutes: 4, attempted: false);

        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Encounter.Combatants.Should().HaveCount(4);
        game.Codes.Should().Contain("spawnImpossible");
    }

    [Fact]
    public void Spawn_VariationDrawn_CompletesItsGroupWithTheBaseInitiativeCard()
    {
        var game = Enumerable.Range(0, 40)
            .Select(seed => Game(setupBrutes: 0, attempted: true, totemSpawns: 2, seed: seed).Do(GameCommand.ActivateToken("totem:red")))
            .First(g => g.Encounter.Combatants[0].Card == "hb:combatant/brute-rager-1-2");

        game.Encounter.Combatants.Select(c => (c.Instance, c.Number, c.Card)).Should().Equal(
            ("brute-rager-1", 1, "hb:combatant/brute-rager-1-2"), ("brute-rager-2", 2, "hb:combatant/brute-rager-1-2"));
        game.Combatant("brute-rager-1").InitiativeCards.Should().Equal("hb:initiative/brute-1-2");
    }

    [Fact]
    public void Spawn_AlternateWithOneFigure_DrawsAgainForTheNextFigure()
    {
        var game = Enumerable.Range(0, 40)
            .Select(seed => Game(setupBrutes: 0, attempted: true, totemSpawns: 2, seed: seed).Do(GameCommand.ActivateToken("totem:red")))
            .First(g => g.Encounter.Combatants[0].Card == "hb:combatant/ogre");

        game.Encounter.Combatants[1].Card.Should().BeOneOf("hb:combatant/brute-1-2", "hb:combatant/brute-rager-1-2");
        game.Encounter.Combatants[1].Number.Should().Be(1);
    }

    [Fact]
    public void Spawn_EveryPossibleSpawnDrawnOverManySeeds_DrawsEachCandidate()
    {
        var cards = Enumerable.Range(0, 40).Select(seed =>
        {
            var game = Game(setupBrutes: 0, attempted: true, seed: seed);
            game.Do(GameCommand.ActivateToken("totem:red"));
            return game.Encounter.Combatants[0].Card;
        }).ToHashSet();

        cards.Should().BeEquivalentTo(["hb:combatant/brute-1-2", "hb:combatant/brute-rager-1-2", "hb:combatant/ogre"]);
    }

    [Fact]
    public void DefeatVariation_MonsterLoot_ComesFromTheBaseCombatant()
    {
        var game = Game(setupBrutes: 4, attempted: true, lootDeck: [new LootCard("monster", null, 1)]);
        game.Do(GameCommand.ActivateToken("totem:red"));

        game.Do(GameCommand.Defeat("ogre-1"));

        game.State.Campaign.Items.Should().Equal("hb:item/brute-tooth");
    }

    [Fact]
    public void Finish_Scenario_IsRecordedAsAttempted()
    {
        var game = Game(setupBrutes: 1, attempted: false);

        game.Do(GameCommand.ReachEndCondition("lose-all")).Continue();

        game.State.Campaign.AttemptedScenarios.Should().Equal(Lair);
    }

    private static EngineDriver Game(int setupBrutes, bool attempted, int totemSpawns = 1, int seed = 7, LootCard[]? lootDeck = null)
    {
        var catalog = new Catalog(
        [
            new Pack
            {
                Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
                Combatants =
                [
                    Card("brute-1-2", "brute", "1-2", ["hb:initiative/brute-1-2"]) with { MonsterLoot = ["hb:item/brute-tooth"] },
                    Card("brute-3-4", "brute", "3-4", ["hb:initiative/brute-3-4"]) with { MonsterLoot = ["hb:item/brute-tooth"] },
                    Card("brute-rager-1-2", "brute-rager", "1-2", []) with
                    {
                        SpawnRule = new SpawnRule { BaseFamily = "brute", VariationOf = "brute-1-2", Requires = [new SpawnPrerequisite { Attempted = Lair }] },
                    },
                    Card("ogre", "ogre", "1", ["hb:initiative/ogre"]) with
                    {
                        SpawnRule = new SpawnRule { BaseFamily = "brute", Requires = [new SpawnPrerequisite { Attempted = Lair }, new SpawnPrerequisite { OncePerEncounter = true }] },
                    },
                ],
                Items = [new Item { Id = "hb:item/brute-tooth", Name = En("Brute Tooth"), Type = "relic", Tier = "monster", MonsterOf = "brute" }],
                Rules = new PackRules { LootDecks = [new LootDeck { LootLevel = "common", Cards = lootDeck ?? [new LootCard("gold", 5, 10)] }] },
            },
        ]);
        var scenario = new Scenario
        {
            Id = Lair,
            Title = En("Lair"),
            Start = "fight",
            Nodes =
            [
                new Node
                {
                    Id = "fight",
                    Type = Node.EncounterType,
                    LootLevel = "common",
                    Setup = new Setup { Combatants = Enumerable.Range(0, setupBrutes).Select(_ => new CombatantPlacement { Family = "brute" }).ToList() },
                    Tokens = new Dictionary<string, TokenRule> { ["totem:red"] = new() { Events = ["totem"] } },
                    Events =
                    [
                        new ScenarioEvent
                        {
                            Id = "totem",
                            Trigger = new EventTrigger { On = "tokenActivated", Token = ["totem:red"] },
                            Actions = [new ScenarioAction { Do = "spawn", Family = "brute", Count = totemSpawns }],
                        },
                    ],
                    EndConditions = [new EndCondition { Id = "lose-all", Kind = "lose", Next = "@end" }],
                },
            ],
        };
        var context = new GameContext(scenario, catalog, new SeededRandomSource(seed));
        var driver = new EngineDriver(scenario, catalog, context);
        if (attempted)
        {
            driver.State.Campaign.AttemptedScenarios.Add(Lair);
        }

        return driver;
    }

    private static Combatant Card(string name, string family, string activation, string[] initiative) => new()
    {
        Id = $"hb:combatant/{name}",
        Family = family,
        Kind = "intelligent",
        LootLevel = "common",
        Activation = activation,
        Name = En(family),
        Stats = new CombatantStats { Health = new StatValue(10) },
        InitiativeCards = initiative,
        AiSteps = [],
    };

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
