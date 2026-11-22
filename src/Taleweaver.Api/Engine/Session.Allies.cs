using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Command Combatants (rulebook p. 58): allies the players play, with their card on the track, their SP and their
/// Damage. Those of the story come by "spawn" or the setup; the Abilities' are in Session.Summons.cs.
/// </summary>
internal sealed partial class Session
{
    private const string AllyPrefix = "ally:";

    private static class AllyErrors
    {
        public static Error Unknown(string? ally) => Error.NotFound("Allies.Unknown", $"No ally '{ally}' in play.");

        public static Error NotACommandCard(string? card) => Error.Validation("Allies.NotACommandCard", $"'{card}' is not a card this can bring.");

        public static Error UnknownAdventurer(string? name) => Error.Validation("Allies.UnknownAdventurer", $"'{name}' is not playing this encounter.");

        public static Error DisciplineMissing(string name) => Error.Conflict("Allies.DisciplineMissing", $"{name} has not learnt the Discipline this takes.");

        public static Error OneAtATime(string name) => Error.Conflict("Allies.OneAtATime", $"{name} already controls a Command Combatant.");

        public static Error UsedThisEncounter(string name) => Error.Conflict("Allies.UsedThisEncounter", $"{name} already did it this encounter.");

        public static Error EsperUnavailable(string card) => Error.Conflict("Allies.EsperUnavailable", $"'{card}' was summoned since the last Restore.");

        public static Error NotSummoned(string ally) => Error.Conflict("Allies.NotSummoned", $"'{ally}' is not a Loyal Esper to dismiss.");

        public static Error NotEarned(string card) => Error.Conflict("Allies.NotEarned", $"Conduit '{card}' is not earned.");

        public static Error NoStamina(string name) => Error.Conflict("Allies.NoStamina", $"{name} has no SP left.");

        public static Error NoToken(string name) => Error.Conflict("Allies.NoToken", $"{name} has no Summon token left.");
    }

    private static bool IsAlly(string? figure) => figure?.StartsWith(AllyPrefix, StringComparison.Ordinal) == true;

    private AllyState? FindAlly(string? figure) =>
        IsAlly(figure) ? Encounter.Allies.FirstOrDefault(a => !a.Defeated && a.Id == figure![AllyPrefix.Length..]) : null;

    private IEnumerable<AllyState> LivingAllies() => Encounter.Allies.Where(a => !a.Defeated);

    private AllyState CreateAlly(Combatant card, string? owner, string source, string? id = null)
    {
        var name = id ?? card.Family;
        for (var copy = 2; id is null && Encounter.Allies.Any(a => !a.Defeated && a.Id == name); copy++)
        {
            name = $"{card.Family}-{copy}";
        }

        var stamina = AllyRules.StaminaOf(card);
        var ally = new AllyState
        {
            Id = name,
            Card = card.Id,
            Family = card.Family,
            Name = new Dictionary<string, string>(card.Name),
            TrackCard = AllyPrefix + (card.InitiativeCards.Count > 0 ? card.InitiativeCards[0] : card.Id),
            Owner = owner,
            Source = source,
            Esper = AllyRules.IsEsper(card),
            StaminaRecovery = stamina.Recovery,
            StaminaMax = stamina.Max,
            Stamina = stamina.Recovery,
        };
        ScaleAlly(ally);
        return ally;
    }

    /// <summary>In play: it takes the place of one of the same id, and its card joins the track as if just Spawned.</summary>
    private void EnterPlay(AllyState ally)
    {
        var encounter = Encounter;
        encounter.Allies.RemoveAll(a => a.Id == ally.Id);
        encounter.Allies.Add(ally);
        if (encounter.InitiativeTrack.Contains(ally.TrackCard))
        {
            return;
        }

        if (encounter.Round > 0 && encounter.CurrentTurn >= 0)
        {
            encounter.InitiativeTrack.Insert(encounter.CurrentTurn + 1, ally.TrackCard);
        }
        else
        {
            encounter.InitiativeTrack.Add(ally.TrackCard);
        }
    }

    /// <summary>The story's ally: back from the previous encounter, it healed as the adventurers do (p. 14, 58).</summary>
    private void SpawnAlly(ScenarioAction action, Combatant card, bool fromSetup, string? initiativeCard)
    {
        for (var i = 0; i < (action.Count ?? 1); i++)
        {
            var named = i == 0 ? action.Instance : null;
            var owner = Member(ExtraString(action, "adventurer"))?.Name;
            var ally = CreateAlly(card, owner, AllySources.Story, named);
            ally.TrackCard = initiativeCard is null ? ally.TrackCard : AllyPrefix + initiativeCard;
            if (Encounter.Allies.FirstOrDefault(a => a.Id == ally.Id) is { Defeated: false } standing)
            {
                (ally.Damage, ally.Stamina, ally.HasPlayed) = (standing.Damage, standing.Stamina, standing.HasPlayed);
                ally.Effects = standing.Effects;
            }
            else if (fromSetup && State.Campaign.AllyConditions.TryGetValue(ally.Id, out var before))
            {
                var half = (ally.Health ?? 0) / 2;
                ally.Damage = before.Defeated || before.Damage > half ? half : before.Damage;
            }

            ally.Figure = action.Figure;
            ally.Effects.AddRange((action.Effects ?? []).Except(ally.Effects));
            ally.Position = i == 0 ? Locate(action.At) : null;
            EnterPlay(ally);
            Log(fromSetup ? "allyPlaced" : "allyJoined", args: Args(("ally", NameOf(ally)), ("card", ally.Card), ("at", Json(action.At))));
        }
    }

    private static string NameOf(AllyState ally) => ally.Name.GetValueOrDefault("en") ?? ally.Id;

    private void BeginAllyTurn(AllyState ally)
    {
        if (ally.HasPlayed)
        {
            ally.Stamina = Math.Min(ally.StaminaMax, ally.Stamina + ally.StaminaRecovery);
        }

        ally.HasPlayed = true;
    }

    /// <summary>
    /// Damage (negative: healed). What an opponent deals a Loyal Esper is dealt to its summoner too (p. 75): unless
    /// said otherwise, that is where an Esper's Damage comes from. Each Summon token discarded first takes 2 off.
    /// </summary>
    private Result DamageAlly(string? figure, int amount, bool? fromOpponent, int tokens)
    {
        if (FindAlly(figure) is not { } ally)
        {
            return AllyErrors.Unknown(figure);
        }

        var summoner = ally.Esper ? Member(ally.Owner) : null;
        if (tokens > 0 && amount > 0)
        {
            if (summoner is null || summoner.SummonTokens < tokens)
            {
                return AllyErrors.NoToken(ally.Owner ?? ally.Id);
            }

            summoner.SummonTokens -= tokens;
            amount = Math.Max(0, amount - (2 * tokens));
            Log("summonToken", args: Args(("adventurer", summoner.Name), ("count", Number(tokens))));
        }

        ScaleAlly(ally);
        ally.Damage = Math.Max(0, ally.Damage + amount);
        if (ally.Health is { } most)
        {
            ally.Damage = Math.Min(ally.Damage, most);
        }

        Log("allyDamage", args: Args(("ally", NameOf(ally)), ("amount", Number(amount)), ("damage", Number(ally.Damage)), ("health", Number(ally.Health))));
        if (ally.Health is { } health && ally.Damage >= health)
        {
            DefeatAlly(ally);
        }

        return amount > 0 && (fromOpponent ?? true) && summoner is { Defeated: false }
            ? DamageAdventurer(AdventurerCard + summoner.Name, amount)
            : Result.Success();
    }

    private Result DefeatAllyCommand(string? figure)
    {
        if (FindAlly(figure) is not { } ally)
        {
            return AllyErrors.Unknown(figure);
        }

        DefeatAlly(ally);
        return Result.Success();
    }

    private void DefeatAlly(AllyState ally)
    {
        LeaveBoard(ally);
        Log("allyDefeated", args: Args(("ally", NameOf(ally))));
        // Only an event that names this ally: a family's is about the opponents
        Fire(e => e.Trigger.On == "combatantDefeated" && e.Trigger.Instance == ally.Id);
    }

    private void LeaveBoard(AllyState ally)
    {
        ally.Defeated = true;
        ally.Position = null;
        ally.Effects.Clear();
        if (LivingAllies().All(a => a.TrackCard != ally.TrackCard))
        {
            RemoveFromTrack(ally.TrackCard);
        }
    }

    private Result SetAllyStamina(string? figure, int value)
    {
        if (State.Encounter is not { Ended: false })
        {
            return GameEngine.Errors.NotInEncounter;
        }

        if (FindAlly(figure) is not { } ally)
        {
            return AllyErrors.Unknown(figure);
        }

        ally.Stamina = Math.Clamp(value, 0, ally.StaminaMax);
        Log("stamina", args: Args(("adventurer", NameOf(ally)), ("stamina", Number(ally.Stamina)), ("max", Number(ally.StaminaMax))));
        return Result.Success();
    }

    /// <summary>The Espers that stayed (p. 75): not healed, to place next to their summoner.</summary>
    private void BringKeptAllies()
    {
        var campaign = State.Campaign;
        foreach (var ally in campaign.KeptAllies.Where(k => ActiveParty.Any(a => a.Name == k.Owner)).ToList())
        {
            campaign.KeptAllies.Remove(ally);
            ally.Position = null;
            ally.HasPlayed = false;
            ally.Stamina = ally.StaminaRecovery;
            EnterPlay(ally);
            Log("allyStays", args: Args(("ally", NameOf(ally)), ("adventurer", ally.Owner)));
        }
    }

    /// <summary>The encounter is left: Espers stay, the story's allies keep their Damage, the rest is gone.</summary>
    private void LeaveEncounter()
    {
        if (State.Encounter is { } encounter)
        {
            foreach (var ally in encounter.Allies)
            {
                if (ally.Esper && ally.Owner is not null)
                {
                    if (!ally.Defeated)
                    {
                        ally.Effects.Clear();
                        State.Campaign.KeptAllies.Add(ally);
                    }
                }
                else if (ally.Source == AllySources.Story)
                {
                    State.Campaign.AllyConditions[ally.Id] = new AllyCondition(ally.Damage, ally.Defeated);
                }
            }
        }

        State.Encounter = null;
    }

    /// <summary>Restore (p. 18, 75): every Loyal Esper returns to the pile, the other allies lose Damage and Effects.</summary>
    private void RestoreAllies()
    {
        var campaign = State.Campaign;
        campaign.KeptAllies.Clear();
        campaign.SpentEspers.Clear();
        campaign.AllyConditions.Clear();
        foreach (var member in campaign.Party)
        {
            member.SummonTokens = 0;
        }

        if (State.Encounter is not { } encounter)
        {
            return;
        }

        foreach (var esper in encounter.Allies.Where(a => a is { Esper: true, Defeated: false, Owner: not null }).ToList())
        {
            LeaveBoard(esper);
            Log("allyDismissed", args: Args(("ally", NameOf(esper))));
        }

        encounter.Allies.RemoveAll(a => a.Esper && a.Owner is not null);
        foreach (var ally in encounter.Allies.Where(a => !a.Defeated))
        {
            ally.Damage = 0;
            ally.Effects.Clear();
        }
    }

    /// <summary>Scaling Icons (p. 72): "*" Health is the owner's maximum, Defeated or not.</summary>
    private void ScaleAlly(AllyState ally)
    {
        var owner = Member(ally.Owner);
        var printed = Catalog.FindCombatant(ally.Card)?.Stats?.Health;
        ally.Health = printed is { IsVariable: false } fixedHealth ? fixedHealth.Value : owner?.Health is > 0 ? owner.Health : null;
        ally.Force = owner is null ? null : AllyRules.ForceOf(Catalog, owner);
    }
}
