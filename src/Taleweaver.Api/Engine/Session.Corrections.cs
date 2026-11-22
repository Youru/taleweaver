using System.Text.Json;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Engine;

/// <summary>What the GM puts right, and what eases the GM's hand (play-spec lot 5): M3, M9, M11, M13.</summary>
internal sealed partial class Session
{
    private static class CorrectionErrors
    {
        public static Error Invalid(string? field) => Error.Validation("Corrections.Invalid", $"'{field}' cannot be corrected that way.");

        public static Error UnknownNode(string? node) => Error.NotFound("Corrections.UnknownNode", $"No step '{node}' in the scenario being played.");

        public static Error UnknownFamily(string? family) => Error.NotFound("Corrections.UnknownFamily", $"No combatant '{family}'.");

        public static readonly Error CardPlaying = Error.Conflict("Corrections.CardPlaying", "The card playing stays on the track: end its turn first.");

        public static readonly Error NoFigure = Error.Validation("Encounters.NoFigure", "Choose at least one figure.");
    }

    /// <summary>The app did not foresee it: the GM writes the value. Nothing it would trigger is run, but a step's own start.</summary>
    private Result Correct(GameCommand command)
    {
        var field = command.Effect;
        var fighting = State.Encounter is { Ended: false };
        var value = command.Value ?? -1;
        string? from = null, to = null, about = null;
        switch (field)
        {
            case Corrections.Gold when value >= 0:
                (from, to) = (Number(State.Campaign.Gold), Number(value));
                State.Campaign.Gold = value;
                break;
            case Corrections.Xp when value >= 0:
                if (MemberOf(command.Instance) is not { } member)
                {
                    return PartyErrors.UnknownAdventurer(command.Instance);
                }

                (from, to, about) = (Number(member.Xp), Number(value), member.Name);
                member.Xp = value;
                break;
            case Corrections.Flag when !string.IsNullOrWhiteSpace(command.Item):
                about = command.Item.Trim();
                from = State.Campaign.Variables.TryGetValue(about, out var was) ? was.ToString() : null;
                to = command.Option?.Trim();
                if (string.IsNullOrEmpty(to))
                {
                    State.Campaign.Variables.Remove(about);
                    break;
                }

                State.Campaign.Variables[about] = to switch
                {
                    "true" => JsonSerializer.SerializeToElement(true),
                    "false" => JsonSerializer.SerializeToElement(false),
                    _ when int.TryParse(to, System.Globalization.CultureInfo.InvariantCulture, out var number) => JsonSerializer.SerializeToElement(number),
                    _ => JsonSerializer.SerializeToElement(to),
                };
                break;
            case Corrections.Urgency when fighting && value is >= 0 and <= 9:
                (from, to) = (Number(Encounter.Urgency), Number(value));
                Encounter.Urgency = value;
                break;
            case Corrections.NoUrgency when fighting && Encounter.Round > 0:
                Encounter.RoundLog.Waived = command.Active ?? true;
                to = Encounter.RoundLog.Waived ? "true" : "false";
                break;
            case Corrections.Round when fighting && Encounter.Round > 0 && value >= 1:
                (from, to) = (Number(Encounter.Round), Number(value));
                Encounter.Round = value;
                break;
            case Corrections.Track when fighting:
                (about, to) = (command.Card, command.Active == false ? "false" : "true");
                var changed = command.Active == false ? TakeOffTrack(command.Card) : PutOnTrack(command.Card);
                if (!changed.IsSuccess)
                {
                    return changed;
                }

                break;
            case Corrections.Spawn when fighting && command.Value is null or (>= 1 and <= 12):
                if (Catalog.Combatants.All(c => c.Family != command.Item))
                {
                    return CorrectionErrors.UnknownFamily(command.Item);
                }

                var count = command.Value ?? 1;
                (about, to) = (command.Item, Number(count));
                // The spawn's own events come before the AI question, asked again after
                State.Prompt = null;
                Spawn(new() { Do = "spawn", Family = command.Item, Count = count }, fromSetup: false);
                break;
            case Corrections.Node:
                return CorrectStep(command.Item);
            default:
                return CorrectionErrors.Invalid(field);
        }

        Log("corrected", args: Args(("field", field), ("about", about), ("from", from), ("to", to)));
        return Result.Success();
    }

    private Result CorrectStep(string? id)
    {
        if (Scenario.Nodes.FirstOrDefault(n => n.Id == id) is not { } node)
        {
            return CorrectionErrors.UnknownNode(id);
        }

        Log("corrected", args: Args(("field", Corrections.Node), ("from", State.Campaign.CurrentNode), ("to", node.Id)));
        State.Prompt = null;
        State.Abraxis = null;
        // Elsewhere than its Story Round, the round is left open behind: "@currentStoryRound" still comes back to it
        if (!node.IsStoryRound && State.Campaign.StoryRound is { } round)
        {
            round.Phase = StoryRoundPhases.SideQuest;
        }

        GoToNode(node.Id);
        return Result.Success();
    }

    private Result TakeOffTrack(string? card)
    {
        var track = Encounter.InitiativeTrack;
        var at = card is null ? -1 : track.IndexOf(card);
        if (at < 0)
        {
            return GameEngine.Errors.UnknownCard(card);
        }

        if (Encounter.Round > 0 && at == Encounter.CurrentTurn)
        {
            return CorrectionErrors.CardPlaying;
        }

        track.RemoveAt(at);
        if (Encounter.Round > 0 && at < Encounter.CurrentTurn)
        {
            Encounter.CurrentTurn--;
        }

        return Result.Success();
    }

    private Result PutOnTrack(string? card)
    {
        var known = card is not null && !Encounter.InitiativeTrack.Contains(card)
            && (Encounter.Adventurers.Any(a => AdventurerCard + a.Name == card)
                || LivingAllies().Any(a => a.TrackCard == card)
                || Alive().Any(c => c.InitiativeCards.Contains(card)));
        if (!known)
        {
            return GameEngine.Errors.UnknownCard(card);
        }

        Encounter.InitiativeTrack.Add(card!);
        return Result.Success();
    }

    /// <summary>"Ready" (p. 18): the rules read and everyone placed, the GM gives the track.</summary>
    private Result Ready()
    {
        if (Encounter.Round > 0)
        {
            return MechanicsErrors.AlreadyStarted;
        }

        if (!Encounter.TrackDealt)
        {
            DealTrack();
        }

        Encounter.Ready = true;
        Log("ready");
        return Result.Success();
    }

    /// <summary>The same Damage or Effect on each, as one command: one undo. One refused: none is touched.</summary>
    private Result Several(IReadOnlyList<string>? figures, string? effect, int amount, bool on)
    {
        if (figures is not { Count: > 0 })
        {
            return CorrectionErrors.NoFigure;
        }

        foreach (var figure in figures.Distinct())
        {
            // The last adventurer down may end the encounter: the rest no longer matters
            if (State.Encounter is not { Ended: false })
            {
                break;
            }

            var done = effect is null ? Deal(figure, amount) : SetEffect(figure, effect, on);
            if (!done.IsSuccess)
            {
                return done;
            }
        }

        return Result.Success();
    }
}

/// <summary>What <see cref="GameCommands.Correct"/> writes.</summary>
public static class Corrections
{
    public const string Gold = "gold";
    public const string Xp = "xp";
    public const string Flag = "flag";
    public const string Urgency = "urgency";
    public const string NoUrgency = "noUrgency";
    public const string Round = "round";
    public const string Track = "track";
    public const string Spawn = "spawn";
    public const string Node = "node";
}
