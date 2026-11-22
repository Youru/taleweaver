using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Storage;

namespace Taleweaver.Api.Features.Games;

/// <summary>Current state of a game, as returned by every game and encounter command.</summary>
public sealed record GameView(string Id, string Scenario, IReadOnlyList<string> Party, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool CanUndo, bool UndoBlocked, GameState State, GameTable Table, IReadOnlyList<int> Actions)
{
    public static GameView Of(Game game) =>
        new(game.Id, game.Scenario, PartyOf(game), game.CreatedAt, game.UpdatedAt,
            game.Commands.Count > (game.Baseline?.CommandCount ?? 0),
            game.Baseline is { CommandCount: > 0 } baseline && game.Commands.Count <= baseline.CommandCount,
            game.State,
            game.Table,
            ActionsOf(game));

    /// <summary>The journal line each action began at; -1: it cannot be gone back on.</summary>
    private static IReadOnlyList<int> ActionsOf(Game game) =>
        Enumerable.Range(0, game.Commands.Count)
            .Select(i => i < (game.Baseline?.CommandCount ?? 0) || i >= game.LogMarks.Count ? -1 : game.LogMarks[i])
            .ToList();

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

    /// <summary>How many entered nodes are kept: each is a whole state in the saved file.</summary>
    private const int KeptCheckpoints = 5;

    /// <summary>Longer without a command: another evening.</summary>
    private static readonly TimeSpan SessionGap = TimeSpan.FromHours(4);

    public static class Errors
    {
        public static readonly Error UndoBlocked = Error.Conflict("Games.UndoBlocked", "The app was updated since: what came before cannot be undone.");

        public static readonly Error HistoryChanged = Error.Conflict("Games.HistoryChanged", "The game went on meanwhile: choose the line again.");

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
        var state = GameEngine.Start(context.Value, party);
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
            State = state,
            EncounterStart = state.Encounter is { Ended: false } ? new Checkpoint(0, GameEngine.Version, state) : null,
            Table = new GameTable { Sessions = [new PlaySession(now, 0)], PlayedAt = now },
        };
        await games.SaveAsync(game, cancellationToken);
        return game;
    }

    /// <summary>
    /// A saved game. A state computed by an older engine is kept as it is and becomes the baseline of the game.
    /// Nothing is written: the next change saves it, one at a time with the others.
    /// </summary>
    public async Task<Result<Game>> LoadAsync(string id, CancellationToken cancellationToken)
    {
        if (await games.GetAsync(id, cancellationToken) is not { } game)
        {
            return Errors.NotFound(id);
        }

        return Upgrade(game);
    }

    private static Game Upgrade(Game game) => game.EngineVersion >= GameEngine.Version
        ? game
        : game with
        {
            EngineVersion = GameEngine.Version,
            Baseline = new Checkpoint(game.Commands.Count, game.EngineVersion, game.State),
            Checkpoints = [],
        };

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

        var changed = game with
        {
            Commands = [.. game.Commands, command],
            // A game begun before the marks: what came earlier has none
            LogMarks = [.. game.LogMarks, .. Enumerable.Repeat(-1, Math.Max(0, game.Commands.Count - game.LogMarks.Count)), game.State.Log.Count],
            Table = Playing(game),
            State = state.Value,
        };
        if (state.Value.Campaign.VisitedNodes.Count > game.State.Campaign.VisitedNodes.Count)
        {
            var entered = new Checkpoint(changed.Commands.Count, GameEngine.Version, state.Value);
            changed = changed with
            {
                Checkpoints = game.Checkpoints.Append(entered).TakeLast(KeptCheckpoints).ToList(),
                EncounterStart = state.Value.Encounter is { Ended: false } ? entered : game.EncounterStart,
            };
        }

        return await SaveAsync(changed, cancellationToken);
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
        var loaded = await LoadAsync(id, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var game = loaded.Value;
        if (game.Commands.Count == 0)
        {
            return Errors.NothingToUndo;
        }

        if (game.Commands.Count <= game.Baseline?.CommandCount)
        {
            return Errors.UndoBlocked;
        }

        return await ReplayAsync(game with { Commands = game.Commands.Take(game.Commands.Count - 1).ToList() }, cancellationToken);
    }

    /// <summary>Back to the first <paramref name="actions"/> commands, if the history still has the length the GM saw.</summary>
    public Task<Result<Game>> RevertAsync(string id, int actions, int outOf, CancellationToken cancellationToken) =>
        OneAtATimeAsync(id, async () =>
        {
            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.IsSuccess)
            {
                return loaded.Error!;
            }

            var game = loaded.Value;
            if (outOf != game.Commands.Count)
            {
                return Errors.HistoryChanged;
            }

            if (actions < 0 || actions >= outOf)
            {
                return Errors.NothingToUndo;
            }

            if (actions < game.Baseline?.CommandCount)
            {
                return Errors.UndoBlocked;
            }

            return await ReplayAsync(game with { Commands = game.Commands.Take(actions).ToList() }, cancellationToken);
        }, cancellationToken);

    /// <summary>Notes, players, pause: no command, the state stays as it is.</summary>
    public Task<Result<Game>> ChangeTableAsync(string id, Func<GameTable, DateTimeOffset, GameTable> change, CancellationToken cancellationToken) =>
        OneAtATimeAsync(id, async () =>
        {
            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.IsSuccess)
            {
                return loaded.Error!;
            }

            return await SaveAsync(loaded.Value with { Table = change(loaded.Value.Table, time.GetUtcNow()) }, cancellationToken);
        }, cancellationToken);

    private GameTable Playing(Game game)
    {
        var now = time.GetUtcNow();
        var table = game.Table;
        var resumes = table.Sessions.Count == 0 || now - (table.PlayedAt ?? game.UpdatedAt) > SessionGap;
        return table with { PlayedAt = now, Sessions = resumes ? [.. table.Sessions, new PlaySession(now, game.State.Log.Count)] : table.Sessions };
    }

    /// <summary>A game begun again keeps who plays and what was ruled, not the evenings nor the pause.</summary>
    private GameTable NewTable(Game game)
    {
        var now = time.GetUtcNow();
        return game.Table with { Sessions = [new PlaySession(now, 0)], PlayedAt = now, Paused = false };
    }

    /// <summary>An exported game under a new id: one of an older engine keeps the state it was exported with.</summary>
    public Task<Result<Game>> ImportAsync(Game game, CancellationToken cancellationToken) =>
        ReplayAsync(Upgrade(game) with { Id = Guid.NewGuid().ToString("N") }, cancellationToken);

    /// <summary>Rebuilds the state from the last checkpoint the history still holds, or from the start without one.</summary>
    private async Task<Result<Game>> ReplayAsync(Game game, CancellationToken cancellationToken)
    {
        var count = game.Commands.Count;
        game = game with
        {
            Checkpoints = game.Checkpoints.Where(c => c.CommandCount <= count).ToList(),
            LogMarks = game.LogMarks.Take(count).ToList(),
            EncounterStart = game.EncounterStart?.CommandCount <= count ? game.EncounterStart : null,
        };
        var from = game.Checkpoints.LastOrDefault() ?? game.Baseline;

        var context = await ContextAsync(game.Scenario, game.Seed, game.Options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var state = from is null
            ? GameEngine.Replay(context.Value, game.Party, game.Commands)
            : GameEngine.Replay(context.Value, from.State, game.Commands.Skip(from.CommandCount));
        if (!state.IsSuccess)
        {
            return state.Error!;
        }

        return await SaveAsync(game with { State = state.Value }, cancellationToken);
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
                var again = game with { Id = Guid.NewGuid().ToString("N"), Seed = Random.Shared.Next(), CreatedAt = now, Commands = game.Commands.Take(departure + 1).ToList(), Baseline = null, Checkpoints = [], EncounterStart = null, Table = NewTable(game) };
                return await ReplayAsync(again, cancellationToken);
            }

            return await StartAsync(game.Scenario, game.Party, game.Options, cancellationToken);
        }

        if (game.EncounterStart is { } start)
        {
            // Entered before an update: that state is where the new game's history begins to be replayable
            var resumed = game with
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = time.GetUtcNow(),
                Commands = game.Commands.Take(start.CommandCount).ToList(),
                Checkpoints = game.Checkpoints.Where(c => c.CommandCount < start.CommandCount).Append(start).ToList(),
                Baseline = start.CommandCount <= game.Baseline?.CommandCount ? start : game.Baseline,
                Table = NewTable(game),
            };
            return await ReplayAsync(resumed, cancellationToken);
        }

        var context = await ContextAsync(game.Scenario, game.Seed, game.Options, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        // No such checkpoint (a game saved before they existed): found again in the history
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

        var copy = game with { Id = Guid.NewGuid().ToString("N"), CreatedAt = time.GetUtcNow(), Commands = game.Commands.Take(entered).ToList(), Baseline = null, Checkpoints = [], EncounterStart = null, Table = NewTable(game) };
        return await ReplayAsync(copy, cancellationToken);
    }

    private async Task<Game> SaveAsync(Game game, CancellationToken cancellationToken)
    {
        var saved = game with { EngineVersion = GameEngine.Version, UpdatedAt = time.GetUtcNow() };
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
