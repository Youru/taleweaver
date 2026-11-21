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
        new(game.Id, game.Scenario, game.Party, game.CreatedAt, game.UpdatedAt, game.Commands.Count > 0, game.State);
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
    public static class Errors
    {
        public static Error NotFound(string id) => Error.NotFound("Games.NotFound", $"No game '{id}'.");

        public static Error ScenarioNotFound(string id) => Error.NotFound("Games.ScenarioNotFound", $"No scenario '{id}'.");

        public static readonly Error NothingToUndo = Error.Conflict("Games.NothingToUndo", "There is no action to undo.");
    }

    public async Task<Result<Game>> StartAsync(string scenarioId, IReadOnlyList<string> party, CancellationToken cancellationToken)
    {
        var seed = Random.Shared.Next();
        var context = await ContextAsync(scenarioId, seed, cancellationToken);
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

    public async Task<Result<Game>> ExecuteAsync(string id, GameCommand command, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.Error!;
        }

        var game = loaded.Value;
        var context = await ContextAsync(game.Scenario, game.Seed, cancellationToken);
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

        var context = await ContextAsync(loaded.Value.Scenario, loaded.Value.Seed, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        var state = GameEngine.Apply(loaded.Value.State, command, context.Value);
        return state.IsSuccess ? Result.Success() : state.Error!;
    }

    /// <summary>Undo = replay the history without its last command.</summary>
    public async Task<Result<Game>> UndoAsync(string id, CancellationToken cancellationToken)
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
        var context = await ContextAsync(game.Scenario, game.Seed, cancellationToken);
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

    private async Task<Result<GameContext>> ContextAsync(string scenarioId, int seed, CancellationToken cancellationToken)
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

        return new GameContext(scenario, await catalogs.GetAsync(cancellationToken), new SeededRandomSource(seed)) { Bounties = bounties };
    }
}
