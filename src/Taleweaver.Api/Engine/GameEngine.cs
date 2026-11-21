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
    /// 6: Story Rounds (phases, Store's Unique Items), Active Group, Injured and Unselectable members.
    /// </summary>
    public const int Version = 6;

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

        public static Error UnknownCommand(string type) => Error.Validation("Games.UnknownCommand", $"Unknown command '{type}'.");
    }

    /// <summary>A new game: the party sets out and the first node of the scenario is read.</summary>
    public static GameState Start(GameContext context, IReadOnlyList<string> party)
    {
        var scenario = context.Scenario;
        var state = new GameState
        {
            Scenario = scenario.Id,
            Campaign = new CampaignState
            {
                Gold = scenario.Party?.StartingGold ?? 0,
                Party = party.Select(name => new AdventurerState { Name = name, Xp = scenario.Party?.StartingXp ?? 0 }).ToList(),
            },
        };
        var session = new Session(state, context);
        session.GoToNode(scenario.Start);
        session.Run();
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
        return session.State;
    }

    /// <summary>Rebuilds a game from its history (used by undo). Fails if a command no longer applies to the scenario.</summary>
    public static Result<GameState> Replay(GameContext context, IReadOnlyList<string> party, IEnumerable<GameCommand> commands)
    {
        var state = Start(context, party);
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
