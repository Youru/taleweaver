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

    /// <summary>A number cut in half is always rounded up (p. 71).</summary>
    private static int HalfRoundedUp(int value) => (value + 1) / 2;

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
            member.Health = Math.Max(1, (card?.Health ?? DefaultHealth) + member.Equipped.Sum(HealthBonus) + DisciplineHealth(member));
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
        // Upgrading one side upgrades the other too (p. 27)
        return (stats?.Health ?? 0) + owned.Upgrades.Sum(id => Catalog.FindUpgrade(id)?.Health ?? 0);
    }

    /// <summary>The Health icons of the Disciplines learnt (p. 72).</summary>
    private int DisciplineHealth(AdventurerState member)
    {
        var known = member.Disciplines.Select(d => Catalog.FindDiscipline(d.Id)).OfType<Discipline>().ToList();
        // A Flipped Discipline cannot be used (p. 37): its icons with it
        known.RemoveAll(card => member.FlippedCards.Contains(card.Id));
        return known.Sum(card => (card.Health ?? 0)
            + (card.HealthPerHighestLevel is { } tree ? known.Where(d => d.Tree == tree).Max(d => d.Level) ?? 0 : 0));
    }

    /// <summary>
    /// Prepare Phase (p. 14): Defeated adventurers, and those with more Damage than half their maximum HP, go down to
    /// half (rounded up); every adventurer of the encounter sets their SP to their Recovery value.
    /// </summary>
    private void PrepareAdventurers()
    {
        UpdateAdventurerStats();
        foreach (var member in ActiveParty)
        {
            var half = HalfRoundedUp(member.Health);
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
        if (MemberOf(adventurer) is not { Defeated: false } member)
        {
            return;
        }

        var played = Encounter.HadFirstTurn;
        if (played is null ? Encounter.Round <= 1 : !played.Contains(member.Name))
        {
            played?.Add(member.Name);
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
        var revived = amount < 0 && member.Defeated && member.Damage < member.Health;
        Log("adventurerDamage", args: Args(("adventurer", member.Name), ("amount", Number(amount)), ("damage", Number(member.Damage)), ("health", Number(member.Health))));
        if (revived)
        {
            Revive(member);
        }
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

        // What follows from a fall is about who fell
        _author = member.Name;
        if (encounter.Adventurers.FirstOrDefault(a => a.Name == member.Name) is { } figure)
        {
            DropCarriedBy(AdventurerCard + member.Name, figure.Position);
            CutTethers(AdventurerCard + member.Name);
            member.AbilityTokens = null;
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

        // Every one of their Initiative Cards
        foreach (var card in encounter.InitiativeTrack.Where(c => IsAdventurer(c) && AdventurerOfCard(c) == member.Name).ToList())
        {
            RemoveFromTrack(card);
        }

        Fire(e => e.Trigger.On == "adventurerDefeated");
        if (State.Encounter is { Ended: false } && ActiveParty.All(a => a.Defeated))
        {
            Log("partyDefeated");
            LoseEncounter(BuiltInEnds.AllDefeated);
        }
    }

    /// <summary>The 4th Urgency token defeats every adventurer (p. 18).</summary>
    private void DefeatEveryAdventurer()
    {
        UpdateAdventurerStats();
        foreach (var figure in State.Encounter?.Adventurers ?? [])
        {
            DropCarriedBy(AdventurerCard + figure.Name, figure.Position);
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
        member.Damage = Math.Min(member.Damage, HalfRoundedUp(member.Health));
        if (Encounter.Adventurers.FirstOrDefault(a => a.Name == member.Name) is { } figure)
        {
            figure.Defeated = false;
        }

        Log("adventurerReturns", args: Args(("adventurer", member.Name), ("damage", Number(member.Damage))));
    }

    /// <summary>
    /// Healed out of a Defeat. During an encounter, "as though they were a Combatant Spawning" (p. 69): the figure to
    /// place again, its card on the track.
    /// </summary>
    private void Revive(AdventurerState member)
    {
        member.Defeated = false;
        if (State.Encounter is not { Ended: false } encounter || encounter.Adventurers.FirstOrDefault(a => a.Name == member.Name) is not { } figure)
        {
            return;
        }

        figure.Defeated = false;
        AddToTrack([AdventurerCard + member.Name]);
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

    /// <summary>Restore Adventurers (p. 18): every Damage token and Effect removed, every card Un-Flipped.</summary>
    private void RestoreAdventurers()
    {
        RestoreAllies();
        UnflipCards();
        RestoreCards();
        foreach (var member in State.Campaign.Party.Where(a => !a.Unselectable))
        {
            OfferReequip(member);
        }

        foreach (var figure in State.Encounter?.Adventurers ?? [])
        {
            CutTethers(AdventurerCard + figure.Name);
        }

        foreach (var member in State.Campaign.Party)
        {
            member.Damage = 0;
            if (member.Defeated)
            {
                Revive(member);
            }
        }

        foreach (var figure in State.Encounter?.Adventurers ?? [])
        {
            figure.Effects.Clear();
            figure.Defeated = false;
        }
    }
}
