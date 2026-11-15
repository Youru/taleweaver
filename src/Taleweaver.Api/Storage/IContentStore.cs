using Taleweaver.Api.Content;

namespace Taleweaver.Api.Storage;

/// <summary>
/// Game content: packs (catalogue) and scenarios. Works on domain models only, so that the JSON files
/// can later be replaced by a database without touching the features or the engine.
/// </summary>
public interface IContentStore
{
    /// <summary>All the packs available, enabled or not.</summary>
    Task<IReadOnlyList<Pack>> GetPacksAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScenarioSummary>> ListScenariosAsync(CancellationToken cancellationToken);

    Task<Scenario?> GetScenarioAsync(string id, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the scenario with the same id.</summary>
    Task SaveScenarioAsync(Scenario scenario, CancellationToken cancellationToken);
}

public sealed record ScenarioSummary(string Id, LocalizedText Title, string? Kind, IReadOnlyList<string> Packs);
