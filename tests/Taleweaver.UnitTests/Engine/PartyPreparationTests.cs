using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The departure (roadmap §5.2): the scenario's party rules, kits, budget, and the replay of it all.</summary>
public class PartyPreparationTests
{
    private const string Sword = "hb:item/sword";
    private const string Axe = "hb:item/axe";
    private const string Spark = "hb:discipline/spark";
    private const string Flame = "hb:discipline/flame";

    private static readonly StarterKit RookPack = new() { Id = "rook-pack", Name = En("Pack"), Adventurer = "hb:adventurer/rook", Items = [Sword, Sword, Sword], Disciplines = [Spark] };

    [Fact]
    public void Start_WithoutNames_ReadsNothingOfTheScenario()
    {
        var game = Prepare(new Party());

        game.Log.Should().BeEmpty();
        game.Preparation!.Missing.Should().Be("Party.TooFew");
        Apply(game, GameCommand.NextTurn()).Should().Be("Games.Preparing");
    }

    [Fact]
    public void Start_RequiredAdventurerWithOneKit_IsReadyToSetOut()
    {
        var game = Prepare(new Party { RequiredAdventurers = ["Rook"], Start = new PartyStart { Kits = [RookPack] } });

        game.Preparation!.Choices.Should().ContainSingle().Which.Kit.Should().Be("rook-pack");
        game.Preparation.Missing.Should().BeNull();
    }

    [Fact]
    public void AKit_IsEquippedWithinTheLimits_ThenPacked_AndFree()
    {
        var game = Prepare(new Party { RequiredAdventurers = ["Rook"], Start = new PartyStart { Kits = [RookPack] } });

        var rook = game.Campaign.Party.Single();
        rook.Equipped.Should().HaveCount(2, "two hands");
        rook.Pack.Should().ContainSingle();
        rook.Disciplines.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = Spark, Xp = 0 });
    }

    [Fact]
    public void AllowedAdventurers_OnlyThoseMayCome()
    {
        var game = Prepare(new Party { AllowedAdventurers = ["Rook"] });

        Apply(game, GameCommand.ChooseAdventurer("Echo")).Should().Be("Party.NotOffered");
        Apply(game, GameCommand.ChooseAdventurer("A stranger")).Should().Be("Party.NotOffered", "a list restricts the names typed");
    }

    [Fact]
    public void HiddenAdventurers_AreOfferedOnceTheGmShowsThem()
    {
        var game = Prepare(new Party());
        game.Preparation!.Adventurers.Select(a => a.Name).Should().NotContain("Echo");
        Apply(game, GameCommand.ChooseAdventurer("Echo")).Should().Be("Party.NotOffered", "a typed name is no way around");

        game = Done(Done(game, GameCommand.ShowHiddenAdventurers(true)), GameCommand.ChooseAdventurer("Echo"));

        game.Campaign.Party.Select(a => a.Name).Should().Equal("Echo");
    }

    [Fact]
    public void AHiddenAdventurer_NamedByTheScenario_IsOffered_AWildcardIsNotEnough()
    {
        Prepare(new Party { AllowedAdventurers = ["hb:adventurer/*"] }).Preparation!.Adventurers.Select(a => a.Name).Should().Equal("Rook");
        Prepare(new Party { AllowedAdventurers = ["Echo"] }).Preparation!.Adventurers.Select(a => a.Name).Should().Equal("Echo");
    }

    [Theory]
    [InlineData("stash")]
    [InlineData("unassigned")]
    [InlineData("Rook/../x")]
    [InlineData("ally:wolf")]
    [InlineData("A, B")]
    public void ATypedName_ThatWouldBreakAnId_IsRefused(string name)
    {
        Apply(Prepare(new Party()), GameCommand.ChooseAdventurer(name)).Should().Be("Party.NotOffered");
    }

    [Fact]
    public void Size_IsTheMost()
    {
        var game = Done(Prepare(new Party { Size = 1 }), GameCommand.ChooseAdventurer("Rook"));

        Apply(game, GameCommand.ChooseAdventurer("Someone")).Should().Be("Party.TooMany");
    }

    [Fact]
    public void Purchases_ShareThePartysGold()
    {
        var game = Prepare(new Party { StartingGold = 50, Start = new PartyStart { Items = new StartItems { Tiers = ["mundane"] } } });

        game = Done(game, GameCommand.ChooseAdventurer("Rook", items: [Sword]));

        game.Campaign.Gold.Should().Be(20);
        Apply(game, GameCommand.ChooseAdventurer("Someone", items: [Sword])).Should().Be("Party.NotEnoughGold");
        Apply(game, GameCommand.ChooseAdventurer("Rook", items: [Axe])).Should().Be("Party.ItemNotForSale");
    }

    [Fact]
    public void ChangingItsMind_LeavesNothingBehind()
    {
        var game = Prepare(new Party { StartingGold = 50, Start = new PartyStart { Items = new StartItems { Tiers = ["mundane"] } } });

        game = Done(Done(game, GameCommand.ChooseAdventurer("Rook", items: [Sword])), GameCommand.ChooseAdventurer("Rook", items: []));

        game.Campaign.Gold.Should().Be(50);
        game.Campaign.Items.Should().BeEmpty();
    }

    [Fact]
    public void ASavedHistory_StillSaysNothingBought()
    {
        GameCommand[] commands = [GameCommand.ChooseAdventurer("Rook", items: [Sword]), GameCommand.ChooseAdventurer("Rook", items: [])];
        var rules = new Party { StartingGold = 50, Start = new PartyStart { Items = new StartItems { Tiers = ["mundane"] } } };

        var saved = JsonSerializer.Deserialize<GameCommand[]>(JsonSerializer.Serialize(commands, JsonDefaults.FileOptions), JsonDefaults.Options)!;

        GameEngine.Replay(Context(rules), [], saved).Value.Campaign.Gold.Should().Be(50);
    }

    [Fact]
    public void Disciplines_CostTheirXp_WhateverTheOrderPicked()
    {
        var game = Done(Prepare(new Party { StartingXp = 8 }), GameCommand.ChooseAdventurer("Rook", disciplines: [Flame, Spark]));

        game.Campaign.Party.Single().Disciplines.Select(d => (d.Id, d.Xp)).Should().Equal((Spark, 3), (Flame, 5));
    }

    [Fact]
    public void Disciplines_WithoutTheirLowerLevelOrTheXp_AreRefused()
    {
        Apply(Prepare(new Party { StartingXp = 8 }), GameCommand.ChooseAdventurer("Rook", disciplines: [Flame])).Should().Be("Party.MissingLevel");
        Apply(Prepare(new Party { StartingXp = 7 }), GameCommand.ChooseAdventurer("Rook", disciplines: [Spark, Flame])).Should().Be("Party.NotEnoughXp");
    }

    [Fact]
    public void SetOut_WithoutTheKitOwed_IsRefused()
    {
        var kits = new PartyStart { Kits = [new StarterKit { Id = "a", Name = En("A") }, new StarterKit { Id = "b", Name = En("B") }] };
        var game = Done(Prepare(new Party { Start = kits }), GameCommand.ChooseAdventurer("Rook"));

        Apply(game, GameCommand.SetOut()).Should().Be("Party.KitMissing");
        Apply(Done(game, GameCommand.ChooseAdventurer("Rook", kit: "a")), GameCommand.SetOut()).Should().BeNull();
    }

    [Fact]
    public void ARequiredAdventurer_CannotBeTakenOut()
    {
        Apply(Prepare(new Party { RequiredAdventurers = ["Rook"] }), GameCommand.RemoveAdventurer("Rook")).Should().Be("Party.Required");
    }

    [Fact]
    public void SetOut_ReadsTheFirstNode_AndReplaysTheSame()
    {
        var rules = new Party { StartingGold = 50, StartingXp = 3, Start = new PartyStart { Items = new StartItems { Tiers = ["mundane"] } } };
        var commands = new[] { GameCommand.ChooseAdventurer("Rook", items: [Sword], disciplines: [Spark]), GameCommand.SetOut() };

        var game = commands.Aggregate(Prepare(rules), (state, command) => Done(state, command, rules));

        game.Preparation.Should().BeNull();
        game.Log.Select(l => l.Code).Should().Contain("setOut").And.Contain("scene");
        GameEngine.Replay(Context(rules), [], commands).Value.Should().BeEquivalentTo(game);
        Apply(game, GameCommand.ChooseAdventurer("Someone")).Should().Be("Games.SetOut");
    }

    private Party? _rules;

    private GameState Prepare(Party rules)
    {
        _rules = rules;
        return GameEngine.Start(Context(rules), []);
    }

    private string? Apply(GameState game, GameCommand command) => GameEngine.Apply(game, command, Context(_rules!)).Error?.Code;

    private GameState Done(GameState game, GameCommand command, Party? rules = null)
    {
        var result = GameEngine.Apply(game, command, Context(rules ?? _rules!));
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }

    private static GameContext Context(Party rules)
    {
        var catalog = new Catalog([new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = En("hb") },
            Adventurers =
            [
                new Adventurer { Id = "hb:adventurer/rook", Name = En("Rook") },
                new Adventurer { Id = "hb:adventurer/echo", Name = En("Echo"), Hidden = true },
            ],
            Items =
            [
                new Item { Id = Sword, Name = En("Sword"), Type = "weapon", Tier = "mundane", Cost = 30 },
                new Item { Id = Axe, Name = En("Axe"), Type = "weapon", Tier = "rare", Cost = 10 },
            ],
            Disciplines =
            [
                new Discipline { Id = Spark, Name = En("Spark"), Tree = "cruor", Level = 1, Cost = 3 },
                new Discipline { Id = Flame, Name = En("Flame"), Tree = "cruor", Level = 2, Cost = 5 },
            ],
        }]);
        var scenario = new Scenario { Id = "hb:scenario/party", Title = En("Party"), Party = rules, Start = "town", Nodes = [new Node { Id = "town", Type = "scene", Next = "@end" }] };
        return new GameContext(scenario, catalog, new SeededRandomSource(7));
    }

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
