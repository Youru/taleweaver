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

    /// <summary>Deletes a scenario of the workshop (homebrew); false when there is none with this id.</summary>
    Task<bool> DeleteScenarioAsync(string id, CancellationToken cancellationToken);

    /// <summary>Forgets the packs read: a content pack was installed or removed.</summary>
    void Invalidate();
}

/// <param name="Editable">Made in the workshop (homebrew): it may be changed or deleted; official scenarios are read only.</param>
public sealed record ScenarioSummary(string Id, LocalizedText Title, string? Kind, IReadOnlyList<string> Packs, bool Editable = false);
