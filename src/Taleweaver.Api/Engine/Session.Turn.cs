using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// A turn as the GM keeps it (play-spec lot 3): the Status Phase (p. 37, 95), what actions cost (p. 38), the reactions
/// that interrupt it (p. 42, 49, 70), and an AI step none of whose instructions is possible (p. 60).
/// </summary>
internal sealed partial class Session
{
    private const string Poison = "poison";
    private const string Condemn = "condemn";
    private const string Haste = "haste";
    private const string Disease = "disease";
    private const string TetherEffect = "tether";
    private const string Tentacle = "tentacle";
    private const string ApplyOption = "apply";
    private const string SkipOption = "skip";
    private const string SkullOption = "skull";

    private static readonly Dictionary<string, int> ActionCosts = new()
    {
        ["move"] = 1, ["extraMove"] = 1, ["attack"] = 2, ["encounterAction"] = 1, ["reequip"] = 1,
        ["dodge"] = 1, ["empower"] = 1, ["counter"] = 2, ["counterDodged"] = 1,
    };

    private static class TurnErrors
    {
        public static readonly Error Reacting = Error.Conflict("Encounters.Reacting", "A reaction is under way: end it, the interrupted turn then resumes.");

        public static readonly Error NoReaction = Error.Conflict("Encounters.NoReaction", "No reaction is under way.");

        public static readonly Error NothingToTakeBack = Error.Conflict("Encounters.NoStep", "No AI step to take back.");

        public static Error BreakAttackDone(string figure) =>
            Error.Conflict("Encounters.BreakAttackDone", $"{figure} already made a Break Attack this turn.");

        public static Error Tethered(string figure) => Error.Conflict("Encounters.Tethered", $"{figure} cannot be Tethered that way.");
    }

    // ---- Status Phase ----

    private void AskStatuses(IEnumerable<(string Figure, List<string> Effects)> figures)
    {
        var asked = figures.SelectMany(f => f.Effects
            .Where(e => e is Poison or Condemn || (e == Haste && (IsAdventurer(f.Figure) || IsAlly(f.Figure))))
            .Select(e => new ScenarioAction { Do = InternalActions.AskStatus, Instance = f.Figure, Effects = [e] })).ToList();
        Push(asked);
    }

    private void AskStatus(ScenarioAction action)
    {
        var (figure, effect) = (action.Instance!, action.Effects![0]);
        if (State.Encounter is not { Ended: false } || EffectsOf(figure)?.Contains(effect) != true || IsDefeated(figure))
        {
            return;
        }

        var name = IsAdventurer(figure) ? AdventurerOfCard(figure) : IsAlly(figure) ? NameOf(FindAlly(figure)!) : figure;
        var (question, options) = effect switch
        {
            Poison => (Text($"{name}, Poison: how much does the purple die deal?", $"{name}, Poison : combien de dégâts au dé violet ?"),
                new[] { ApplyOption, SkipOption }),
            Condemn => (Text($"{name}, Condemn: is the black die the Skull?", $"{name}, Condemn : le dé noir donne-t-il le crâne ?"),
                new[] { SkipOption, SkullOption }),
            _ => (Text($"{name}, Haste: +1 SP?", $"{name}, Haste : +1 SP ?"), new[] { ApplyOption, SkipOption }),
        };
        Ask(PromptKinds.Status, question, options.Select(o => new PromptOption(o)), action, figure);
    }

    private Result AnswerStatus(Prompt prompt, GameCommand command)
    {
        var (figure, effect) = (prompt.Instance!, prompt.Action!.Effects![0]);
        Encounter.LastStep = null;
        switch (effect)
        {
            case Poison when command.Option == ApplyOption:
                if (command.Value is not >= 0)
                {
                    State.Prompt = prompt;
                    return GameEngine.Errors.InvalidAnswer(command.Option);
                }

                return Deal(figure, command.Value.Value, fromOpponent: false, targeted: false);
            case Condemn when command.Option == SkullOption:
                return IsAdventurer(figure) ? DefeatAdventurerCommand(figure) : IsAlly(figure) ? DefeatAllyCommand(figure) : Defeat(figure);
            case Haste when command.Option == ApplyOption:
                return IsAlly(figure) && FindAlly(figure) is { } ally ? SetAllyStamina(figure, ally.Stamina + 1)
                    : MemberOf(figure) is { } member ? SetStamina(figure, member.Stamina + 1)
                    : Result.Success();
        }

        return Result.Success();
    }

    private bool IsDefeated(string figure) =>
        IsAdventurer(figure) ? MemberOf(figure)?.Defeated != false
        : IsAlly(figure) ? FindAlly(figure) is null
        : Find(figure)?.Defeated != false;

    // ---- Damage, as the rules of Effects see it ----

    /// <summary>Damage (negative: healed) on any figure, with Disease (no healing) and Tether (copied once).</summary>
    private Result Deal(string? figure, int amount, bool? fromOpponent = null, int tokens = 0, bool targeted = true)
    {
        var fighting = State.Encounter is { Ended: false };
        if (fighting && amount < 0 && EffectsOf(figure)?.Contains(Disease) == true)
        {
            Log("healBlocked", args: Args(("instance", figure)));
            return Result.Success();
        }

        var tethered = fighting && figure is not null && Encounter.Tethers?.GetValueOrDefault(figure) is { } linked ? linked : null;
        var result = DealToOne(figure, amount, fromOpponent, tokens, targeted);
        if (!result.IsSuccess || tethered is null || amount == 0 || State.Encounter is not { Ended: false } || IsDefeated(tethered)
            || (amount < 0 && EffectsOf(tethered)?.Contains(Disease) == true))
        {
            return result;
        }

        Log("tetherCopied", args: Args(("instance", figure), ("target", tethered), ("amount", Number(amount))));
        return DealToOne(tethered, amount, fromOpponent: false, tokens: 0, targeted: false);
    }

    private Result DealToOne(string? figure, int amount, bool? fromOpponent, int tokens, bool targeted) =>
        IsAdventurer(figure) ? DamageAdventurer(figure, amount)
        : IsAlly(figure) ? DamageAlly(figure, amount, fromOpponent, tokens)
        : ApplyDamage(figure, amount, targeted);

    /// <summary>The placer's Damage and healing are copied on the Tethered figure; one Tether each, never both ways.</summary>
    private Result Tether(string? placer, string? target)
    {
        if (EffectsOf(placer) is null)
        {
            return GameEngine.Errors.UnknownFigure(placer);
        }

        var tethers = Encounter.Tethers ?? [];
        if (tethers.Remove(placer!, out var before))
        {
            EffectsOf(before)?.Remove(TetherEffect);
        }

        if (target is not null)
        {
            if (EffectsOf(target) is not { } effects)
            {
                return GameEngine.Errors.UnknownFigure(target);
            }

            if (target == placer || tethers.GetValueOrDefault(target) == placer)
            {
                return TurnErrors.Tethered(target);
            }

            foreach (var other in tethers.Where(t => t.Value == target).Select(t => t.Key).ToList())
            {
                tethers.Remove(other);
            }

            tethers[placer!] = target;
            effects.Remove(TetherEffect);
            effects.Add(TetherEffect);
        }

        Encounter.Tethers = tethers.Count > 0 ? tethers : null;
        Log(target is null ? "tetherRemoved" : "tethered", args: Args(("instance", placer), ("target", target)));
        return Result.Success();
    }

    /// <summary>The figure leaves play or loses its tokens: no link from it nor to it.</summary>
    private void CutTethers(string figure, bool placedByItToo = true)
    {
        if (State.Encounter?.Tethers is not { } tethers)
        {
            return;
        }

        foreach (var (placer, target) in tethers.ToList())
        {
            if (target == figure || (placedByItToo && placer == figure))
            {
                tethers.Remove(placer);
                EffectsOf(target)?.Remove(TetherEffect);
            }
        }

        State.Encounter.Tethers = tethers.Count > 0 ? tethers : null;
    }

    // ---- Actions ----

    private Result Spend(string? figure, string? action)
    {
        if (action is null || !ActionCosts.TryGetValue(action, out var cost))
        {
            return GameEngine.Errors.InvalidAnswer(action);
        }

        var ally = FindAlly(figure);
        var member = ally is null ? MemberOf(figure) : null;
        if (ally is null && member is null)
        {
            return GameEngine.Errors.UnknownFigure(figure);
        }

        var id = ally is null ? AdventurerCard + member!.Name : AllyPrefix + ally.Id;
        if (SquareOf(id) is { } square && TerrainAt(square) == "muck")
        {
            cost++;
        }

        if (action == "move")
        {
            if (Encounter.Moved.Contains(id))
            {
                Log("secondMove", args: Args(("instance", id)));
            }
            else
            {
                Encounter.Moved.Add(id);
            }
        }

        // The GM is the referee (P1): signalled, not refused
        var held = ally?.Stamina ?? member!.Stamina;
        if (cost > held)
        {
            Log("staminaShort", args: Args(("instance", id), ("action", action), ("cost", Number(cost)), ("held", Number(held))));
        }

        Log("spent", args: Args(("instance", id), ("action", action), ("cost", Number(cost))));
        return ally is null ? SetStamina(id, held - cost) : SetAllyStamina(id, held - cost);
    }

    // ---- Reactions ----

    private Result React(string? figure, string? kind, string? against)
    {
        var poppet = kind == "poppet";
        var reacting = poppet ? Encounter.Tokens.Any(t => !t.Removed && t.Id == figure) : EffectsOf(figure) is not null;
        if (!reacting)
        {
            return GameEngine.Errors.UnknownFigure(figure);
        }

        switch (kind)
        {
            case "counter" or "counterDodged":
                if (IsAdventurer(figure) || IsAlly(figure))
                {
                    var paid = Spend(figure, kind);
                    if (!paid.IsSuccess)
                    {
                        return paid;
                    }
                }

                break;
            case "break":
                if (Encounter.BreakAttacks.Contains(figure!))
                {
                    return TurnErrors.BreakAttackDone(figure!);
                }

                // Free (p. 42)
                Encounter.BreakAttacks.Add(figure!);
                break;
            case "redirect":
                if (Find(figure) is { } target)
                {
                    target.Attacked = true;
                }

                break;
            case "poppet":
                // No Combat Dice: 1 Magic Damage (p. 97)
                Log("poppetBreak", args: Args(("instance", against)));
                return Deal(against, 1);
            default:
                return GameEngine.Errors.InvalidAnswer(kind);
        }

        Encounter.Reactions.Add(new Reaction(kind!, figure!, against));
        Log("reaction", args: Args(("kind", kind), ("instance", figure), ("target", against)));
        return Result.Success();
    }

    private Result EndReaction()
    {
        var reactions = Encounter.Reactions;
        if (reactions.Count == 0)
        {
            return TurnErrors.NoReaction;
        }

        reactions.RemoveAt(reactions.Count - 1);
        if (reactions.Count == 0)
        {
            Log("turnResumes");
        }

        return Result.Success();
    }

    // ---- No instruction possible ----

    private void RememberStep(Activation activation, CombatantState figure) =>
        Encounter.LastStep = new LastAiStep
        {
            Before = JsonSerializer.Deserialize<Activation>(JsonSerializer.Serialize(activation, JsonDefaults.Options), JsonDefaults.Options)!,
            RoundHadTrueStep = Encounter.RoundLog.AiStepTrue,
            Aggro = figure.Aggro,
            Attacked = figure.Attacked,
        };

    /// <summary>"If none of the instructions can be performed, treat that condition as false": on to the next step.</summary>
    private Result NoInstruction()
    {
        if (Encounter.LastStep is not { } last || Find(last.Before.Figures.FirstOrDefault()) is not { Defeated: false } figure)
        {
            return TurnErrors.NothingToTakeBack;
        }

        Encounter.LastStep = null;
        last.Before.Step++;
        Encounter.Activation = last.Before;
        Encounter.RoundLog.AiStepTrue = last.RoundHadTrueStep;
        (figure.Aggro, figure.Attacked) = (last.Aggro, last.Attacked);
        Log("noInstruction", args: Args(("instance", figure.Instance)));
        return Result.Success();
    }

    // ---- Combatant tokens (p. 97) ----

    /// <summary>"If an opponent starts their turn on the same space as a Tentacle, they put the token on their card."</summary>
    private void TakeTentacles(string figure, List<string> effects, Cell? square)
    {
        if (square is not { } at)
        {
            return;
        }

        foreach (var token in Encounter.Tokens.Where(t => !t.Removed && t.Type is not null && TokenType(t.Type) == Tentacle && t.Area is { } area && area.Cells().Contains(at)))
        {
            token.Removed = true;
            var effect = Tentacle;
            for (var copy = 2; effects.Contains(effect); copy++)
            {
                effect = $"{Tentacle}-{copy}";
            }

            effects.Add(effect);
            Log("tentacleTaken", args: Args(("instance", figure)));
        }
    }

    private Result FlipToken(string? id)
    {
        if (Encounter.Tokens.FirstOrDefault(t => t.Id == id && !t.Removed) is not { } token)
        {
            return GameEngine.Errors.UnknownToken(id);
        }

        token.Flipped = !token.Flipped;
        Log("tokenFlipped", args: Args(("token", token.Key), ("id", token.Id)));
        return Result.Success();
    }
}
