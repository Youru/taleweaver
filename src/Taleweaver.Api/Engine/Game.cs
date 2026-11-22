namespace Taleweaver.Api.Engine;

/// <summary>
/// A saved game: how it started (scenario, party, seed) and every command since, plus the resulting state.
/// The saved state is the reference: it is never recomputed by a newer engine, the history is replayed from a checkpoint only.
/// </summary>
public sealed record Game
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Version of the engine that computed <see cref="State"/> (0 in files saved before it was recorded).</summary>
    public int EngineVersion { get; init; }

    public required string Id { get; init; }

    public required string Scenario { get; init; }

    public IReadOnlyList<string> EnabledPacks { get; init; } = [];

    public required IReadOnlyList<string> Party { get; init; }

    public int Seed { get; init; }

    /// <summary>The optional rules chosen at the launch; null: none.</summary>
    public GameOptions? Options { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<GameCommand> Commands { get; init; } = [];

    public required GameState State { get; init; }

    /// <summary>The state found by a newer engine: the history before it is never replayed, nor undone.</summary>
    public Checkpoint? Baseline { get; init; }

    /// <summary>The last nodes entered: undo replays from them, whatever changed in the content before.</summary>
    public IReadOnlyList<Checkpoint> Checkpoints { get; init; } = [];

    /// <summary>The state on entering the last encounter, to play it again after an Ending.</summary>
    public Checkpoint? EncounterStart { get; init; }
}

/// <summary>The state of a game after its first <see cref="CommandCount"/> commands.</summary>
public sealed record Checkpoint(int CommandCount, int EngineVersion, GameState State);
