using System.Text.Json;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Story Rounds (rulebook p. 13) and the party between encounters (p. 10-11), on a small homebrew scenario.</summary>
public class StoryRoundTests
{
    private const string Sword = "hb:item/sword";
    private static readonly string[] Uniques = ["hb:item/crown", "hb:item/ring"];

    [Fact]
    public void EnteringAStoryRound_RunsItsMechanics_DrawsTheStoreUnique_AndOpensShopAndTrain()
    {
        var game = Game();

        game.State.Prompt!.Kind.Should().Be(PromptKinds.StoryRound);
        game.State.Prompt.Options.Select(o => o.Id).Should().Equal("explore");
        game.State.Campaign.StoryRound.Should().BeEquivalentTo(new { Node = "town", Phase = StoryRoundPhases.Shop });
        game.State.Campaign.Gold.Should().Be(100);
        game.State.Campaign.Party.Select(a => a.Name).Should().Equal("Nightingale", "Rook", "Zeke", "Remi");
        game.State.Campaign.StoreUniques["town"].Should().ContainSingle().Which.Should().BeOneOf(Uniques);
    }

    [Fact]
    public void Explore_OffersTheSideQuestsAndTheWaysForth()
    {
        var game = Game().Answer("explore");

        game.State.Campaign.StoryRound!.Phase.Should().Be(StoryRoundPhases.Explore);
        game.State.Prompt!.Options.Select(o => o.Id).Should().Equal("quest:rats", "venture:0");
    }

    [Fact]
    public void ASideQuest_ComesBackToShopAndTrain_WithTheSameUnique_AndCannotBeChosenAgain()
    {
        var game = Game();
        var unique = game.State.Campaign.StoreUniques["town"].Single();

        game.Answer("explore").Answer("quest:rats");
        game.State.Campaign.CurrentNode.Should().Be("rats");
        game.State.Campaign.StoryRound!.Phase.Should().Be(StoryRoundPhases.SideQuest);
        game.Continue();

        game.State.Campaign.CurrentNode.Should().Be("town");
        game.State.Status.Should().Be(GameStatus.InProgress);
        game.State.Campaign.StoryRound!.Phase.Should().Be(StoryRoundPhases.Shop);
        game.State.Campaign.Gold.Should().Be(120, "the Side Quest's reward, but not the Story Round's again");
        game.State.Campaign.StoreUniques["town"].Should().Equal(unique);
        game.Answer("explore").State.Prompt!.Options.Select(o => o.Id).Should().Equal("venture:0");
    }

    [Fact]
    public void VentureForth_LeavesTheStoryRoundForGood()
    {
        var game = Game().Answer("explore").Answer("venture:0");

        game.State.Campaign.StoryRound.Should().BeNull();
        game.State.Campaign.StoryRoundsLeft.Should().Equal("town");
        game.State.Encounter!.Node.Should().Be("fight");
    }

    [Fact]
    public void TheEncounter_IsPlayedByTheActiveGroup_WithoutTheInjured()
    {
        var game = Game();

        game.Do(GameCommand.SetActiveGroup("Nightingale", "Rook", "Zeke"));
        game.Do(GameCommand.SetAdventurerStatus("Zeke", "injured", true));
        game.Answer("explore").Answer("venture:0");

        game.Encounter.InitiativeTrack.Where(c => c.StartsWith("adventurer:", StringComparison.Ordinal))
            .Should().Equal("adventurer:Nightingale", "adventurer:Rook");
        game.Encounter.Adventurers.Select(a => a.Name).Should().Equal("Nightingale", "Rook");
    }

    [Fact]
    public void TheActiveGroup_RefusesTheInjured_MoreThanFour_AndChangesDuringAnEncounter()
    {
        var game = Game();
        game.Do(GameCommand.SetAdventurerStatus("Zeke", "injured", true));

        Refused(game, GameCommand.SetActiveGroup("Nightingale", "Zeke")).Should().Be("Party.NotSelectable");
        Refused(game, GameCommand.SetActiveGroup("Nightingale", "Rook", "Remi", "Zeke", "Nightingale")).Should().Be("Party.TooLarge");
        Refused(game, GameCommand.SetActiveGroup("Yun")).Should().Be("Party.UnknownAdventurer");

        game.Answer("explore").Answer("venture:0");
        Refused(game, GameCommand.SetActiveGroup("Nightingale")).Should().Be("Games.InEncounter");
    }

    [Fact]
    public void LeavingTheParty_PutsTheirItemsInTheStash()
    {
        var game = Game();
        game.Do(GameCommand.AddItem(Sword, "Rook", "equipped"));

        game.Answer("explore").Answer("quest:rats");

        game.State.Campaign.Party.Select(a => a.Name).Should().NotContain("Rook");
        game.State.Campaign.Stash.Select(i => i.Item).Should().Equal(Sword);
    }

    [Fact]
    public void Replay_RebuildsTheSameStoryRound()
    {
        var game = Game();
        game.Do(GameCommand.SetAdventurerStatus("Zeke", "unselectable", true)).Answer("explore").Answer("quest:rats").Continue();

        var replayed = GameEngine.Replay(game.Context, game.Party, game.History).Value;

        replayed.Should().BeEquivalentTo(game.State);
    }

    [Fact]
    public void Checker_RequiresAWayForth()
    {
        var scenario = Scenario() with { Nodes = [new Node { Id = "town", Type = Node.StoryRoundType }] };

        ScenarioChecker.Check(scenario, Catalog()).Errors.Select(e => e.Code).Should().Contain("Scenarios.StoryRoundWithoutExit");
    }

    private static string Refused(EngineDriver game, GameCommand command)
    {
        var result = GameEngine.Apply(game.State, command, game.Context);
        result.IsSuccess.Should().BeFalse();
        return result.Error!.Code;
    }

    private static EngineDriver Game() => new(Scenario(), Catalog(), "Nightingale", "Rook", "Zeke");

    private static Catalog Catalog() => new(
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
                },
            ],
            Items =
            [
                new Item { Id = Sword, Name = En("Sword"), Type = "weapon", Tier = "common", Cost = 40 },
                new Item { Id = Uniques[0], Name = En("Crown"), Type = "relic", Tier = "common", Unique = true, Cost = 90 },
                new Item { Id = Uniques[1], Name = En("Ring"), Type = "accessory", Tier = "common", Unique = true, Cost = 70 },
            ],
        },
    ]);

    private static Scenario Scenario() => new()
    {
        Id = "hb:scenario/story-round",
        Title = En("Story Round"),
        Start = "town",
        Nodes =
        [
            new Node
            {
                Id = "town",
                Type = Node.StoryRoundType,
                Name = En("The Town"),
                Narrative = En("Market day."),
                Mechanics =
                [
                    new ScenarioAction { Do = "gainGold", Amount = 100 },
                    new ScenarioAction { Do = "joinParty", Extra = Extra(("adventurer", "Remi")) },
                ],
                Store = new Store { Items = [new StoreFilter { Tiers = ["common"] }], Unique = new StoreUnique("common") },
                SideQuests = [new StoryExit { Id = "rats", Node = "rats", Label = En("Rats in the cellar") }],
                VentureForth = [new StoryExit { Node = "fight", Label = En("Main Quest") }],
            },
            new Node
            {
                Id = "rats",
                Type = "scene",
                Narrative = En("Rook stays behind."),
                Mechanics =
                [
                    new ScenarioAction { Do = "gainGold", Amount = 20 },
                    new ScenarioAction { Do = "leaveParty", Extra = Extra(("adventurer", "Rook")) },
                ],
                Next = "@currentStoryRound",
            },
            new Node
            {
                Id = "fight",
                Type = Node.EncounterType,
                LootLevel = "common",
                Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute" }] },
                EndConditions = [new EndCondition { Id = "lose-all", Kind = "lose", Next = "@end" }],
            },
        ],
    };

    private static Dictionary<string, JsonElement> Extra(params (string Key, string Value)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value));

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
