using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>The party's items (rulebook ch. 6, p. 25): handed out, moved, sold, used, on a small homebrew pack.</summary>
public class ItemsTests
{
    private const string Plank = "hb:item/plank";
    private const string Sword = "hb:item/sword";
    private const string PlankToken = "hb:token/combatant/plank";

    [Fact]
    public void GainItem_NamedCardAndCardsToDraw_WaitToBeHandedOut()
    {
        var game = Game();

        var unassigned = game.State.Campaign.Unassigned;
        unassigned.Should().HaveCount(3);
        unassigned[0].Should().BeEquivalentTo(new { Id = "item-1", Item = Plank, Source = "scenario" });
        unassigned[1].Should().BeEquivalentTo(new { Item = (string?)null, Tiers = new[] { "mundane" }, Type = "consumable" });
        unassigned[2].Id.Should().Be("item-3");
    }

    [Fact]
    public void ItemCommands_WhileAQuestionIsPending_LeaveTheQuestionAsked()
    {
        var game = Game();
        game.State.Prompt!.Kind.Should().Be(PromptKinds.Continue);

        game.Do(GameCommand.MoveItem("item-1", "Rook", "pack"));

        game.State.Prompt!.Kind.Should().Be(PromptKinds.Continue);
        game.State.Campaign.Party.Single(a => a.Name == "Rook").Pack.Select(i => i.Item).Should().Equal(Plank);
        game.State.Campaign.Unassigned.Should().HaveCount(2);
    }

    [Fact]
    public void AddItem_Bought_TakesThePriceFromThePartyGold()
    {
        var game = Game();

        game.Do(GameCommand.AddItem(Sword, "Nightingale", "equipped", price: 40));

        game.State.Campaign.Gold.Should().Be(10);
        game.State.Campaign.Party.Single(a => a.Name == "Nightingale").Equipped.Single().Should()
            .BeEquivalentTo(new { Id = "item-4", Item = Sword, Source = "gm" });
        game.State.Campaign.Items.Should().Contain(Sword);
    }

    [Fact]
    public void AddItem_TooExpensive_IsRefused()
    {
        var game = Game();

        var result = GameEngine.Apply(game.State, GameCommand.AddItem(Sword, "stash", price: 60), game.Context);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Items.NotEnoughGold");
    }

    [Fact]
    public void AddItem_UnknownOwnerOrCard_IsRefused()
    {
        var game = Game();

        GameEngine.Apply(game.State, GameCommand.AddItem(Sword, "Zeke"), game.Context).Error!.Code.Should().Be("Items.UnknownOwner");
        GameEngine.Apply(game.State, GameCommand.AddItem("hb:item/nothing", "stash"), game.Context).Error!.Code.Should().Be("Items.UnknownItem");
    }

    [Fact]
    public void AddItem_CardDrawnForAnItemToDraw_TakesItsPlace()
    {
        var game = Game();

        game.Do(GameCommand.AddItem(Plank, "Rook", "equipped", replaces: "item-2"));

        game.State.Campaign.Unassigned.Select(i => i.Id).Should().Equal("item-1", "item-3");
        game.State.Campaign.Party.Single(a => a.Name == "Rook").Equipped.Single().Should()
            .BeEquivalentTo(new { Id = "item-2", Item = Plank, Source = "scenario", Tiers = (List<string>?)null });
    }

    [Fact]
    public void MoveItem_FromAdventurerToStash_AndDiscardSold_AddsTheGold()
    {
        var game = Game();

        game.Do(GameCommand.MoveItem("item-1", "Rook"))
            .Do(GameCommand.MoveItem("item-1", "stash"))
            .Do(GameCommand.DiscardItem("item-1", gold: 5));

        game.State.Campaign.Stash.Should().BeEmpty();
        game.State.Campaign.Party.SelectMany(a => a.Equipped).Should().BeEmpty();
        game.State.Campaign.Gold.Should().Be(55);
        game.Codes.Should().Contain(["itemMoved", "itemSold"]);
    }

    [Fact]
    public void FlipItem_ShowsTheOtherSide()
    {
        var game = Game().Do(GameCommand.FlipItem("item-1", true));

        game.State.Campaign.Unassigned[0].Flipped.Should().BeTrue();
    }

    [Fact]
    public void UseItem_Plank_PlacesItsTokenAndIsDiscarded()
    {
        var game = Game().Do(GameCommand.MoveItem("item-1", "Rook")).Continue();

        game.Do(GameCommand.UseItem("item-1", discard: true, PlankToken, new Cell(3, 4)));

        game.State.Campaign.Party.Single(a => a.Name == "Rook").Equipped.Should().BeEmpty();
        game.Encounter.Tokens.Should().ContainSingle(t => t.Type == PlankToken)
            .Which.Should().BeEquivalentTo(new { Key = "plank", Area = new Rect(3, 4, 2, 1), Removed = false });
        game.State.Log.Should().Contain(l => l.Code == "itemUsed" && l.Args!["owner"] == "Rook" && l.Args["token"] == PlankToken);
    }

    [Fact]
    public void UseItem_TokenOutsideAnEncounter_IsRefused()
    {
        var game = Game();

        var result = GameEngine.Apply(game.State, GameCommand.UseItem("item-1", discard: true, PlankToken, new Cell(0, 0)), game.Context);

        result.Error.Should().Be(GameEngine.Errors.NotInEncounter);
    }

    [Fact]
    public void UseItem_CardStillToDraw_IsRefused()
    {
        var game = Game();

        GameEngine.Apply(game.State, GameCommand.UseItem("item-2", discard: true), game.Context).Error!.Code.Should().Be("Items.NotDrawnYet");
    }

    [Fact]
    public void Defeat_ItemLoot_AddsACardToDrawOfTheLootLevel()
    {
        var game = Game().Continue();

        game.Do(GameCommand.Defeat("brute-1"));

        game.State.Campaign.Unassigned.Last().Should().BeEquivalentTo(new { Id = "item-4", Item = (string?)null, Tiers = new[] { "common" }, Source = "itemLoot" });
        game.State.Log.Should().Contain(l => l.Code == "lootItem" && l.Args!["id"] == "item-4");
    }

    [Fact]
    public void Replay_ItemCommands_RebuildTheSameItems()
    {
        var game = Game()
            .Do(GameCommand.AddItem(Sword, "Rook", "pack", price: 40))
            .Do(GameCommand.MoveItem("item-1", "Nightingale"))
            .Do(GameCommand.AddItem(Plank, "stash", replaces: "item-3"));

        var replayed = GameEngine.Replay(game.Context, game.Party, game.History);

        replayed.IsSuccess.Should().BeTrue();
        replayed.Value.Campaign.Should().BeEquivalentTo(game.State.Campaign);
    }

    private static EngineDriver Game()
    {
        var catalog = new Catalog(
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
                    new Item { Id = Plank, Name = En("Plank"), Type = "consumable", Tier = "mundane", Cost = 10 },
                    new Item { Id = Sword, Name = En("Sword"), Type = "weapon", Tier = "common", Cost = 40 },
                ],
                Tokens = [new TokenType { Id = PlankToken, Name = En("Plank"), Category = "combatant", Size = "2x1" }],
                Rules = new PackRules { LootDecks = [new LootDeck { LootLevel = "common", Cards = [new LootCard("item", null, 2)] }] },
            },
        ]);
        var scenario = new Scenario
        {
            Id = "hb:scenario/items",
            Title = En("Items"),
            Start = "camp",
            Nodes =
            [
                new Node
                {
                    Id = "camp",
                    Type = "scene",
                    Narrative = En("A merchant waves."),
                    Mechanics =
                    [
                        new ScenarioAction { Do = "gainGold", Amount = 50 },
                        new ScenarioAction { Do = "gainItem", Ref = Plank },
                        new ScenarioAction { Do = "gainItem", Count = 2, Tiers = ["mundane"], Type = "consumable" },
                    ],
                    Next = "fight",
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
        return new EngineDriver(scenario, catalog);
    }

    private static Dictionary<string, string> En(string text) => new() { ["en"] = text };
}
