using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Items;

/// <summary>
/// An item is used: <c>Discard</c> it (a Consumable, X DISCARD), and place its token on the board when it says so
/// (Ubiquitous Plank: a Plank token on an adjacent space; <c>Token</c> = catalogue id, <c>At</c> = its square).
/// </summary>
public static class UseItem
{
    public sealed record Request(bool Discard, string? Token = null, Square? At = null);

    public sealed record Square(int X, int Y);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.At!.X).InclusiveBetween(0, 200).When(r => r.At is not null);
            RuleFor(r => r.At!.Y).InclusiveBetween(0, 200).When(r => r.At is not null);
            RuleFor(r => r.At).NotNull().When(r => r.Token is not null).WithMessage("Say on which square the token goes.");
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{item}/use", async Task<Results<Ok<GameView>, ProblemHttpResult>> (
                string id, string item, Request request, GameRunner runner, CancellationToken cancellationToken) =>
                (await runner.ExecuteAsync(id,
                    GameCommand.UseItem(item, request.Discard, request.Token, request.At is { } at ? new Cell(at.X, at.Y) : null),
                    cancellationToken)).ToResponse())
            .WithValidation<Request>()
            .WithName("UseItem");
}
