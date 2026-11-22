using Taleweaver.Api.Common;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The companion's rules, as pure functions: (state, command, content) → new state. No I/O, no clock,
/// chance only through <see cref="IRandomSource"/>: replaying the same commands always gives the same game.
/// </summary>
public static class GameEngine
{
    /// <summary>
    /// Raised when the engine computes a different state from the same history (new fields, rule fixes).
    /// A game saved by an older engine is replayed when loaded. 2: positions; 3: moves, adventurers on the board, fog;
    /// 4: every starting zone filled, initiative cards played this round (cards moved on the track);
    /// 5: the party's items (stash, equipped, pack, items to hand out);
    /// 6: Story Rounds (phases, Store's Unique Items), Active Group, Injured and Unselectable members;
    /// 7: Bounties played from a Story Round, Consumables discarded after Shop and Train, Item Upgrades, Disciplines.
    /// 8: Discipline costs from the catalogue (+1 per Discipline of the same level), level prerequisites, Re-specing.
    /// 9: the adventurers' Health (Damage kept between encounters, Defeat, Prepare Phase, Restore) and Stamina Points.
    /// 10: combatants under the fog wait for their area to be revealed (no card on the track, no turn);
    /// Achievements, launch options, Abraxis Battle Dice.
    /// 11: the party prepared before it sets out; Command Combatants, summons and Conduits.
    /// 12: gainDiscipline, the hiddenCard.&lt;size:code&gt; condition, a Hidden Card revealed once.
    /// 16: who did it, rewards and Skill Checks, the group, what is carried.
    /// 17: the Status Phase asks, reactions, cards and Ability tokens, Disease and Tether.
    /// 18: counters, the track arranged, sides, figures of the scenario, kept boards, effects for later, opposing Espers.
    /// </summary>
    public const int Version = 18;

    public static class Errors
    {
        public static readonly Error Finished = Error.Conflict("Games.Finished", "The scenario is over.");
        public static readonly Error AwaitingAnswer = Error.Conflict("Games.AwaitingAnswer", "Answer the current question first.");
        public static readonly Error NoQuestion = Error.Conflict("Games.NoQuestion", "There is no question to answer.");
        public static readonly Error NotInEncounter = Error.Conflict("Games.NotInEncounter", "No encounter is in progress.");
        public static readonly Error InEncounter = Error.Conflict("Games.InEncounter", "The party cannot change during an encounter.");

        public static Error InvalidAnswer(string? answer) => Error.Validation("Games.InvalidAnswer", $"'{answer}' is not an answer to the current question.");

        public static Error UnknownCombatant(string? instance) => Error.NotFound("Encounters.UnknownCombatant", $"No combatant '{instance}' on the board.");

        public static Error UnknownToken(string? token) => Error.NotFound("Encounters.UnknownToken", $"Token '{token}' is not part of this encounter.");

        public static Error InactiveEndCondition(string? id) => Error.Conflict("Encounters.InactiveEndCondition", $"End condition '{id}' is not active.");

        public static Error UnknownFigure(string? figure) => Error.NotFound("Encounters.UnknownFigure", $"No figure '{figure}' on the board.");

        public static Error UnknownCard(string? card) => Error.NotFound("Encounters.UnknownCard", $"No initiative card '{card}' on the track.");

        public static Error UnknownArea(string? area) => Error.NotFound("Encounters.UnknownArea", $"No hidden area '{area}' on the board.");

        public static Error AdventurerDefeated(string name) => Error.Conflict("Encounters.AdventurerDefeated", $"{name} is Defeated: back at the next encounter.");

        public static Error UnknownCommand(string type) => Error.Validation("Games.UnknownCommand", $"Unknown command '{type}'.");
    }

    /// <summary>
    /// A new game. With names, the party sets out at once and the first node is read; without any, it is prepared
    /// first, within the scenario's rules (roadmap §5.2).
    /// </summary>
    public static GameState Start(GameContext context, IReadOnlyList<string> party)
    {
        var scenario = context.Scenario;
        var state = new GameState
        {
            Scenario = scenario.Id,
            Options = context.Options,
            Campaign = new CampaignState
            {
                Gold = scenario.Party?.StartingGold ?? 0,
                Party = party.Select(name => new AdventurerState { Name = name, Xp = scenario.Party?.StartingXp ?? 0 }).ToList(),
            },
        };
        var session = new Session(state, context);
        if (party.Count == 0)
        {
            session.StartPreparation();
            session.UpdateAdventurerStats();
            return state;
        }

        session.UpdateAdventurerStats();
        session.GoToNode(scenario.Start);
        session.Run();
        session.UpdateAdventurerStats();
        return state;
    }

    public static Result<GameState> Apply(GameState state, GameCommand command, GameContext context)
    {
        if (state.Status == GameStatus.Finished)
        {
            return Errors.Finished;
        }

        var session = new Session(state.Clone(), context);
        var result = session.Execute(command);
        if (!result.IsSuccess)
        {
            return result.Error!;
        }

        session.Run();
        session.UpdateAdventurerStats();
        if (command.By is { } by)
        {
            // The lines this command wrote say who asked for it (a player's request, room-spec §9)
            var log = session.State.Log;
            for (var i = state.Log.Count; i < log.Count; i++)
            {
                log[i] = log[i] with { Args = new Dictionary<string, string>(log[i].Args ?? new Dictionary<string, string>()) { ["by"] = by } };
            }
        }

        return session.State;
    }

    /// <summary>Rebuilds a game from its history (used by undo). Fails if a command no longer applies to the scenario.</summary>
    public static Result<GameState> Replay(GameContext context, IReadOnlyList<string> party, IEnumerable<GameCommand> commands)
    {
        return Replay(context, Start(context, party), commands);
    }

    /// <summary>Applies the rest of a history to the state of a checkpoint.</summary>
    public static Result<GameState> Replay(GameContext context, GameState from, IEnumerable<GameCommand> commands)
    {
        var state = from;
        foreach (var command in commands)
        {
            var result = Apply(state, command, context);
            if (!result.IsSuccess)
            {
                return result.Error!;
            }

            state = result.Value;
        }

        return state;
    }
}

/// <summary>Rule helpers of the engine exposed for tests.</summary>
internal static class GameEngineInternals
{
    public static IEnumerable<int> ActivationNumbers(string? activation) => Session.ActivationNumbers(activation);
}
