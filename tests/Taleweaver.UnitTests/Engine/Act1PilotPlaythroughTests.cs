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

    [ContentFact]
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

        // Shop and Train (p. 21): a Story Round with no Side Quest, the Store of Mundane Items and Accessories
        game.State.Campaign.StoryRound!.Node.Should().Be("making-the-grade-shop");
        game.Answer("explore");
        game.State.Prompt!.Options.Select(o => o.Id).Should().Equal("venture:0");
        game.Answer("venture:0");
        game.State.Campaign.CurrentNode.Should().Be("path-to-citizenship");

        // The graduation, Yun's party, then the White Vaults (p. 33): a loot of 10 Gold, 1 XP each
        while (game.State.Encounter is null)
        {
            game.Continue();
        }

        game.Encounter.Node.Should().Be("a-dangerous-path");
        game.Do(GameCommand.ActivateToken("loot:blue")).Do(GameCommand.ReachEndCondition("win")).Continue();
        game.State.Campaign.Gold.Should().Be(300);
        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 5);

        // Rumble in the Dark (p. 34): the Animate is not impressed, the party charges it (D)
        game.Encounter.Node.Should().Be("rumble-in-the-dark");
        game.Do(GameCommand.ActivateToken("objective:blue"));
        while (game.State.Prompt?.Options.Any(o => o.Id == "fail") != true)
        {
            game.Continue();
        }

        game.Answer("fail");
        SkipTexts(game);
        game.State.Prompt!.Options.Should().HaveCount(2, "play it safe (C) or charge (D)");
        game.Answer("1");
        SkipTexts(game);
        game.Do(GameCommand.ReachEndCondition("lose")).Continue();

        // Into the Gloom (p. 36), then Hogrifar, Suri, the Jeong house and the castle
        game.Encounter.Node.Should().Be("into-the-gloom");
        game.Do(GameCommand.ActivateToken("loot:blue")).Do(GameCommand.ReachEndCondition("win")).Continue();
        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 6);
        while (game.State.Campaign.CurrentNode != "the-arsen-castle")
        {
            game.Continue();
        }

        // 6 XP earned: 40 Gold, then the Story Round of p. 47
        SkipTexts(game);
        game.Answer("2");
        while (game.State.Campaign.StoryRound is null)
        {
            game.Continue();
        }

        game.State.Campaign.Gold.Should().Be(310 + 40);
        game.State.Campaign.StoryRound!.Node.Should().Be("the-city-of-elenia");

        // Loa Problems (p. 48): the altar is reached, one Loa is defeated, then the party falls: 40 Gold anyway
        game.Answer("explore");
        game.Answer("quest:loa-problems");
        while (game.State.Encounter is null)
        {
            game.Continue();
        }

        game.Encounter.Node.Should().Be("loa-altar");
        while (game.State.Prompt is not null)
        {
            game.Answer(game.State.Prompt.Options[0].Id);
        }

        game.Do(GameCommand.ActivateToken("objective:blue"));
        SkipTexts(game);
        game.State.Campaign.Variables["loas-defeated"].GetInt32().Should().Be(0);
        game.Do(GameCommand.ReachEndCondition("lose"));
        while (game.State.Campaign.CurrentNode != "the-city-of-elenia")
        {
            game.Continue();
        }

        game.State.Campaign.Gold.Should().Be(350 + 50);
        game.Answer("explore");
        game.State.Prompt!.Options.Select(o => o.Id).Should().NotContain("quest:loa-problems");

        // Cave Sickle Extermination (p. 49): down the chasm to the Dank Grotto, then the bomb is set and time runs out
        game.Answer("quest:cave-sickle-extermination");
        while (game.State.Encounter is null)
        {
            game.Continue();
        }

        game.Encounter.Node.Should().Be("bombing-the-sickles");
        while (game.State.Prompt is not null)
        {
            game.Answer(game.State.Prompt.Options[0].Id);
        }

        game.Do(GameCommand.ReachEndCondition("alt-win")).Continue();
        game.Encounter.Node.Should().Be("dank-grotto");
        game.Do(GameCommand.ReachEndCondition("win")).Continue();
        game.Encounter.Node.Should().Be("sickles-upon-sickles");
        SkipTexts(game);
        game.Do(GameCommand.ActivateToken("objective:blue"));
        SkipTexts(game);
        game.Do(GameCommand.ReachEndCondition("time-out"));
        while (game.State.Campaign.CurrentNode != "the-city-of-elenia")
        {
            game.Continue();
        }

        game.State.Campaign.Gold.Should().Be(400 + 60);
        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 7);
        game.Answer("explore");
        game.Answer("venture:0");

        // Back to the Vaults (p. 66): the party falls once, then wins the last two encounters
        while (game.State.Encounter is null)
        {
            game.Continue();
        }

        game.Encounter.Node.Should().Be("retracing-their-steps");
        game.Do(GameCommand.ReachEndCondition("lose")).Continue();
        game.Encounter.Node.Should().Be("an-alternate-path");
        game.Do(GameCommand.ActivateToken("objective:blue"));
        SkipTexts(game);
        game.Do(GameCommand.ReachEndCondition("win-red")).Continue();
        game.Encounter.Node.Should().Be("finding-their-way");
        game.Do(GameCommand.ReachEndCondition("win"));
        while (game.State.Campaign.StoryRound?.Node != "rhamsted-exploration")
        {
            if (game.State.Prompt is { Kind: not PromptKinds.Continue } prompt)
            {
                game.Answer(prompt.Options[0].Id);
            }
            else
            {
                game.Continue();
            }
        }

        // Chapter 2 (p. 74-81): the scenes up to the next Story Round
        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 8);

        // The Side Quest of p. 82: its encounter won, back to the Story Round
        game.Answer("explore");
        game.Answer("quest:maliks-mission");
        while (game.State.Encounter is null)
        {
            game.Continue();
        }

        game.Encounter.Node.Should().Be("bungled-caper");
        game.Do(GameCommand.ActivateToken("totem:red"));
        game.Encounter.Combatants.Should().Contain(c => c.Family == "earth-loa", "Fragors are on the board: A");
        game.Do(GameCommand.ReachEndCondition("win"));
        while (game.State.Campaign.CurrentNode != "rhamsted-exploration")
        {
            game.Continue();
        }

        game.State.Campaign.Party.Should().OnlyContain(a => a.Xp == 9);
        game.Answer("explore");
        game.Answer("venture:0");

        // The Main Quest (p. 87): the party loses its encounter, then answers the decision with its second option
        while (game.State.Encounter is null)
        {
            game.Continue();
        }

        game.State.Campaign.RevealedHiddenCards.Should().Contain(c => c.Contains("UM5"));
        game.Do(GameCommand.ReachEndCondition("lose"));
        while (game.State.Prompt?.Options.Count != 2)
        {
            game.Continue();
        }

        game.Answer(game.State.Prompt.Options[1].Id);
        while (game.State.Status == GameStatus.InProgress)
        {
            game.Continue();
        }

        game.State.Ending.Should().Be("@chapter-2-rhamsted-recreation");
        // Sickles Upon Sickles calls 6 more Cave Sickles than their cards allow (p. 59): only those cannot spawn
        game.Codes.Should().NotContain(["manualAction", "unknownNode", "unknownEndCondition"]);
        game.Codes.Count(c => c == "spawnImpossible").Should().Be(1);
    }

    /// <summary>Reads on through the texts shown to the GM, up to the next question.</summary>
    private static void SkipTexts(EngineDriver game)
    {
        while (game.State.Prompt?.Kind == PromptKinds.Continue)
        {
            game.Continue();
        }
    }
}
