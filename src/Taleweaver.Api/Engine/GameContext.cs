using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>What the engine reads but never changes: the scenario, the catalogue and the source of chance.</summary>
public sealed record GameContext(Scenario Scenario, Catalog Catalog, IRandomSource Random);
