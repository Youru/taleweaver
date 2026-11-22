using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The adventurers' Health and Stamina (moteur version 9, rulebook p. 14, 18, 25, 37, 69): Damage tokens kept from
/// one encounter to the next, Defeat, the Prepare Phase, SP gained at each Refresh Phase, and Restore.
/// </summary>
internal sealed partial class Session
{
    /// <summary>Health and SP of the Adventurer Card when the catalogue does not know the adventurer (the usual values).</summary>
    private const int DefaultHealth = 12;

    private static readonly AdventurerStamina DefaultStamina = new(3, 5);

    private static bool IsAdventurer(string? figure) => figure?.StartsWith(AdventurerCard, StringComparison.Ordinal) == true;

    /// <summary>A party member from a figure ("adventurer:Rook") or a name.</summary>
    private AdventurerState? MemberOf(string? figure) => Member(IsAdventurer(figure) ? figure![AdventurerCard.Length..] : figure);

    /// <summary>Maximum HP, SP gained per turn and most SP held, from the card and what is equipped: kept on the state for the front.</summary>
    public void UpdateAdventurerStats()
    {
        foreach (var member in State.Campaign.Party)
        {
            var card = Catalog.FindAdventurer(member.Name);
            // In Conduit Form, SP are the Conduit card's; maximum HP stay the adventurer's (p. 72, 77)
            var stamina = member.ConduitForm is { } form ? AllyRules.StaminaOf(Catalog.FindCombatant(form)) : card?.Stamina ?? DefaultStamina;
            member.Health = (card?.Health ?? DefaultHealth) + member.Equipped.Sum(HealthBonus);
            member.StaminaRecovery = stamina.Recovery;
            member.StaminaMax = stamina.Max;
        }

        if (State.Encounter is { } encounter)
        {
            foreach (var ally in encounter.Allies)
            {
                ScaleAlly(ally);
            }

            encounter.AllyOptions = OptionsNow();
        }
    }

    private int HealthBonus(OwnedItem owned)
    {
        var item = Catalog.FindItem(owned.Item);
        var stats = owned.Flipped && item?.BackSide?.Stats is { } back ? back : item?.Stats;
        return stats?.Health ?? 0;
    }

    /// <summary>
    /// Prepare Phase (p. 14): Defeated adventurers, and those with more Damage than half their maximum HP, go down to
    /// half (rounded down); every adventurer of the encounter sets their SP to their Recovery value.
    /// </summary>
    private void PrepareAdventurers()
    {
        UpdateAdventurerStats();
        foreach (var member in ActiveParty)
        {
            var half = member.Health / 2;
            if (member.Defeated || member.Damage > half)
            {
                member.Damage = half;
            }

            member.Defeated = false;
            member.Stamina = member.StaminaRecovery;
        }

        Log("adventurersPrepared", args: Args(("adventurers", string.Join(", ", ActiveParty.Select(a => $"{a.Name} {a.Damage}/{a.Health}")))));
    }

    /// <summary>Refresh Phase (p. 37): SP gained up to the maximum, except on the adventurer's 1st turn of the encounter.</summary>
    private void RefreshStamina(string adventurer)
    {
        if (MemberOf(adventurer) is not { Defeated: false } member || Encounter.Round <= 1)
        {
            return;
        }

        member.Stamina = Math.Min(member.StaminaMax, member.Stamina + member.StaminaRecovery);
    }

    /// <summary>Damage dealt to an adventurer (negative: healed). Reaching the maximum HP defeats them (p. 69).</summary>
    private Result DamageAdventurer(string? figure, int amount)
    {
        if (MemberOf(figure) is not { } member)
        {
            return GameEngine.Errors.UnknownCombatant(figure);
        }

        if (member.Defeated && amount > 0)
        {
            return GameEngine.Errors.AdventurerDefeated(member.Name);
        }

        UpdateAdventurerStats();
        member.Damage = Math.Clamp(member.Damage + amount, 0, member.Health);
        if (amount < 0 && member.Defeated && member.Damage < member.Health)
        {
            // Healed out of a Defeat between encounters (the story heals them).
            member.Defeated = false;
        }

        Log("adventurerDamage", args: Args(("adventurer", member.Name), ("amount", Number(amount)), ("damage", Number(member.Damage)), ("health", Number(member.Health))));
        if (member.Damage >= member.Health && !member.Defeated)
        {
            DefeatAdventurer(member);
        }

        return Result.Success();
    }

    private Result DefeatAdventurerCommand(string? figure)
    {
        if (MemberOf(figure) is not { Defeated: false } member)
        {
            return GameEngine.Errors.UnknownCombatant(figure);
        }

        UpdateAdventurerStats();
        DefeatAdventurer(member);
        return Result.Success();
    }

    /// <summary>The adventurer the scenario or the GM names is Defeated (defeatAdventurer); an unknown one is only logged.</summary>
    private void DefeatNamedAdventurer(string? name)
    {
        if (MemberOf(name) is { Defeated: false } member)
        {
            UpdateAdventurerStats();
            DefeatAdventurer(member);
            return;
        }

        Log("adventurerDefeated", args: Args(("adventurer", name)));
        Fire(e => e.Trigger.On == "adventurerDefeated");
    }

    private Result SetStamina(string? figure, int value)
    {
        if (MemberOf(figure) is not { } member)
        {
            return GameEngine.Errors.UnknownCombatant(figure);
        }

        UpdateAdventurerStats();
        member.Stamina = Math.Clamp(value, 0, member.StaminaMax);
        Log("stamina", args: Args(("adventurer", member.Name), ("stamina", Number(member.Stamina)), ("max", Number(member.StaminaMax))));
        return Result.Success();
    }

    /// <summary>
    /// Being Defeated (p. 69): Damage up to the maximum HP (kept until the next Prepare Phase), Effects removed, the
    /// figure leaves the board and its Initiative Card the track. When every adventurer of the encounter is Defeated,
    /// the party loses it (p. 18).
    /// </summary>
    private void DefeatAdventurer(AdventurerState member)
    {
        member.Damage = member.Health;
        member.Defeated = true;
        // No Player Elimination: the next turn is played "as if they had not been Defeated", SP included
        var comesBack = Options.NoPlayerElimination && State.Encounter is { Ended: false };
        if (!comesBack)
        {
            member.Stamina = 0;
        }

        Log("adventurerDefeated", args: Args(("adventurer", member.Name)));
        if (State.Encounter is not { Ended: false } encounter)
        {
            return;
        }

        if (encounter.Adventurers.FirstOrDefault(a => a.Name == member.Name) is { } figure)
        {
            figure.Effects.Clear();
            figure.Position = null;
            figure.Defeated = true;
        }

        if (comesBack)
        {
            // Their card stays on the track: the Urgency token is the price
            AddUrgency(1);
            Fire(e => e.Trigger.On == "adventurerDefeated");
            return;
        }

        RemoveFromTrack(AdventurerCard + member.Name);
        Fire(e => e.Trigger.On == "adventurerDefeated");
        if (State.Encounter is { Ended: false } && ActiveParty.All(a => a.Defeated))
        {
            Log("partyDefeated");
            var lose = EncounterNode.EndConditions.FirstOrDefault(e => e.Kind == "lose" && Encounter.ActiveEndConditions.Contains(e.Id));
            if (lose is not null)
            {
                EndEncounter(lose.Id);
            }
        }
    }

    /// <summary>The 4th Urgency token defeats every adventurer (p. 18).</summary>
    private void DefeatEveryAdventurer()
    {
        UpdateAdventurerStats();
        foreach (var figure in State.Encounter?.Adventurers ?? [])
        {
            figure.Defeated = true;
        }

        foreach (var member in ActiveParty.Where(a => !a.Defeated))
        {
            member.Damage = member.Health;
            member.Defeated = true;
            member.Stamina = 0;
        }
    }

    /// <summary>No Player Elimination (p. 86): back on their turn, at half their HP; the GM places the figure near an ally.</summary>
    private void ReturnToPlay(string adventurer)
    {
        if (!Options.NoPlayerElimination || MemberOf(adventurer) is not { Defeated: true } member)
        {
            return;
        }

        member.Defeated = false;
        member.Damage = Math.Min(member.Damage, member.Health / 2);
        if (Encounter.Adventurers.FirstOrDefault(a => a.Name == member.Name) is { } figure)
        {
            figure.Defeated = false;
        }

        Log("adventurerReturns", args: Args(("adventurer", member.Name), ("damage", Number(member.Damage))));
    }

    private void RemoveFromTrack(string card)
    {
        var encounter = Encounter;
        var index = encounter.InitiativeTrack.IndexOf(card);
        if (index < 0)
        {
            return;
        }

        encounter.InitiativeTrack.RemoveAt(index);
        if (index <= encounter.CurrentTurn)
        {
            encounter.CurrentTurn--;
        }

        Log("initiativeRemoved", args: Args(("card", card)));
    }

    /// <summary>Restore Adventurers (p. 18): every Damage token and Effect removed. Flipped cards are left to the table.</summary>
    private void RestoreAdventurers()
    {
        RestoreAllies();
        foreach (var member in State.Campaign.Party)
        {
            member.Damage = 0;
            member.Defeated = false;
        }

        foreach (var figure in State.Encounter?.Adventurers ?? [])
        {
            figure.Effects.Clear();
            figure.Defeated = false;
        }
    }
}
