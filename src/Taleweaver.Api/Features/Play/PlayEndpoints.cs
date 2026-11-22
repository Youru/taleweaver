using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;
using Taleweaver.Api.Features.Games;

namespace Taleweaver.Api.Features.Play;

/// <summary>
/// What the GM keeps during play (play-spec lots 2 to 4): SP spent, reactions, cards and tokens, what is carried,
/// counters, the track dealt, opposing Espers. Each command returns the new game state.
/// </summary>
public static class PlayEndpoints
{
    public sealed record SpendRequest(string Figure, string Action);

    public sealed record ReactionRequest(string Figure, string Kind, string? Against = null);

    public sealed record CardRequest(string Figure, string Card, string Mark, bool Value);

    public sealed record TokenRequest(string Figure, string Token, int Value);

    public sealed record TetherRequest(string Figure, string? Target = null);

    public sealed record CarryRequest(string? Figure = null, bool Lost = false);

    public sealed record CounterRequest(int By);

    public sealed record CorrectionRequest(string Field, int? Value = null, string? Instance = null, string? Item = null, string? Text = null, string? Card = null, bool? Active = null);

    public sealed record SeveralRequest(IReadOnlyList<string> Figures, int? Amount = null, string? Effect = null, bool? Active = null);

    public sealed class SeveralValidator : AbstractValidator<SeveralRequest>
    {
        public SeveralValidator()
        {
            RuleFor(r => r.Figures).NotEmpty();
            RuleFor(r => r).Must(r => (r.Amount is null) != (r.Effect is null)).WithMessage("Damage, or an Effect.");
            RuleFor(r => r.Amount).InclusiveBetween(-99, 99).NotEqual(0);
        }
    }

    public sealed record ResolutionRequest(string Attacker, string Target, int Amount, Resolution Resolution, int? Tokens = null);

    public sealed class ResolutionValidator : AbstractValidator<ResolutionRequest>
    {
        private static readonly string[] Outcomes = [Resolution.Hit, Resolution.Miss, Resolution.Affected, Resolution.Resisted, Resolution.Backfire];

        public ResolutionValidator()
        {
            RuleFor(r => r.Attacker).NotEmpty();
            RuleFor(r => r.Target).NotEmpty();
            RuleFor(r => r.Amount).InclusiveBetween(0, 999);
            RuleFor(r => r.Tokens).InclusiveBetween(0, 9);
            RuleFor(r => r.Resolution).NotNull();
            RuleFor(r => r.Resolution.Outcome).Must(o => Outcomes.Contains(o)).When(r => r.Resolution is not null);
        }
    }

    public sealed record EsperRequest(string Card, string Summoner, Cell? At = null);

    public sealed class TokenValidator : AbstractValidator<TokenRequest>
    {
        public TokenValidator() => RuleFor(r => r.Value).InclusiveBetween(0, 99);
    }

    public sealed class CounterValidator : AbstractValidator<CounterRequest>
    {
        public CounterValidator() => RuleFor(r => r.By).NotEqual(0).InclusiveBetween(-999, 999);
    }

    public static IEndpointRouteBuilder MapPlayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games/{id}/play").WithTags("Play");
        group.MapPost("/actions", (string id, SpendRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Spend(r.Figure, r.Action), runner, ct)).WithName("SpendStamina");
        group.MapPost("/reactions", (string id, ReactionRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.React(r.Figure, r.Kind, r.Against), runner, ct)).WithName("React");
        group.MapPost("/resolutions", (string id, ResolutionRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Resolve(r.Attacker, r.Target, r.Amount, r.Resolution, r.Tokens), runner, ct)).WithValidation<ResolutionRequest>().WithName("Resolve");
        group.MapDelete("/reactions/current", (string id, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.EndReaction(), runner, ct)).WithName("EndReaction");
        group.MapPost("/no-instruction", (string id, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.NoInstruction(), runner, ct)).WithName("NoInstruction");
        group.MapPut("/cards", (string id, CardRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.MarkCard(r.Figure, r.Card, r.Mark, r.Value), runner, ct)).WithName("MarkCard");
        group.MapPut("/ability-tokens", (string id, TokenRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.AbilityToken(r.Figure, r.Token, r.Value), runner, ct)).WithValidation<TokenRequest>().WithName("SetAbilityToken");
        group.MapPut("/tethers", (string id, TetherRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Tether(r.Figure, r.Target), runner, ct)).WithName("SetTether");
        group.MapPost("/tokens/{token}/flip", (string id, string token, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.FlipToken(token), runner, ct)).WithName("FlipToken");
        group.MapPost("/reequips/{adventurer}", (string id, string adventurer, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Reequipped(adventurer), runner, ct)).WithName("Reequipped");
        group.MapPut("/carried/{thing}", (string id, string thing, CarryRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Carry(thing, r.Figure, r.Lost), runner, ct)).WithName("Carry");
        group.MapPost("/counters/{counter}", (string id, string counter, CounterRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Counter(counter, r.By), runner, ct)).WithValidation<CounterRequest>().WithName("MoveCounter");
        group.MapPost("/initiative/deal", (string id, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.DealInitiative(), runner, ct)).WithName("DealInitiative");
        group.MapPost("/espers", (string id, EsperRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.SummonEsper(r.Card, r.Summoner, r.At), runner, ct)).WithName("SummonEsper");
        group.MapPost("/corrections", (string id, CorrectionRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Correct(r.Field, r.Value, r.Instance, r.Item, r.Text, r.Card, r.Active), runner, ct)).WithName("Correct");
        group.MapPost("/ready", (string id, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Ready(), runner, ct)).WithName("Ready");
        group.MapPost("/several", (string id, SeveralRequest r, GameRunner runner, CancellationToken ct) =>
            Run(id, GameCommand.Several(r.Figures, r.Amount, r.Effect, r.Active), runner, ct)).WithValidation<SeveralRequest>().WithName("SeveralFigures");
        return app;
    }

    private static async Task<Results<Ok<GameView>, ProblemHttpResult>> Run(string id, GameCommand command, GameRunner runner, CancellationToken cancellationToken) =>
        (await runner.ExecuteAsync(id, command, cancellationToken)).ToResponse();
}
