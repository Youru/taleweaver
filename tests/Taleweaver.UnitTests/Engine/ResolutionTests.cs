using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.UnitTests.Engine;

/// <summary>An Attack or a Spell validated from the duel (engine 20).</summary>
public class ResolutionTests
{
    private const string A = "adventurer:A";
    private const string Imp = "imp-1";

    [Fact]
    public void Damage_LandsOnAnAdventurer()
    {
        var game = Game().Do(GameCommand.Resolve(Imp, A, 3, new(Resolution.Hit, 3)));

        Member(game).Damage.Should().Be(3);
        game.Codes.Should().Contain(["attackResolved", "adventurerDamage"]);
    }

    [Fact]
    public void Damage_LandsOnACombatant_AndCountsForAggro()
    {
        var game = Game().Do(GameCommand.Resolve(A, Imp, 2, new(Resolution.Hit)));

        game.Combatant(Imp).Damage.Should().Be(2);
        game.Combatant(Imp).Attacked.Should().BeTrue();
    }

    [Fact]
    public void Damage_BetweenAllies_IsNotAggro()
    {
        var game = Game().Do(GameCommand.Resolve("imp-2", Imp, 1, new(Resolution.Hit)));

        game.Combatant(Imp).Damage.Should().Be(1);
        game.Combatant(Imp).Attacked.Should().BeFalse();
    }

    [Theory]
    [InlineData(Resolution.Miss, "attackMissed")]
    [InlineData(Resolution.Resisted, "spellResisted")]
    [InlineData(Resolution.Hit, "attackBlocked")]
    public void WithoutDamage_TheTargetIsNotedAllTheSame(string outcome, string code)
    {
        var game = Game().Do(GameCommand.Resolve(A, Imp, 0, new(outcome)));

        game.Combatant(Imp).Attacked.Should().BeTrue();
        game.Combatant(Imp).Damage.Should().Be(0);
        game.Codes.Should().Contain(code);
    }

    [Fact]
    public void AMiss_NeverDeals()
    {
        var game = Game().Do(GameCommand.Resolve(Imp, A, 5, new(Resolution.Miss)));

        Member(game).Damage.Should().Be(0);
    }

    [Fact]
    public void Empower_AndDodge_CostTheirStamina()
    {
        var game = Game();
        var held = Member(game).Stamina;

        game.Do(GameCommand.Resolve(A, Imp, 0, new(Resolution.Miss, Empower: true)));
        game.Do(GameCommand.Resolve(Imp, A, 0, new(Resolution.Miss, Dodge: true)));

        Member(game).Stamina.Should().Be(held - 2);
    }

    [Fact]
    public void Backfire_HurtsTheCaster_AndIsNotAggroAgainstIt()
    {
        var game = Game().Do(GameCommand.Resolve(Imp, A, 9, new(Resolution.Backfire)));

        game.Combatant(Imp).Damage.Should().Be(3);
        game.Combatant(Imp).Attacked.Should().BeFalse();
        Member(game).Damage.Should().Be(0);
    }

    [Fact]
    public void AnAiAction_IsResolvedOnce_AndTheAiGoesOn()
    {
        var game = Game();
        var step = Told(game);
        var resolve = GameCommand.Resolve(Imp, A, 1, new(Resolution.Hit, Step: step));

        game.Do(resolve);

        game.State.Prompt!.Kind.Should().Be(PromptKinds.AiCondition, "the next figure's question is asked again");
        GameEngine.Apply(game.State, resolve, game.Context).Error!.Code.Should().Be("Encounters.AlreadyResolved");
        Member(game).Damage.Should().Be(1);
        var replayed = GameEngine.Replay(game.Context, game.Party, game.History).Value;
        JsonSerializer.Serialize(replayed, JsonDefaults.FileOptions).Should().Be(JsonSerializer.Serialize(game.State, JsonDefaults.FileOptions));
    }

    [Fact]
    public void AnAiAction_IsThatFiguresOwn()
    {
        var game = Game();
        var step = Told(game);

        foreach (var command in new[]
        {
            GameCommand.Resolve("imp-2", A, 1, new(Resolution.Hit, Step: step)),
            GameCommand.Resolve(Imp, A, 1, new(Resolution.Hit, Step: "0:0:0")),
            GameCommand.Resolve(Imp, A, 1, new(Resolution.Hit, Step: "x")),
        })
        {
            GameEngine.Apply(game.State, command, game.Context).Error!.Code.Should().Be("Encounters.UnknownStep");
        }
    }

    [Fact]
    public void AnUnknownTarget_IsRefused()
    {
        var game = Game();

        GameEngine.Apply(game.State, GameCommand.Resolve(Imp, "adventurer:Z", 1, new(Resolution.Hit)), game.Context)
            .IsSuccess.Should().BeFalse();
    }

    /// <summary>Plays up to imp-1's instruction; imp-2's question waits.</summary>
    private static string Told(EngineDriver game)
    {
        for (var i = 0; i < 6; i++)
        {
            game.Do(GameCommand.NextTurn());
            if (game.State.Prompt?.Kind == PromptKinds.AiCondition)
            {
                game.Answer("yes");
                var line = game.State.Log.ToList().FindLastIndex(l => l.Code == "aiInstruction");
                return $"{line}:0:0";
            }
        }

        throw new InvalidOperationException("No AI turn.");
    }

    private static AdventurerState Member(EngineDriver game) => game.State.Campaign.Party.Single(a => a.Name == "A");

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)!;

    private static EngineDriver Game()
    {
        var name = new Dictionary<string, string> { ["en"] = "Duel" };
        var node = Read<Node>("""
            {"id":"fight","type":"encounter","lootLevel":"common","setup":{"combatants":[{"family":"imp"},{"family":"imp"}]},
             "endConditions":[{"id":"win","kind":"win"},{"id":"lose","kind":"lose"}]}
            """);
        var pack = new Pack
        {
            Manifest = new PackManifest { Id = "hb", Name = name },
            Combatants =
            [
                Read<Combatant>("""
                    {"id":"hb:combatant/imp","family":"imp","name":{"en":"Imp"},"kind":"intelligent","lootLevel":"common","activation":"1-3",
                     "initiativeCards":["hb:initiative/imp"],"stats":{"health":9},
                     "aiSteps":[{"condition":{"text":{"en":"In range?"},"kind":"ask"},"instructions":[{"text":{"en":"Make a [Melee] ATK."}}]}]}
                    """),
            ],
        };
        return new EngineDriver(new Scenario { Id = "hb:scenario/duel", Title = name, Start = "fight", Nodes = [node] }, new Catalog([pack]), "A", "B");
    }
}
