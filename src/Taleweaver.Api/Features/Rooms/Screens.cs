namespace Taleweaver.Api.Features.Rooms;

/// <summary>The two roles of a screen linked by QR code (room-spec §7.6). A phone is a seat, not a screen.</summary>
public static class ScreenRole
{
    /// <summary>The GM's tablet: runs this game (<see cref="ScreenAccess"/>).</summary>
    public const string Gm = "gm";

    /// <summary>Shows the table to the players: looks, never acts, never gets the game state.</summary>
    public const string Table = "table";
}

/// <summary>A QR code waiting to be scanned: good once, for a while.</summary>
public sealed record ScreenPairing(string Role, string TokenHash, DateTimeOffset ExpiresAt);

/// <summary>The screens as the GM sees them; <see cref="Waiting"/>: the roles whose QR code still waits.</summary>
public sealed record ScreensView(bool Gm, int Tables, IReadOnlyList<string> Waiting);

/// <summary>
/// The table's screen, by white list: what everybody around it sees. No seat, no item, no prompt, no journal, and
/// no combatant card (its AI is on it): the board draws standees.
/// </summary>
public sealed record TableScreenView(
    long Version,
    string GameId,
    LocalizedText? ScenarioTitle,
    IReadOnlyList<PartyMemberView> Party,
    PlayerEncounter? Encounter,
    string? Cards,
    bool Paused)
{
    public static TableScreenView Of(PlayerView view) => new(
        view.Version,
        view.GameId,
        view.ScenarioTitle,
        view.Party,
        view.Encounter is not { Ended: false } encounter
            ? null
            : encounter with
            {
                Combatants = encounter.Combatants.Select(c => c with { Image = null }).ToList(),
                Allies = encounter.Allies.Select(a => a with { Image = null }).ToList(),
                AllyOptions = [],
            },
        view.Cards,
        view.Paused);
}
