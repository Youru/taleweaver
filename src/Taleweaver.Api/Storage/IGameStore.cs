using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Storage;

/// <summary>Saved games. Works on <see cref="Game"/> only, so that the files can later become a database.</summary>
public interface IGameStore
{
    Task<Game?> GetAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Game>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates or replaces the game with the same id.</summary>
    Task SaveAsync(Game game, CancellationToken cancellationToken);

    /// <returns>False when there was no such game.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
