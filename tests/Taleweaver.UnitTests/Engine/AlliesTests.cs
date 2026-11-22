using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Command Combatants (rulebook p. 58), Summoning and Conduit Summoning (p. 75-77), Scaling Icons (p. 72).</summary>
public class AlliesTests
{
    private const string Esper = "hb:combatant/esper";
    private const string OtherEsper = "hb:combatant/other-esper";
    private const string Spirit = "hb:combatant/spirit";
    private const string Wolf = "hb:combatant/wolf";
    private const string Conduit = "hb:combatant/brute-conduit";
    private const string EsperCard = "ally:hb:initiative/esper";
    private const string EsperFigure = "ally:esper";

    [Fact]
    public void AStoryAlly_IsNoOpponent()
    {
        var game = Game(setup: [new CombatantPlacement { Ref = Wolf, Instance = "wolf", At = new Cell(1, 1) }]);

        game.Encounter.Combatants.Select(c => c.Family).Should().Equal("brute");
        game.Encounter.Allies.Single().Should().BeEquivalentTo(new { Id = "wolf", Health = 9, Stamina = 2, StaminaMax = 4, Position = new Cell(1, 1) });
        game.Encounter.InitiativeTrack.Should().Contain("ally:hb:initiative/wolf");
    }

    [Fact]
    public void AnAllyDefeated_DropsNoLoot_AndDoesNotStopUrgency()
    {
        var game = Game(setup: [new CombatantPlacement { Ref = Wolf, Instance = "wolf" }]);
        game.Do(GameCommand.NextTurn());

        game.Do(GameCommand.Damage("ally:wolf", 20));

        game.Encounter.RoundLog.Defeated.Should().Be(0);
        game.Encounter.InitiativeTrack.Should().NotContain("ally:hb:initiative/wolf");
        game.Codes.Should().Contain("allyDefeated").And.NotContain("defeated");
    }

    [Fact]
    public void AStoryAlly_ComesBackHealedToHalf_TheNextEncounter()
    {
        var game = Game(setup: [new CombatantPlacement { Ref = Wolf, Instance = "wolf" }]);

        game.Do(GameCommand.Defeat("ally:wolf")).Do(GameCommand.ReachEndCondition("win")).Continue();

        game.Encounter.Allies.Single().Should().BeEquivalentTo(new { Id = "wolf", Damage = 5, Defeated = false });
    }

    [Fact]
    public void Summoning_BringsTheEsper_ScaledOnItsSummoner_WithAToken()
    {
        var game = Game();
        game.Do(GameCommand.NextTurn());
        var playing = game.Encounter.CurrentTurn;

        game.Do(GameCommand.Summon("Rook", Esper));

        game.Encounter.Allies.Single().Should().BeEquivalentTo(new { Owner = "Rook", Health = 14, Force = 8, Damage = 0 });
        game.Encounter.InitiativeTrack[playing + 1].Should().Be(EsperCard, "as if just Spawned");
        game.Encounter.RoundLog.Spawned.Should().Be(0);
        Rook(game).SummonTokens.Should().Be(1);
    }

    [Fact]
    public void Summoning_IsOnePerAdventurer()
    {
        var game = Game();
        game.Do(GameCommand.Summon("Rook", Esper));

        Refused(game, GameCommand.Summon("Rook", OtherEsper)).Should().Be("Allies.OneAtATime");
    }

    [Fact]
    public void Summoning_IsOncePerEncounter_EvenAfterADismissal()
    {
        var game = Game();
        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.DismissAlly(EsperFigure));

        Refused(game, GameCommand.Summon("Rook", OtherEsper)).Should().Be("Allies.UsedThisEncounter");
    }

    [Fact]
    public void Summoning_NeedsItsDiscipline()
    {
        Refused(Game(), GameCommand.Summon("Damocles", Esper)).Should().Be("Allies.DisciplineMissing");
    }

    [Fact]
    public void AnEspersDamageFromAnOpponent_IsItsSummonersToo()
    {
        var game = Game();

        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.Damage(EsperFigure, 5));

        game.Encounter.Allies.Single().Damage.Should().Be(5);
        Rook(game).Damage.Should().Be(5);
    }

    [Fact]
    public void AnEspersOtherDamage_IsItsOwnOnly()
    {
        var game = Game();

        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.Damage(EsperFigure, 3) with { FromOpponent = false });

        Rook(game).Damage.Should().Be(0);
    }

    [Fact]
    public void ASummonToken_TakesTwoDamageOff()
    {
        var game = Game();

        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.Damage(EsperFigure, 5) with { Tokens = 1 });

        Rook(game).Should().BeEquivalentTo(new { Damage = 3, SummonTokens = 0 });
    }

    [Fact]
    public void DismissedInsteadOfTheDamage_CountsAsDefeated()
    {
        var game = Game();

        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.DismissAlly(EsperFigure, ignoreDamage: true));

        Rook(game).Damage.Should().Be(0);
        game.Codes.Should().Contain("allyDefeated");
    }

    [Fact]
    public void AnAlly_GainsSpFromItsSecondTurn()
    {
        var game = Game(setup: [new CombatantPlacement { Ref = Wolf, Instance = "wolf" }]);
        game.PlayRound();
        game.Encounter.Allies.Single().Stamina.Should().Be(2);

        game.PlayRound();

        game.Encounter.Allies.Single().Stamina.Should().Be(4);
    }

    [Fact]
    public void AnEsper_StaysIntoTheNextEncounter_WithoutHealing()
    {
        var game = Game();
        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.Damage(EsperFigure, 10) with { FromOpponent = false });

        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        game.Encounter.Node.Should().Be("second");
        game.Encounter.Allies.Single().Should().BeEquivalentTo(new { Id = "esper", Damage = 10, Position = (Cell?)null });
        game.Encounter.InitiativeTrack.Should().Contain(EsperCard);
        Refused(game, GameCommand.Summon("Rook", OtherEsper)).Should().Be("Allies.OneAtATime");
    }

    [Fact]
    public void Restore_SendsTheEspersBackToThePile()
    {
        var game = Game(restoreAfterFirst: true);
        game.Do(GameCommand.Summon("Rook", Esper));

        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        game.Encounter.Allies.Should().BeEmpty();
        game.Encounter.AllyOptions.Single(o => o.Kind == "summoning").Cards.Should().Contain(Esper);
        Rook(game).SummonTokens.Should().Be(0);
    }

    [Fact]
    public void FelledAfterlife_BringsTwoSpirits_OnOneCard()
    {
        var game = Game();

        game.Do(GameCommand.Summon("Shayliss", Spirit));

        game.Encounter.Allies.Select(a => a.Id).Should().Equal("enslaved-spirit", "enslaved-spirit-2");
        game.Encounter.InitiativeTrack.Count(c => c.StartsWith("ally:", StringComparison.Ordinal)).Should().Be(1);

        game.Do(GameCommand.Defeat("ally:enslaved-spirit"));

        game.Encounter.InitiativeTrack.Should().Contain("ally:hb:initiative/spirit", "the other still plays");
    }

    [Fact]
    public void AConduit_IsEarnedFromADefeatedOpponent_ThenTakenFor1Sp()
    {
        var game = Game();
        Refused(game, GameCommand.EarnConduit("Rook", Conduit)).Should().Be("Allies.NotACommandCard");

        game.Do(GameCommand.Defeat("brute-1")).Do(GameCommand.EarnConduit("Rook", Conduit)).Do(GameCommand.ConduitForm("Rook", Conduit));

        Rook(game).Should().BeEquivalentTo(new { ConduitForm = Conduit, Stamina = 2, StaminaRecovery = 2, StaminaMax = 4, Health = 14 });

        game.Do(GameCommand.ConduitForm("Rook", null));

        Rook(game).Should().BeEquivalentTo(new { ConduitForm = (string?)null, Stamina = 1, StaminaMax = 5 });
    }

    [Fact]
    public void Allies_ReplayTheSame()
    {
        var game = Game();
        game.Do(GameCommand.Summon("Rook", Esper)).Do(GameCommand.Damage(EsperFigure, 2)).PlayRound();
        game.Do(GameCommand.ReachEndCondition("win")).Continue();

        GameEngine.Replay(game.Context, game.Party, game.History).Value.Should().BeEquivalentTo(game.State);
    }

    private static AdventurerState Rook(EngineDriver game) => game.State.Campaign.Party.Single(a => a.Name == "Rook");

    private static string? Refused(EngineDriver game, GameCommand command) => GameEngine.Apply(game.State, command, game.Context).Error?.Code;

    /// <summary>Rook knows Summoning and Conduit Summoning, Shayliss Felled Afterlife, Damocles nothing.</summary>
    private static EngineDriver Game(IReadOnlyList<CombatantPlacement>? setup = null, bool restoreAfterFirst = false)
    {
        var start = new Node { Id = "camp", Type = "scene", Next = "first" };
        Node Fight(string id, string next) => new()
        {
            Id = id,
            Type = Node.EncounterType,
            LootLevel = "common",
            Setup = new Setup { Combatants = [new CombatantPlacement { Family = "brute" }, .. setup ?? []] },
            EndConditions =
            [
                new EndCondition { Id = "win", Kind = "win", Next = next, Mechanics = restoreAfterFirst ? [new ScenarioAction { Do = "restore" }] : [] },
            ],
        };
        var scenario = new Scenario
        {
            Id = "hb:scenario/allies",
            Title = En("Allies"),
            Party = new Party { StartingXp = 20 },
            Start = "camp",
            Nodes = [start, Fight("first", "second"), Fight("second", "@end")],
        };
        var game = new EngineDriver(scenario, Catalog(), "Rook", "Shayliss", "Damocles");
        game.Do(GameCommand.LearnDiscipline("Rook", "hb:discipline/summoning", 0))
            .Do(GameCommand.LearnDiscipline("Rook", "hb:discipline/conduit-summoning", 0))
            .Do(GameCommand.LearnDiscipline("Shayliss", "hb:discipline/felled-afterlife", 0));
        return game.Continue();
    }

    private static Catalog Catalog()
    {
        Combatant Card(string id, string family, string kind, int? health, params string[] tags) => new()
        {
            Id = id,
            Family = family,
            Name = En(family),
            Kind = kind,
            LootLevel = "any",
            Tags = tags,
            Stats = new CombatantStats { Health = new StatValue(health) },
            Stamina = "2 rouges / 2 gris",
            InitiativeCards = kind == "command" ? ["hb:initiative/" + id.Split('/')[1]] : ["hb:initiative/brute"],
        };

        return new Catalog([new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = En("hb") },
            Adventurers = [new Adventurer { Id = "hb:adventurer/rook", Name = En("Rook"), Health = 14 }],
            Combatants =
            [
                Card("hb:combatant/brute", "brute", "intelligent", 10),
                Card(Esper, "esper", "command", null, "LOYAL ESPER"),
                Card(OtherEsper, "other-esper", "command", null, "LOYAL ESPER"),
                Card(Spirit, "enslaved-spirit", "command", 1),
                Card(Wolf, "wolf", "command", 9),
                Card(Conduit, "brute", "command", null, "CONDUIT"),
            ],
            Disciplines =
            [
                new Discipline { Id = "hb:discipline/summoning", Name = En("Summoning"), Tree = "a", Level = 1, Cost = 1 },
                new Discipline { Id = "hb:discipline/conduit-summoning", Name = En("Conduit Summoning"), Tree = "a", Level = 2, Cost = 1 },
                new Discipline { Id = "hb:discipline/felled-afterlife", Name = En("Felled Afterlife"), Tree = "b", Level = 1, Cost = 1 },
            ],
        }]);
    }

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
