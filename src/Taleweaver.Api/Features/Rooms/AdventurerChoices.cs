using System.Collections.Concurrent;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>
/// "Choose your adventurer" from a phone (roadmap §5.3): the engine checks the rules and nothing starts before the GM
/// sets out, so a choice needs no approval. Choices of one game go one after the other: two phones cannot hold one
/// adventurer.
/// </summary>
public static class AdventurerChoices
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public static readonly Error NotPreparing = Error.Conflict("Games.SetOut", "The party has already set out.");

    public static async Task<Result> ApplyAsync(
        Room room, Seat seat, PlayerCommand command, GameRunner runner, RoomService rooms, CatalogProvider catalogs, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(room.GameId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var game = await runner.LoadAsync(room.GameId, cancellationToken);
            if (!game.IsSuccess)
            {
                return game.Error!;
            }

            if (game.Value.State.Preparation is not { } preparation)
            {
                return NotPreparing;
            }

            return command.Type == PlayerCommand.ReleaseAdventurer
                ? await ReleaseAsync(room, seat, command.Adventurer, preparation, runner, rooms, cancellationToken)
                : await ChooseAsync(room, seat, command, preparation, runner, rooms, catalogs, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<Result> ChooseAsync(
        Room room, Seat seat, PlayerCommand command, PartyPreparation preparation, GameRunner runner, RoomService rooms, CatalogProvider catalogs, CancellationToken cancellationToken)
    {
        var named = PartyRules.NameIn(preparation, await catalogs.GetAsync(cancellationToken), command.Adventurer);
        if (!named.IsSuccess)
        {
            return named.Error!;
        }

        var name = named.Value;
        if (room.Seats.Any(s => s.Id != seat.Id && s.Adventurers.Contains(name)))
        {
            return PlayerCommands.Errors.AdventurerTaken(name);
        }

        // One the party already has, taken as it is: nothing for the history
        var changes = command.Kit is not null || command.Items is not null || command.Disciplines is not null || preparation.Choices.All(c => c.Adventurer != name);
        var party = preparation.Choices.Select(c => c.Adventurer).ToList();
        if (changes)
        {
            var engine = GameCommand.ChooseAdventurer(name, command.Kit, command.Items, command.Disciplines) with { By = seat.DisplayName };
            var chosen = await runner.ExecuteAsync(room.GameId, engine, cancellationToken);
            if (!chosen.IsSuccess)
            {
                return chosen.Error!;
            }

            party = chosen.Value.State.Campaign.Party.Select(a => a.Name).ToList();
        }

        if (seat.Adventurers.Contains(name))
        {
            return Result.Success();
        }

        var assigned = await rooms.AssignAsync(room.GameId, seat.Id, [.. seat.Adventurers, name], party, cancellationToken);
        return assigned.IsSuccess ? Result.Success() : assigned.Error!;
    }

    private static async Task<Result> ReleaseAsync(
        Room room, Seat seat, string? name, PartyPreparation preparation, GameRunner runner, RoomService rooms, CancellationToken cancellationToken)
    {
        if (name is null || !seat.Adventurers.Contains(name))
        {
            return RoomService.Errors.AdventurerNotAccessible(name ?? "");
        }

        // One the story imposes stays in the party: only the seat lets go
        if (preparation.Adventurers.Any(a => a.Required && a.Name == name))
        {
            var party = preparation.Choices.Select(c => c.Adventurer).ToList();
            var assigned = await rooms.AssignAsync(room.GameId, seat.Id, seat.Adventurers.Where(a => a != name).ToList(), party, cancellationToken);
            return assigned.IsSuccess ? Result.Success() : assigned.Error!;
        }

        // Out of the party, they leave the seat with it (RoomNotifier)
        var removed = await runner.ExecuteAsync(room.GameId, GameCommand.RemoveAdventurer(name) with { By = seat.DisplayName }, cancellationToken);
        return removed.IsSuccess ? Result.Success() : removed.Error!;
    }
}
