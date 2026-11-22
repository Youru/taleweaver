using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>Hidden Cards (rulebook p. 37): revealed once, read by conditions; a Discipline the story gives.</summary>
public class HiddenCardsTests
{
    private static readonly Catalog Catalog = new(
    [
        new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = En("Homebrew") },
            Disciplines = [new Discipline { Id = "hb:discipline/gift", Name = En("Gift"), Tree = "cruor", Story = true }],
        },
    ]);

    [Fact]
    public void RevealHiddenCard_TwiceThenACondition_IsRevealedOnceAndTheBranchIsTaken()
    {
        var game = Game(Reveal(), Reveal(), IfRevealed());

        game.State.Campaign.RevealedHiddenCards.Should().Equal("medium:HB1");
        game.State.Campaign.Gold.Should().Be(Game().State.Campaign.Gold + 5);
    }

    [Fact]
    public void Condition_CardNotRevealed_BranchIsNotTaken() =>
        Game(IfRevealed()).State.Campaign.Gold.Should().Be(Game().State.Campaign.Gold);

    [Fact]
    public void GainDiscipline_NobodyNamed_TheGmSaysWhoTakesItWithoutXp()
    {
        var game = Game(new ScenarioAction { Do = "gainDiscipline", Ref = "hb:discipline/gift" });

        game.Answer("1");

        var rook = game.State.Campaign.Party.Single(a => a.Name == "Rook");
        rook.Disciplines.Should().ContainSingle(d => d.Id == "hb:discipline/gift" && d.Xp == 0);
        game.State.Campaign.Party.Single(a => a.Name == "Nightingale").Disciplines.Should().BeEmpty();
    }

    private static ScenarioAction Reveal() => new() { Do = "revealHiddenCard", Size = "medium", Code = "HB1" };

    private static ScenarioAction IfRevealed() => new()
    {
        Do = "branch",
        If = [new EventCondition { Var = "hiddenCard.medium:HB1", Value = System.Text.Json.JsonSerializer.SerializeToElement(true) }],
        Then = [new ScenarioAction { Do = "gainGold", Amount = 5 }],
    };

    private static EngineDriver Game(params ScenarioAction[] actions)
    {
        var scenario = new Scenario
        {
            Id = "hb:scenario/hidden",
            Title = En("Hidden"),
            Start = "town",
            Nodes = [new Node { Id = "town", Type = "scene", Next = "@end", Mechanics = actions }],
        };
        return new EngineDriver(scenario, Catalog);
    }

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
