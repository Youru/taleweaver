using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The pilot of Act 1 (The MAST, scenarios/um1/act1-pilot.json) played through the engine, as a GM would.</summary>
public class Act1PilotPlaythroughTests
{
    private static async Task<EngineDriver> StartAsync()
    {
        var scenario = await RepositoryContent.CreateStore().GetScenarioAsync("um1:scenario/act1", CancellationToken.None)
                       ?? throw new InvalidOperationException("Act 1 pilot not found.");
        return new EngineDriver(scenario, await RepositoryContent.CatalogAsync(), "Nightingale", "Remi", "Zeke", "Rook");
    }

    [Fact]
    public async Task Play_TheMastToTheGraduation_GradesThePartyAndPaysTheRightReward()
    {
        var game = await StartAsync();

        // Before the MAST, then Day 1: the red totem spawns 3 Cave Sickles, the blue loot gives 2 bonus points
        game.Continue();
        game.Encounter.Node.Should().Be("the-mast-day-1");
        game.Do(GameCommand.ActivateToken("totem:red"));
        game.Encounter.Combatants.Should().HaveCount(3).And.OnlyContain(c => c.Family == "cave-sickle");
        game.Do(GameCommand.ActivateToken("loot:blue"));
        game.Do(GameCommand.ReachEndCondition("win"));
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Number);
        game.Do(GameCommand.AnswerNumber(16));
        game.State.Campaign.Variables["mast-grade"].GetInt32().Should().Be(18);
        game.Continue();

        // Wrong Way → Setting up Camp: Zeke passes his Perception check → Check out the Loot → Cozy Fire
        game.Continue();
        game.State.Prompt!.Kind.Should().Be(PromptKinds.SkillCheck);
        game.Answer("pass-0").Continue().Continue();

        // Day 2: a negative score counts as 0
        game.Encounter.Node.Should().Be("the-mast-day-2");
        game.Encounter.Combatants.Select(c => c.Family).Should().BeEquivalentTo(["cave-sickle", "cave-sickle", "cave-sickle", "gevaudan"]);
        game.Do(GameCommand.ReachEndCondition("lose")).Do(GameCommand.AnswerNumber(-5)).Continue();
        game.State.Campaign.Variables["mast-grade"].GetInt32().Should().Be(18);

        // Scramble Away → The Second Night: let the man pass (+5) → Sleep Tight
        game.Continue();
        game.Answer("1").Continue().Continue();
        game.State.Campaign.Variables["mast-grade"].GetInt32().Should().Be(23);

        // Chamber 1, lost twice: Good Try the first time, then Back to School (game over)
        game.Encounter.Node.Should().Be("the-gauntlet-chamber-1");
        game.Do(GameCommand.ReachEndCondition("lose")).Do(GameCommand.AnswerNumber(0));
        game.State.Campaign.CurrentNode.Should().Be("good-try-lets-go-again");
        game.Continue();
        game.Encounter.Node.Should().Be("the-gauntlet-chamber-1");
        game.Do(GameCommand.ActivateToken("totem:blue"));
        game.Encounter.Tokens.Should().Contain(t => t.Key == "loot:blue" && !t.Removed);
        game.Do(GameCommand.ActivateToken("loot:blue"));
        game.Do(GameCommand.ReachEndCondition("win")).Do(GameCommand.AnswerNumber(4)).Continue();

        // Chamber 2: the random loot is drawn, the totems lay diagrams 1 and 3, the lever pays 10 Gold
        game.Encounter.Node.Should().Be("the-gauntlet-chamber-2");
        game.Encounter.Tokens.Should().Contain(t => t.Id == "loot-random" && t.Key != "loot:random");
        game.Do(GameCommand.ActivateToken("objective:blue"));
        game.Encounter.Tokens.Single(t => t.Id == "objective-blue").Removed.Should().BeTrue();
        game.Do(GameCommand.ActivateToken("totem:blue")).Do(GameCommand.ActivateToken("totem:green"));
        game.Encounter.PlacedDiagrams.Should().Equal("diagram-1", "diagram-3");
        game.Encounter.Tokens.Should().Contain(t => t.Key == "exit:blue" && !t.Removed);

        // The lever was pulled: the red totem spawns the Animate without the Presence check
        game.Do(GameCommand.ActivateToken("totem:red"));
        game.Encounter.Combatants.Should().Contain(c => c.Family == "animate");
        game.State.Prompt.Should().BeNull();
        game.Do(GameCommand.ReachEndCondition("win")).Do(GameCommand.AnswerNumber(20)).Continue();

        // 23 + 5 (coin) + 4 + 20 = 52: 280 Gold and 4 XP each
        game.State.Campaign.Variables["mast-grade"].GetInt32().Should().Be(52);
        game.State.Campaign.CurrentNode.Should().Be("making-the-grade");
        game.Continue();
        game.State.Campaign.Gold.Should().Be(10 + 280);
        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 4);

        // To the end of the pilot
        while (game.State.Status == GameStatus.InProgress)
        {
            game.Continue();
        }

        game.State.Ending.Should().Be("@a-dangerous-path");
        game.Codes.Should().NotContain(["manualAction", "unknownNode", "unknownEndCondition", "spawnImpossible"]);
    }
}
