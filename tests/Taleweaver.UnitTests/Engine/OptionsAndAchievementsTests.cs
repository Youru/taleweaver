using System.Text.Json;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Launch options (rulebook p. 86-88), Achievements (p. 17) and Abraxis Battle Dice (p. 85).</summary>
public class OptionsAndAchievementsTests
{
    private static readonly Catalog Catalog = new(
    [
        new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
            Combatants = [Card("brute-mundane", "brute", "mundane", 6), Card("brute-common", "brute", "common", 9), Card("lord-common", "lord", "common", 20, "BOSS")],
        },
    ]);

    [Fact]
    public void TougherMobs_AddHalfTheHp_NotToABoss()
    {
        var game = Game(new GameOptions { TougherMobs = true });

        game.Encounter.Combatants.Single(c => c.Family == "brute").Health.Should().Be(14, "9 and a half more, rounded up");
        game.Encounter.Combatants.Single(c => c.Family == "lord").Health.Should().BeNull();

        game.Do(GameCommand.Damage("brute-1", 13));
        game.Combatant("brute-1").Defeated.Should().BeFalse();
        game.Do(GameCommand.Damage("brute-1", 1));
        game.Combatant("brute-1").Defeated.Should().BeTrue();
    }

    [Fact]
    public void NoScaling_TakesTheLowestCard()
    {
        Game(new GameOptions { NoScaling = true }).Combatant("brute-1").Card.Should().Be("hb:combatant/brute-mundane");
        Game().Combatant("brute-1").Card.Should().Be("hb:combatant/brute-common");
    }

    [Fact]
    public void NoPlayerElimination_AddsUrgency_ThenTheAdventurerComesBack()
    {
        var game = Game(new GameOptions { NoPlayerElimination = true });

        game.Do(GameCommand.Defeat("adventurer:Rook"));

        game.Encounter.Urgency.Should().Be(1);
        game.Encounter.InitiativeTrack.Should().Contain("adventurer:Rook");
        game.PlayRound();
        var rook = game.State.Campaign.Party.Single(a => a.Name == "Rook");
        rook.Defeated.Should().BeFalse();
        rook.Damage.Should().Be(rook.Health / 2);
        GameEngine.Replay(game.Context, game.Party, game.History).Value.Should().BeEquivalentTo(game.State);
    }

    [Fact]
    public void NoPlayerElimination_LosesAtTheUrgencyLimitChosen()
    {
        var game = Game(new GameOptions { NoPlayerElimination = true, UrgencyLimit = 3 });

        game.PlayRound().PlayRound().PlayRound().Do(GameCommand.NextTurn());

        game.Encounter.Ended.Should().BeTrue();
        game.Encounter.Urgency.Should().Be(3);
    }

    [Fact]
    public void RemoveUrgency_NeverAddsAToken()
    {
        var game = Game(new GameOptions { RemoveUrgency = true });

        game.PlayRound().PlayRound().PlayRound().PlayRound().PlayRound().PlayRound();

        game.Encounter.Urgency.Should().Be(0);
    }

    [Fact]
    public void StaticMonsters_KeepTheOrderFromOneRoundToTheNext()
    {
        var game = Game(new GameOptions { StaticMonsters = true }).PlayRound();
        var order = game.Encounter.InitiativeTrack.ToList();

        game.PlayRound().PlayRound();

        game.Encounter.InitiativeTrack.Should().Equal(order);
    }

    [Fact]
    public void AnAchievement_RewardsItsAdventurerOnce()
    {
        var game = Game();

        game.Do(GameCommand.CompleteAchievement("untouched", "Rook"));

        game.State.Campaign.Party.Single(a => a.Name == "Rook").Xp.Should().Be(2);
        game.State.Campaign.Party.Single(a => a.Name == "Nightingale").Xp.Should().Be(0);
        game.State.Campaign.Achievements.Single().Adventurer.Should().Be("Rook");
        game.Codes.Should().Contain(["achievement", "achievementText"]);
        GameEngine.Apply(game.State, GameCommand.CompleteAchievement("untouched", "Nightingale"), game.Context)
            .Error!.Code.Should().Be("Achievements.AlreadyDone");
    }

    [Fact]
    public void AnAchievement_MayBeReportedOnTheEncountersLastPage()
    {
        var game = Game().Do(GameCommand.ReachEndCondition("win"));

        game.Do(GameCommand.CompleteAchievement("untouched", "Rook"));

        game.State.Campaign.Party.Single(a => a.Name == "Rook").Xp.Should().Be(2);
    }

    [Fact]
    public void Abraxis_KeepsThePoints_ThenGivesTheRewardsOfEveryTierReached()
    {
        var game = Game().Do(GameCommand.ReachEndCondition("win")).Continue();
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Abraxis);

        game.Do(GameCommand.AnswerAbraxis([0, 4]));
        game.State.Abraxis!.Players.Select(p => p.Points).Should().Equal(11, 6);
        game.Do(GameCommand.AnswerAbraxis([1, 2], penalty: 1));
        game.State.Abraxis!.Players.Select(p => p.Points).Should().Equal(9, 3);
        game.Do(GameCommand.AnswerAbraxis([1, 3]));
        game.Do(GameCommand.AnswerAbraxis([1]));
        game.State.Abraxis!.Round.Should().Be(5);
        game.Do(GameCommand.AnswerAbraxis([0]));

        game.State.Abraxis.Should().BeNull();
        game.State.Campaign.Gold.Should().Be(25, "one survivor: the first tier only");
        GameEngine.Replay(game.Context, game.Party, game.History).Value.Should().BeEquivalentTo(game.State);
    }

    [Fact]
    public void TheStatus_IsAWord_AndOldSavesWithANumberAreStillRead()
    {
        JsonSerializer.Serialize(GameStatus.Finished, Taleweaver.Api.Common.JsonDefaults.Options).Should().Be("\"Finished\"");
        JsonSerializer.Deserialize<GameStatus>("1", Taleweaver.Api.Common.JsonDefaults.Options).Should().Be(GameStatus.Finished);
    }

    private static EngineDriver Game(GameOptions? options = null)
    {
        var scenario = new Scenario
        {
            Id = "hb:scenario/options",
            Title = En("Options"),
            Start = "fight",
            Nodes =
            [
                new Node
                {
                    Id = "fight",
                    Type = Node.EncounterType,
                    LootLevel = "common",
                    Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute" }, new CombatantPlacement { Family = "lord" }] },
                    Achievements =
                    [
                        new Achievement
                        {
                            Id = "untouched",
                            Name = En("Untouched"),
                            HiddenText = En("Well done."),
                            Actions = [new ScenarioAction { Do = "gainXp", Amount = 2 }],
                        },
                    ],
                    EndConditions = [new EndCondition { Id = "win", Kind = "win", Next = "tavern" }, new EndCondition { Id = "lose", Kind = "lose", Next = "@end" }],
                },
                new Node
                {
                    Id = "tavern",
                    Type = "scene",
                    Next = "@end",
                    Mechanics =
                    [
                        new ScenarioAction
                        {
                            Do = "abraxis",
                            Text = En("The Card"),
                            Rewards = [[new ScenarioAction { Do = "gainGold", Amount = 25 }], [new ScenarioAction { Do = "gainGold", Amount = 100 }]],
                        },
                    ],
                },
            ],
        };
        var context = new GameContext(scenario, Catalog, new SeededRandomSource(7)) { Options = options };
        return new EngineDriver(scenario, Catalog, context);
    }

    private static Combatant Card(string name, string family, string lootLevel, int health, params string[] tags) => new()
    {
        Id = $"hb:combatant/{name}",
        Family = family,
        Kind = "intelligent",
        LootLevel = lootLevel,
        Activation = "1-2",
        Name = En(family),
        Tags = tags,
        Stats = new CombatantStats { Health = new StatValue(health) },
        InitiativeCards = [$"hb:initiative/{family}"],
    };

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
