using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>
/// The adventurers' Health and Stamina (rulebook p. 14, 18, 25, 37, 69): Damage kept from one encounter to the next,
/// Defeat, the Prepare Phase, SP gained at each Refresh Phase, Restore.
/// </summary>
public class AdventurerHealthTests
{
    private const string Plate = "hb:item/plate";

    [Fact]
    public void Stats_ComeFromTheAdventurerCard_AndTheHealthOfTheEquippedCards()
    {
        var game = Game();

        Rook(game).Health.Should().Be(14);
        Rook(game).StaminaRecovery.Should().Be(3);
        Rook(game).StaminaMax.Should().Be(5);
        Member(game, "Damocles").StaminaMax.Should().Be(6);

        game.Do(GameCommand.AddItem(Plate, "Rook", "equipped"));
        Rook(game).Health.Should().Be(16);
    }

    [Fact]
    public void AnUnknownAdventurer_GetsTheUsualCard()
    {
        var game = new EngineDriver(Scenario(), Catalog(), "Stranger");

        Member(game, "Stranger").Should().BeEquivalentTo(new { Health = 12, StaminaRecovery = 3, StaminaMax = 5 });
    }

    [Fact]
    public void PreparePhase_SetsSpToTheRecoveryValue()
    {
        var game = Game();

        Rook(game).Stamina.Should().Be(3);
        game.State.Log.Should().Contain(l => l.Code == "adventurersPrepared");
    }

    [Fact]
    public void Damage_StaysIntoTheNextEncounter_DownToHalfTheMaximumHp()
    {
        var game = Game();

        game.Do(GameCommand.Damage("adventurer:Rook", 10));
        game.Do(GameCommand.Damage("adventurer:Damocles", 3));
        Rook(game).Damage.Should().Be(10);
        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        game.Encounter.Node.Should().Be("second");
        Rook(game).Damage.Should().Be(7, "more than half of 14: down to half");
        Member(game, "Damocles").Damage.Should().Be(3, "no more than half: kept");
    }

    [Fact]
    public void Healing_NeverGoesBelowZero()
    {
        var game = Game();

        game.Do(GameCommand.Damage("adventurer:Rook", 2)).Do(GameCommand.Damage("adventurer:Rook", -5));

        Rook(game).Damage.Should().Be(0);
    }

    [Fact]
    public void ReachingTheMaximumHp_DefeatsTheAdventurer_AndRemovesTheirInitiativeCard()
    {
        var game = Game();
        game.Do(GameCommand.SetEffect("adventurer:Rook", "darkness", true));

        game.Do(GameCommand.Damage("adventurer:Rook", 20));

        Rook(game).Should().BeEquivalentTo(new { Damage = 14, Defeated = true, Stamina = 0 });
        game.Encounter.InitiativeTrack.Should().NotContain("adventurer:Rook");
        game.Encounter.Adventurers.Single(a => a.Name == "Rook").Should().BeEquivalentTo(new { Position = (Cell?)null, Effects = Array.Empty<string>(), Defeated = true });
        game.State.Log.Should().Contain(l => l.Code == "adventurerDefeated" && l.Args!["adventurer"] == "Rook");
        GameEngine.Apply(game.State, GameCommand.Damage("adventurer:Rook", 1), game.Context).Error!.Code.Should().Be("Encounters.AdventurerDefeated");
    }

    [Fact]
    public void ADefeatedAdventurer_ComesBackAtTheNextEncounter_WithHalfTheirHp()
    {
        var game = Game();

        game.Do(GameCommand.Defeat("adventurer:Rook"));
        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        Rook(game).Should().BeEquivalentTo(new { Damage = 7, Defeated = false, Stamina = 3 });
        game.Encounter.InitiativeTrack.Should().Contain("adventurer:Rook");
    }

    [Fact]
    public void EveryAdventurerDefeated_LosesTheEncounter()
    {
        var game = Game();

        game.Do(GameCommand.Defeat("adventurer:Rook")).Do(GameCommand.Defeat("adventurer:Damocles"));

        game.State.Log.Should().Contain(l => l.Code == "partyDefeated");
        game.State.Log.Should().Contain(l => l.Code == "encounterEnd" && l.Args!["endCondition"] == "lose");
    }

    [Fact]
    public void RefreshPhase_GivesNoSpOnTheFirstTurn_ThenTheRecoveryValueUpToTheMaximum()
    {
        var game = Game();

        game.Do(GameCommand.SetStamina("adventurer:Rook", 1));
        game.PlayRound();
        Rook(game).Stamina.Should().Be(1, "no SP on the 1st turn of an encounter");

        game.PlayRound();
        Rook(game).Stamina.Should().Be(4);
        game.PlayRound();
        Rook(game).Stamina.Should().Be(5, "never more than the bar's dots");
    }

    [Fact]
    public void SetStamina_IsKeptWithinTheBar()
    {
        var game = Game();

        game.Do(GameCommand.SetStamina("adventurer:Damocles", 9));

        Member(game, "Damocles").Stamina.Should().Be(6);
    }

    [Fact]
    public void Restore_RemovesEveryDamageToken()
    {
        var game = Game();
        game.Do(GameCommand.Damage("adventurer:Rook", 5)).Do(GameCommand.ReachEndCondition("win")).Continue();
        Rook(game).Damage.Should().Be(5);

        game.Do(GameCommand.ReachEndCondition("rest"));

        Rook(game).Damage.Should().Be(0);
        game.State.Log.Should().Contain(l => l.Code == "restoreAdventurers");
    }

    [Fact]
    public void BetweenEncounters_TheStoryMayStillHurtOrHeal()
    {
        var game = Game();
        game.Do(GameCommand.ReachEndCondition("win"));
        game.State.Encounter!.Ended.Should().BeTrue();

        game.Do(GameCommand.Damage("adventurer:Rook", 4)).Do(GameCommand.SetStamina("adventurer:Rook", 2));

        Rook(game).Should().BeEquivalentTo(new { Damage = 4, Stamina = 2 });
    }

    [Fact]
    public void TheScenariosDefeatAdventurer_DefeatsTheNamedAdventurer()
    {
        var game = Game();
        game.State.PendingActions.Add(new ScenarioAction { Do = "defeatAdventurer", Who = System.Text.Json.JsonSerializer.SerializeToElement("Damocles") });

        game.Do(GameCommand.SetEffect("adventurer:Rook", "darkness", true));

        Member(game, "Damocles").Defeated.Should().BeTrue();
        game.Encounter.InitiativeTrack.Should().NotContain("adventurer:Damocles");
    }

    [Fact]
    public void Replay_RebuildsTheSameHealth()
    {
        var game = Game();
        game.Do(GameCommand.Damage("adventurer:Rook", 6)).Do(GameCommand.SetStamina("adventurer:Rook", 1));

        var replayed = GameEngine.Replay(game.Context, game.Party, game.History).Value;

        replayed.Campaign.Party.Single(a => a.Name == "Rook").Should().BeEquivalentTo(Rook(game));
    }

    private static AdventurerState Rook(EngineDriver game) => Member(game, "Rook");

    private static AdventurerState Member(EngineDriver game, string name) => game.State.Campaign.Party.Single(a => a.Name == name);

    private static EngineDriver Game() => new(Scenario(), Catalog(), "Rook", "Damocles");

    private static Catalog Catalog() => new(
    [
        new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
            Adventurers =
            [
                new Adventurer { Id = "hb:adventurer/rook", Name = En("Rook"), Health = 14 },
                new Adventurer { Id = "hb:adventurer/damocles", Name = En("Damocles"), Stamina = new AdventurerStamina(3, 6) },
            ],
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
            Items = [new Item { Id = Plate, Name = En("Plate"), Type = "armor", Tier = "common", Stats = new ItemStats { Health = 2 } }],
        },
    ]);

    private static Scenario Scenario()
    {
        static Node Fight(string id, params EndCondition[] ends) => new()
        {
            Id = id,
            Type = Node.EncounterType,
            LootLevel = "common",
            Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute", At = new Cell(3, 3) }] },
            EndConditions = [new EndCondition { Id = "lose", Kind = "lose", Next = "@end" }, .. ends],
        };

        return new Scenario
        {
            Id = "hb:scenario/health",
            Title = En("Health"),
            Start = "first",
            Nodes =
            [
                Fight("first", new EndCondition { Id = "win", Kind = "win", Next = "second" }),
                Fight("second", new EndCondition { Id = "rest", Kind = "win", Mechanics = [new ScenarioAction { Do = "restore" }], Next = "@end" }),
            ],
        };
    }

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
