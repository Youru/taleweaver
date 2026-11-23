using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Blight of Rhamsted played from start to end on the real content, by the GM's commands.</summary>
public class BlightPlaythroughTests
{
    [ContentFact]
    public async Task Play_BlightOfRhamsted_ReachesTheEndWithTheRewards()
    {
        var game = new EngineDriver(await RepositoryContent.BlightAsync(), await RepositoryContent.CatalogAsync(), "Nightingale", "Rook", "Remi", "Zeke");

        // Intro, then Search for the Lair: 3 Fragor, 1 Earth Loa, 2 Water Loa
        game.State.Log[0].Code.Should().Be("scene");
        game.Continue();
        game.Encounter.Node.Should().Be("search-for-the-lair");
        game.Encounter.Combatants.Select(c => c.Instance).Should().Equal("fragor-1", "fragor-2", "fragor-3", "earth-loa-1", "water-loa-1", "water-loa-2");
        game.Combatant("water-loa-1").Card.Should().Be("um1:combatant/crystal-water-loa-1-2-common");
        game.Encounter.InitiativeTrack.Should().HaveCount(4 + 3);
        game.Combatant("fragor-1").Position.Should().Be(new Cell(11, 2));
        game.Encounter.Tokens.Single(t => t.Key == "totem:blue").Area.Should().Be(new Rect(1, 3));
        game.Encounter.Tokens.Single(t => t.Id == "obs-long-1").Area.Should().Be(new Rect(6, 6, 1, 2));

        game.PlayRound("yes");
        game.Encounter.Round.Should().Be(1);
        game.Encounter.Urgency.Should().Be(0);

        // Green totem: the GM says the 3 Fragor cannot spawn → Urgency instead
        game.Do(GameCommand.ActivateToken("totem:green"));
        game.State.Prompt!.Kind.Should().Be(PromptKinds.SpawnPossible);
        game.Answer("no");
        game.Encounter.Urgency.Should().Be(1);

        // Blue totem: Earth Loa on the totem + 2 Gevaudan, who gain a step
        game.Do(GameCommand.ActivateToken("totem:blue"));
        game.Encounter.Combatants.Select(c => c.Instance).Should().Contain(["earth-loa-2", "gevaudan-1", "gevaudan-2"]);
        game.Combatant("earth-loa-2").Position.Should().Be(new Cell(1, 3), "it spawns on the totem");
        game.Combatant("gevaudan-1").Position.Should().BeNull("'any space on the east side' is for the GM to choose");
        game.Encounter.ExtraAiSteps["gevaudan"].Should().ContainSingle();
        game.Encounter.Tokens.Where(t => t.Key == "totem:blue").Should().OnlyContain(t => t.Removed);

        // Loot: 20 Gold, then 2 consumables
        game.Do(GameCommand.ActivateToken("loot:green")).Do(GameCommand.ActivateToken("loot:yellow"));
        game.State.Campaign.Gold.Should().Be(20);

        // Blue objective reveals the exit; reaching it ends the encounter
        game.Do(GameCommand.ActivateToken("objective:blue"));
        game.Encounter.Tokens.Should().Contain(t => t.Key == "exit:blue");
        game.Do(GameCommand.ReachEndCondition("win-exit"));
        game.Continue();

        // Crossing the River: first swim places diagram A (6 Cave Sickles), the next ones only move the figure
        game.Encounter.Node.Should().Be("crossing-the-river");
        game.Do(GameCommand.ActivateToken("objective:red"));
        game.Encounter.PlacedDiagrams.Should().Equal("diagram-a");
        game.Encounter.Combatants.Count(c => c.Family == "cave-sickle").Should().Be(6);
        game.Encounter.InitiativeTrack.Should().Contain(["um1:initiative/cave-sickle-1-3", "um1:initiative/cave-sickle-4-6"]);
        game.Do(GameCommand.ActivateToken("objective:green"));
        game.Encounter.PlacedDiagrams.Should().Equal("diagram-a");
        game.Do(GameCommand.ActivateToken("objective:yellow"));
        game.Encounter.PlacedDiagrams.Should().Equal("diagram-a", "diagram-b");
        game.Do(GameCommand.ReachEndCondition("win-exit"));
        game.Continue();

        // Blight Found: the Terror is played with the Blighted Guardian's figure and initiative card
        game.Encounter.Node.Should().Be("blight-found");
        game.State.Campaign.RevealedHiddenCards.Should().Contain("large:BP1");
        game.Combatant("terror").InitiativeCards.Should().Equal("um1:initiative/blighted-guardian");
        game.PlayRound();

        // Both Animates fall: diagram C can bring them back
        game.Do(GameCommand.Defeat("animate-1")).Do(GameCommand.Defeat("animate-2"));
        game.Encounter.InitiativeTrack.Should().NotContain("um1:initiative/animate");

        // Terror defeated, not in the pit (B): the Brute appears and The Slain adds a win condition
        game.Do(GameCommand.Damage("terror", 60));
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Choice);
        game.Answer("1");
        game.Combatant("brute").Card.Should().Be("um1:combatant/blighted-brute-common");
        game.Encounter.InitiativeTrack.Should().Contain("um1:initiative/blighted-brute").And.NotContain("um1:initiative/blighted-guardian");
        game.Encounter.PlacedDiagrams.Should().Equal("diagram-c");
        game.Encounter.Combatants.Where(c => c.Family == "animate").Should().OnlyContain(c => !c.Defeated).And.HaveCount(2);
        game.Encounter.Tokens.Where(t => t.Key.StartsWith("objective:", StringComparison.Ordinal)).Should().OnlyContain(t => t.Removed);
        game.Encounter.ActiveEndConditions.Should().Contain("win-slain");

        game.Do(GameCommand.Damage("brute", 80));
        game.Continue();

        game.State.Status.Should().Be(GameStatus.Finished);
        game.State.Ending.Should().Be("@currentStoryRound");
        var lootGold = game.State.Log.Where(l => l.Code == "gainGold" && l.Args!.GetValueOrDefault("source") == "loot").Sum(l => int.Parse(l.Args!["amount"], System.Globalization.CultureInfo.InvariantCulture));
        lootGold.Should().BePositive("each Defeated opponent draws a Combatant Loot card");
        game.State.Campaign.Gold.Should().Be(70 + lootGold);
        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 1);
        game.State.Campaign.Items.Should().Contain("um1:item/brute-serum").And.Contain("2 mundane/common/uncommon consumable");
        game.State.Campaign.RevealedHiddenCards.Should().Contain(["small:BP2", "large:BP2", "medium:BP3"]);
        game.Codes.Should().NotContain(["manualAction", "unknownNode", "unknownHiddenRule", "unknownEndCondition", "spawnImpossible"]);
    }

    [ContentFact]
    public async Task Play_TerrorFallsAndSkullIsRolled_WinsWithTheFallen()
    {
        var game = new EngineDriver(await RepositoryContent.BlightAsync(), await RepositoryContent.CatalogAsync());
        game.Continue().Do(GameCommand.ReachEndCondition("win-exit")).Continue()
            .Do(GameCommand.ReachEndCondition("win-exit")).Continue();

        game.Do(GameCommand.Defeat("terror"));
        game.Answer("0");
        game.Answer("0");
        game.Continue();

        game.State.Status.Should().Be(GameStatus.Finished);
        game.State.Log.Should().Contain(l => l.Code == "encounterEnd" && l.Args!["endCondition"] == "win-fallen");
    }

    [ContentFact]
    public async Task Play_TerrorClimbsBackAndAdventurerFailsAgility_AsksDamageThenAdventurer()
    {
        var game = new EngineDriver(await RepositoryContent.BlightAsync(), await RepositoryContent.CatalogAsync());
        game.Continue().Do(GameCommand.ReachEndCondition("win-exit")).Continue()
            .Do(GameCommand.ReachEndCondition("win-exit")).Continue();
        game.PlayRound();

        game.Do(GameCommand.Defeat("terror")).Answer("0").Answer("1");
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Number);
        game.Do(GameCommand.AnswerNumber(12));
        game.Combatant("terror").Should().Match<CombatantState>(c => !c.Defeated && c.Damage == 12);
        game.Encounter.InitiativeTrack.Should().Contain("um1:initiative/blighted-guardian");
        game.Answer("0");
        game.State.Prompt!.Kind.Should().Be(PromptKinds.SkillCheck);
        game.Answer("fail");
        game.State.Prompt!.Options.Select(o => o.Id).Should().Equal("Nightingale", "Rook");
        game.Answer("Rook");

        game.State.Log.Should().Contain(l => l.Code == "adventurerDefeated" && l.Args!["adventurer"] == "Rook");
        game.State.Prompt.Should().BeNull();
    }

    [ContentFact]
    public async Task Replay_HistoryOfAGame_RebuildsTheSameState()
    {
        var game = new EngineDriver(await RepositoryContent.BlightAsync(), await RepositoryContent.CatalogAsync());
        game.Continue().PlayRound("yes").PlayRound().Do(GameCommand.ActivateToken("totem:red"));

        var replayed = GameEngine.Replay(game.Context, game.Party, game.History);

        System.Text.Json.JsonSerializer.Serialize(replayed.Value).Should().Be(System.Text.Json.JsonSerializer.Serialize(game.State));
    }
}
