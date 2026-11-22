using FluentValidation;
using Taleweaver.Api.Common;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Games;

/// <summary>
/// Around the game (play-spec lot 5): going back to a line of the journal (M2), and what is of the table rather
/// than of the rules: sessions and notes (M4), who plays whom (M6), pause (M16), rulings (M17).
/// </summary>
public static class TableEndpoints
{
    public sealed record RevertRequest(int Actions, int OutOf);

    public sealed record NoteRequest(string Text, string Kind = TableNote.Note, int? Page = null);

    public sealed record PlayersRequest(IReadOnlyList<TablePlayer> Players);

    public sealed record PauseRequest(bool Paused);

    public sealed class NoteValidator : AbstractValidator<NoteRequest>
    {
        public NoteValidator()
        {
            RuleFor(r => r.Text).NotEmpty().MaximumLength(2000);
            RuleFor(r => r.Kind).Must(k => k is TableNote.Note or TableNote.Ruling).WithMessage("A note or a ruling.");
            RuleFor(r => r.Page).InclusiveBetween(1, 200);
        }
    }

    public sealed class PlayersValidator : AbstractValidator<PlayersRequest>
    {
        public PlayersValidator()
        {
            RuleFor(r => r.Players).NotNull().Must(p => p is null || p.Count <= 16).WithMessage("Too many players.");
            RuleForEach(r => r.Players).ChildRules(player =>
            {
                player.RuleFor(p => p.Adventurer).NotEmpty().MaximumLength(80);
                player.RuleFor(p => p.Player).MaximumLength(40);
                player.RuleFor(p => p.Tonight).MaximumLength(40);
            });
        }
    }

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id}/revert", async (string id, RevertRequest r, GameRunner runner, CancellationToken ct) =>
            (await runner.RevertAsync(id, r.Actions, r.OutOf, ct)).ToResponse()).WithName("RevertGame");
        group.MapPost("/{id}/session/notes", async (string id, NoteRequest r, GameRunner runner, CancellationToken ct) =>
            (await runner.ChangeTableAsync(id, (table, now) =>
                table with { Notes = [.. table.Notes, new TableNote(Guid.NewGuid().ToString("N"), now, r.Kind, r.Text.Trim(), r.Page)] }, ct)).ToResponse())
            .WithValidation<NoteRequest>().WithName("AddNote");
        group.MapDelete("/{id}/session/notes/{note}", async (string id, string note, GameRunner runner, CancellationToken ct) =>
            (await runner.ChangeTableAsync(id, (table, _) =>
                table with { Notes = table.Notes.Where(n => n.Id != note).ToList() }, ct)).ToResponse()).WithName("DeleteNote");
        group.MapPut("/{id}/session/players", async (string id, PlayersRequest r, GameRunner runner, CancellationToken ct) =>
            (await runner.ChangeTableAsync(id, (table, _) => table with { Players = r.Players.ToList() }, ct)).ToResponse())
            .WithValidation<PlayersRequest>().WithName("SetPlayers");
        group.MapPut("/{id}/session/pause", async (string id, PauseRequest r, GameRunner runner, CancellationToken ct) =>
            (await runner.ChangeTableAsync(id, (table, _) => table with { Paused = r.Paused }, ct)).ToResponse()).WithName("PauseGame");
    }
}
