using System.Globalization;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Engine;

/// <summary>
/// An Attack or a Spell validated from the duel, as one action: what it cost, the target noted, its Damage. Its
/// journal line is what says that an AI instruction was resolved: nothing else is kept, so undo takes both back.
/// </summary>
internal sealed partial class Session
{
    private const int BackfireDamage = 3;

    private static class ResolutionErrors
    {
        public static readonly Error AlreadyResolved = Error.Conflict("Encounters.AlreadyResolved", "This AI action is resolved already.");

        public static Error UnknownStep(string step) => Error.Validation("Encounters.UnknownStep", $"'{step}' is not an instruction given to that figure.");
    }

    private Result Resolve(string? attacker, string? target, int amount, Resolution? resolution, int tokens)
    {
        if (resolution is null || ResolutionLine(resolution.Outcome, amount) is not { } code)
        {
            return GameEngine.Errors.InvalidAnswer(resolution?.Outcome);
        }

        if (EffectsOf(attacker) is null || IsDefeated(attacker!))
        {
            return GameEngine.Errors.UnknownFigure(attacker);
        }

        if (EffectsOf(target) is null || IsDefeated(target!))
        {
            return GameEngine.Errors.UnknownFigure(target);
        }

        if (resolution.Step is { } step)
        {
            // "line:part:occurrence", the line being the instruction's in the journal
            if (!int.TryParse(step.Split(':')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var line)
                || line >= State.Log.Count
                || State.Log[line] is not { Code: "aiInstruction", Args: { } given }
                || given.GetValueOrDefault("instance") != attacker)
            {
                return ResolutionErrors.UnknownStep(step);
            }

            if (State.Log.Skip(line + 1).Any(entry => entry.Args?.GetValueOrDefault("step") == step))
            {
                return ResolutionErrors.AlreadyResolved;
            }
        }

        var byParty = IsAdventurer(attacker) || IsAlly(attacker);
        _author = byParty ? AuthorOf(attacker) : null;
        if (resolution.Empower && byParty && Spend(attacker, "empower") is { IsSuccess: false } empower)
        {
            return empower;
        }

        if (resolution.Dodge && (IsAdventurer(target) || IsAlly(target)) && Spend(target, "dodge") is { IsSuccess: false } dodge)
        {
            return dodge;
        }

        Log(code, args: Args(("attacker", attacker), ("target", target), ("amount", Number(amount)), ("difference", Number(resolution.Difference)), ("step", resolution.Step)));

        var opposed = CampOf(attacker!) != CampOf(target!);
        // Hit or not, the target of an opponent counts for Aggro (Golden Rule #4)
        if (opposed && Find(target) is { Untargetable: false } figure)
        {
            figure.Attacked = true;
        }

        if (resolution.Outcome == Resolution.Backfire)
        {
            return Deal(attacker, BackfireDamage, fromOpponent: false, targeted: false);
        }

        return amount > 0 && resolution.Outcome is Resolution.Hit or Resolution.Affected
            ? Deal(target, amount, fromOpponent: opposed, tokens: tokens, targeted: opposed)
            : Result.Success();
    }

    private static string? ResolutionLine(string outcome, int amount) => outcome switch
    {
        Resolution.Hit => amount > 0 ? "attackResolved" : "attackBlocked",
        Resolution.Miss => "attackMissed",
        Resolution.Affected => "spellResolved",
        Resolution.Resisted => "spellResisted",
        Resolution.Backfire => "spellBackfired",
        _ => null,
    };

    private string CampOf(string figure) =>
        IsAdventurer(figure) || IsAlly(figure) ? "players" : Find(figure)?.Side ?? "opponents";
}
