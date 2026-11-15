namespace Taleweaver.Api.Engine;

/// <summary>
/// A saved game: how it started (scenario, party, seed) and every command since, plus the resulting state.
/// The state can always be rebuilt from the rest; it is kept to load the game without replaying it.
/// </summary>
public sealed record Game
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string Scenario { get; init; }

    public IReadOnlyList<string> EnabledPacks { get; init; } = [];

    public required IReadOnlyList<string> Party { get; init; }

    public int Seed { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<GameCommand> Commands { get; init; } = [];

    public required GameState State { get; init; }
}
