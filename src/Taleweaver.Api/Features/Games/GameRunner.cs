using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

/// <summary>Current state of a game, as returned by every game and encounter command.</summary>
public sealed record GameView(string Id, string Scenario, IReadOnlyList<string> Party, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool CanUndo, GameState State)
{
    public static GameView Of(Game game) =>
        new(game.Id, game.Scenario, PartyOf(game), game.CreatedAt, game.UpdatedAt, game.Commands.Count > 0, game.State);

    /// <summary>A prepared game started without names: its party is in the state.</summary>
    public static IReadOnlyList<string> PartyOf(Game game) =>
        game.Party.Count > 0 ? game.Party : game.State.Campaign.Party.Select(a => a.Name).ToList();
}

/// <summary>
/// The I/O around the pure engine, shared by the Games and Encounters features: load the game and its
/// scenario, apply the command, save. The engine itself never touches the stores.
/// </summary>
/// <summary>Told after every saved change of a game (the room pushes the new views to the phones).</summary>
public interface IGameObserver
{
    Task GameChangedAsync(Game game, CancellationToken cancellationToken);
}

public sealed class GameRunner(IGameStore games, IContentStore content, CatalogProvider catalogs, TimeProvider time, IEnumerable<IGameObserver> observers)
{
    /// <summary>
    /// The changes of one game, one after the other: several phones choose at the same moment, and a load-apply-save
    /// crossing another would lose a command. Never taken by a load: the observers load.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Writing = new();

    private static async Task<Result<Game>> OneAtATimeAsync(string id, Func<Task<Result<Game>>> change, CancellationToken cancellationToken)
    {
        var gate = Writing.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await change();
        }
        finally
        {
            gate.Release();
        }
    }

    public static class Errors
    {
        public static Error NotFound(string id) => Error.NotFound("Games.NotFound", $"No game '{id}'.");

        public static Error ScenarioNotFound(string id) => Error.NotFound("Games.ScenarioNotFound", $"No scenario '{id}'.");

        public static readonly Error NotFinished = Error.Conflict("Games.NotFinished", "The Adventure is not over.");

        public static readonly Error NoEncounterPlayed = Error.Conflict("Games.NoEncounterPlayed", "No encounter was played: start again from the beginning.");

        public static readonly Error NothingToUndo = Error.Conflict("Games.NothingToUndo", "There is no action to undo.");
    }

    public async Task<Result<Game>> StartAsync(string scenarioId, IReadOnlyList<string> party, GameOptions? options, CancellationToken cancellationToken)
    {
        var seed = Random.Shared.Next();
        var context = await ContextAsync(scenarioId, seed, options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var now = time.GetUtcNow();
        var game = new Game
        {
            Id = Guid.NewGuid().ToString("N"),
            Scenario = scenarioId,
            EnabledPacks = context.Value.Catalog.Packs.Select(p => p.Id).ToList(),
            Party = party,
            Seed = seed,
            Options = options,
            CreatedAt = now,
            UpdatedAt = now,
            EngineVersion = GameEngine.Version,
            State = GameEngine.Start(context.Value, party),
        };
        await games.SaveAsync(game, cancellationToken);
        return game;
    }

    /// <summary>
    /// A saved game, brought up to date: a state computed by an older engine is rebuilt from the history.
    /// If the history no longer applies (scenario changed since), the saved state is kept as it is.
    /// </summary>
    public async Task<Result<Game>> LoadAsync(string id, CancellationToken cancellationToken)
    {
        if (await games.GetAsync(id, cancellationToken) is not { } game)
        {
            return Errors.NotFound(id);
        }

        if (game.EngineVersion >= GameEngine.Version)
        {
            return game;
        }

        var replayed = await ReplayAsync(game, cancellationToken, touch: false);
        return replayed.IsSuccess ? replayed.Value : game;
    }

    public Task<Result<Game>> ExecuteAsync(string id, GameCommand command, CancellationToken cancellationToken) =>
        OneAtATimeAsync(id, () => ApplyAsync(id, command, cancellationToken), cancellationToken);

    private async Task<Result<Game>> ApplyAsync(string id, GameCommand command, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var game = loaded.Value;
        var context = await ContextAsync(game.Scenario, game.Seed, game.Options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var state = GameEngine.Apply(game.State, command, context.Value);
        if (!state.IsSuccess)
        {
            return state.Error!;
        }

        return await SaveAsync(game with { Commands = [.. game.Commands, command], State = state.Value }, cancellationToken);
    }

    /// <summary>Would the command apply now? Nothing is saved (a player's request is checked when it is made).</summary>
    public async Task<Result> CheckAsync(string id, GameCommand command, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var context = await ContextAsync(loaded.Value.Scenario, loaded.Value.Seed, loaded.Value.Options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var state = GameEngine.Apply(loaded.Value.State, command, context.Value);
        return state.IsSuccess ? Result.Success() : state.Error!;
    }

    /// <summary>Undo = replay the history without its last command.</summary>
    public Task<Result<Game>> UndoAsync(string id, CancellationToken cancellationToken) =>
        OneAtATimeAsync(id, () => ReplayWithoutLastAsync(id, cancellationToken), cancellationToken);

    private async Task<Result<Game>> ReplayWithoutLastAsync(string id, CancellationToken cancellationToken)
    {
        if (await games.GetAsync(id, cancellationToken) is not { } game)
        {
            return Errors.NotFound(id);
        }

        if (game.Commands.Count == 0)
        {
            return Errors.NothingToUndo;
        }

        return await ReplayAsync(game with { Commands = game.Commands.Take(game.Commands.Count - 1).ToList() }, cancellationToken);
    }

    /// <summary>Rebuilds the state from the history (import of an exported game, undo).</summary>
    public async Task<Result<Game>> ReplayAsync(Game game, CancellationToken cancellationToken, bool touch = true)
    {
        var context = await ContextAsync(game.Scenario, game.Seed, game.Options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var state = GameEngine.Replay(context.Value, game.Party, game.Commands);
        if (!state.IsSuccess)
        {
            return state.Error!;
        }

        return await SaveAsync(game with { State = state.Value }, cancellationToken, touch);
    }

    /// <summary>
    /// An Ending (rulebook p. 13): play again from the beginning, or from the last encounter entered. Always a new
    /// game, the one that ended stays as it is.
    /// </summary>
    public async Task<Result<Game>> RestartAsync(string id, bool fromLastEncounter, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var game = loaded.Value;
        if (game.State.Status != GameStatus.Finished)
        {
            return Errors.NotFinished;
        }

        if (!fromLastEncounter)
        {
            // A prepared party sets out again as it was: the history up to the departure, with other dice
            var departure = game.Commands.ToList().FindIndex(c => c.Type == GameCommands.SetOut);
            if (game.Party.Count == 0 && departure >= 0)
            {
                var now = time.GetUtcNow();
                var again = game with { Id = Guid.NewGuid().ToString("N"), Seed = Random.Shared.Next(), CreatedAt = now, Commands = game.Commands.Take(departure + 1).ToList() };
                return await ReplayAsync(again, cancellationToken);
            }

            return await StartAsync(game.Scenario, game.Party, game.Options, cancellationToken);
        }

        var context = await ContextAsync(game.Scenario, game.Seed, game.Options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var state = GameEngine.Start(context.Value, game.Party);
        var visits = 0;
        var entered = -1;
        for (var done = 0; done <= game.Commands.Count; done++)
        {
            if (state.Campaign.VisitedNodes.Count > visits)
            {
                visits = state.Campaign.VisitedNodes.Count;
                entered = state.Encounter is { Ended: false } ? done : entered;
            }

            if (done == game.Commands.Count)
            {
                break;
            }

            var applied = GameEngine.Apply(state, game.Commands[done], context.Value);
            if (!applied.IsSuccess)
            {
                return applied.Error!;
            }

            state = applied.Value;
        }

        if (entered < 0)
        {
            return Errors.NoEncounterPlayed;
        }

        var copy = game with { Id = Guid.NewGuid().ToString("N"), CreatedAt = time.GetUtcNow(), Commands = game.Commands.Take(entered).ToList() };
        return await ReplayAsync(copy, cancellationToken);
    }

    private async Task<Game> SaveAsync(Game game, CancellationToken cancellationToken, bool touch = true)
    {
        var saved = game with { EngineVersion = GameEngine.Version, UpdatedAt = touch ? time.GetUtcNow() : game.UpdatedAt };
        await games.SaveAsync(saved, cancellationToken);
        foreach (var observer in observers)
        {
            await observer.GameChangedAsync(saved, cancellationToken);
        }

        return saved;
    }

    private async Task<Result<GameContext>> ContextAsync(string scenarioId, int seed, GameOptions? options, CancellationToken cancellationToken)
    {
        if (await content.GetScenarioAsync(scenarioId, cancellationToken) is not { } scenario)
        {
            return Errors.ScenarioNotFound(scenarioId);
        }

        // The Bounties its Story Rounds may send the party to (rulebook p. 13): loaded only when it has some.
        var bounties = new Dictionary<string, Scenario>();
        if (scenario.Nodes.Any(n => n.IsStoryRound && n.BountyDifficulty is not null))
        {
            foreach (var summary in (await content.ListScenariosAsync(cancellationToken)).Where(s => s.Kind == "bounty" && s.Id != scenario.Id))
            {
                if (await content.GetScenarioAsync(summary.Id, cancellationToken) is { } bounty)
                {
                    bounties[bounty.Id] = bounty;
                }
            }
        }

        return new GameContext(scenario, await catalogs.GetAsync(cancellationToken), new SeededRandomSource(seed)) { Bounties = bounties, Options = options };
    }
}
