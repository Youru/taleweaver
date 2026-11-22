using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// What the engine reads but never changes: the scenario, the catalogue and the source of chance.
/// <see cref="Bounties"/>: the Bounties a Story Round may send the party to (rulebook p. 13), by id.
/// </summary>
public sealed record GameContext(Scenario Scenario, Catalog Catalog, IRandomSource Random)
{
    public GameOptions? Options { get; init; }

    public IReadOnlyDictionary<string, Scenario> Bounties { get; init; } = new Dictionary<string, Scenario>();
}
