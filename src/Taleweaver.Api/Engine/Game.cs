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

    /// <summary>The lines in the journal before each command (-1: played before they were counted).</summary>
    public IReadOnlyList<int> LogMarks { get; init; } = [];

    /// <summary>What is of the table rather than of the rules: never undone, never replayed.</summary>
    public GameTable Table { get; init; } = new();
}

/// <summary>The state of a game after its first <see cref="CommandCount"/> commands.</summary>
public sealed record Checkpoint(int CommandCount, int EngineVersion, GameState State);

/// <summary>The evenings played, the GM's notes and rulings, who plays whom, the pause (play-spec M4, M6, M16, M17).</summary>
public sealed record GameTable
{
    public IReadOnlyList<PlaySession> Sessions { get; init; } = [];

    /// <summary>The last command: a longer silence opens a new session.</summary>
    public DateTimeOffset? PlayedAt { get; init; }

    public IReadOnlyList<TableNote> Notes { get; init; } = [];

    public IReadOnlyList<TablePlayer> Players { get; init; } = [];

    public bool Paused { get; init; }
}

/// <summary>An evening, and the line of the journal it began at.</summary>
public sealed record PlaySession(DateTimeOffset At, int Line);

public sealed record TableNote(string Id, DateTimeOffset At, string Kind, string Text, int? Page = null)
{
    public const string Note = "note";
    public const string Ruling = "ruling";
}

/// <summary>An adventurer's player, absent or not, and who plays them tonight when someone else does.</summary>
public sealed record TablePlayer(string Adventurer, string? Player = null, bool Present = true, string? Tonight = null);
