using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.UnitTests.Content;

/// <summary>The fields of the play-spec lots 2 to 4, as the checker reads them.</summary>
public class LotsCheckerTests
{
    [Fact]
    public void WhatTheEngineNowPlays_IsNotFlagged()
    {
        var report = Check("""
            {"id":"fight","type":"encounter","lootLevel":"common","setup":{"combatants":[{"ref":"figure:altar"},{"ref":"figure:root"}]},
             "figures":[{"id":"altar","stats":{"health":3}}],"endConditions":[{"id":"win","kind":"win","next":"@end"}],
             "events":[{"id":"start","trigger":{"on":"encounterStart"},"actions":[{"do":"setCounter","id":"time"},{"do":"delay","rounds":1,"actions":[{"do":"modifyFigure","instance":"altar"}]}]},
                       {"id":"late","trigger":{"on":"counter","counter":"time","value":3},"conditions":[{"var":"counter.time","op":">=","value":3},{"var":"carried.idol","value":true}]},
                       {"id":"end","trigger":{"on":"turnEnd"}}]}
            """);

        report.Errors.Should().BeEmpty();
        report.Warnings.Select(w => w.Code).Should().NotContain(["Scenarios.UnknownAction", "Scenarios.UnknownTrigger", "Scenarios.UnknownVariable", "Scenarios.UnknownCounter"]);
    }

    [Fact]
    public void BrokenLinks_AreFlagged()
    {
        var report = Check("""
            {"id":"fight","type":"encounter","lootLevel":"common","keepBoardFrom":"nowhere","setup":{"combatants":[{"ref":"figure:ghost"}]},
             "encounterEspers":[{"ref":"hb:combatant/none","summoner":"boss"}],"endConditions":[{"id":"win","kind":"win","next":"@end"}],
             "events":[{"id":"late","trigger":{"on":"counter","counter":"time"},"actions":[{"do":"gainItem","pick":"maybe"},{"do":"askSkillCheck","mode":"group"}]}]}
            """);

        report.Errors.Select(e => e.Code).Should().Contain(
            ["Scenarios.UnknownBoard", "Scenarios.UnknownFigure", "Scenarios.UnknownEsper", "Scenarios.BadPick", "Scenarios.BadCheckMode"]);
        report.Warnings.Select(w => w.Code).Should().Contain("Scenarios.UnknownCounter");
    }

    [Fact]
    public void EveryExitUnderACondition_IsFlagged()
    {
        var report = Check("""{"id":"fight","type":"storyRound","ventureForth":[{"node":"@end","if":[{"var":"key","value":true}]}]}""");

        report.Warnings.Select(w => w.Code).Should().Contain(["Scenarios.NoUnconditionalExit", "Scenarios.UnknownVariable"]);
    }

    private static ScenarioReport Check(string node)
    {
        var name = new Dictionary<string, string> { ["en"] = "Lots" };
        var scenario = new Scenario
        {
            Id = "hb:scenario/lots",
            Title = name,
            Start = "fight",
            Nodes = [JsonSerializer.Deserialize<Node>(node, JsonDefaults.Options)!],
        };
        return ScenarioChecker.Check(scenario, new Catalog([new Pack { Manifest = new PackManifest { Id = "hb", Name = name } }]));
    }
}
