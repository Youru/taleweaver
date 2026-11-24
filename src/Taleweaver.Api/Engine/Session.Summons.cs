using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// What Abilities bring (rulebook p. 75-77): Summoning's Loyal Espers, Felled Afterlife's Enslaved Spirits, and
/// Conduit Summoning. Distances (SOI) and the Energy tokens are the table's: the GM vouches for them.
/// </summary>
internal sealed partial class Session
{
    private static class OptionKinds
    {
        public const string EarnConduit = "earnConduit";
        public const string Conduit = "conduit";
        public const string Exalted = "exalted";
    }

    private Result Summon(GameCommand command)
    {
        if (ActiveParty.FirstOrDefault(a => a.Name == command.Instance) is not { Defeated: false } member)
        {
            return AllyErrors.UnknownAdventurer(command.Instance);
        }

        if (command.Card is null || Catalog.FindCombatant(command.Card) is not { } card)
        {
            return AllyErrors.NotACommandCard(command.Card);
        }

        var esper = AllyRules.IsEsper(card);
        if (!esper && !(AllyRules.IsCommand(card) && card.Family == AllyRules.EnslavedSpirit))
        {
            return AllyErrors.NotACommandCard(card.Id);
        }

        var source = esper ? AllySources.Summoning : AllySources.FelledAfterlife;
        if (!AllyRules.Knows(member, esper ? AllyRules.Summoning : AllyRules.FelledAfterlife))
        {
            return AllyErrors.DisciplineMissing(member.Name);
        }

        if (ControlsOne(member))
        {
            return AllyErrors.OneAtATime(member.Name);
        }

        if (Encounter.UsedSummons.Contains($"{source}:{member.Name}"))
        {
            return AllyErrors.UsedThisEncounter(member.Name);
        }

        if (esper && !EsperAvailable(card))
        {
            return AllyErrors.EsperUnavailable(card.Id);
        }

        for (var i = 0; i < (esper ? 1 : 2); i++)
        {
            var ally = CreateAlly(card, member.Name, source);
            ally.Position = i == 0 ? command.To : null;
            EnterPlay(ally);
            Log("allySummoned", args: Args(("ally", NameOf(ally)), ("card", ally.Card), ("adventurer", member.Name)));
        }

        Encounter.UsedSummons.Add($"{source}:{member.Name}");
        if (esper)
        {
            State.Campaign.SpentEspers.Add(card.Id);
            member.SummonTokens++;
        }

        return Result.Success();
    }

    /// <summary>
    /// Exalted Summoning (rulebook p. 76): the adventurer uses an Exalted Esper's printed Ability, from their own
    /// SOI, once per encounter. Nothing comes onto the board; the GM applies what the card says (damage, effects).
    /// </summary>
    private Result ExaltedSummon(GameCommand command)
    {
        if (ActiveParty.FirstOrDefault(a => a.Name == command.Instance) is not { Defeated: false } member)
        {
            return AllyErrors.UnknownAdventurer(command.Instance);
        }

        if (Catalog.FindDiscipline(command.Card ?? "") is not { Tree: Discipline.ExaltedTree } card)
        {
            return AllyErrors.NotACommandCard(command.Card);
        }

        if (!AllyRules.Knows(member, AllyRules.ExaltedSummoning))
        {
            return AllyErrors.DisciplineMissing(member.Name);
        }

        if (Encounter.UsedSummons.Contains($"{OptionKinds.Exalted}:{member.Name}"))
        {
            return AllyErrors.UsedThisEncounter(member.Name);
        }

        Encounter.UsedSummons.Add($"{OptionKinds.Exalted}:{member.Name}");

        Log("exaltedSummoned", args: Args(("adventurer", member.Name), ("esper", card.Name["en"]), ("card", card.Id)));
        return Result.Success();
    }

    /// <summary>The GM brings an ally the table's way: a card, a rule the app does not play. No Discipline asked.</summary>
    private Result AddAlly(GameCommand command)
    {
        if (command.Card is null || Catalog.FindCombatant(command.Card) is not { } card || !AllyRules.IsCommand(card) || AllyRules.IsConduit(card))
        {
            return AllyErrors.NotACommandCard(command.Card);
        }

        string? owner = null;
        if (!string.IsNullOrEmpty(command.Instance))
        {
            if (Member(command.Instance) is not { } member)
            {
                return AllyErrors.UnknownAdventurer(command.Instance);
            }

            owner = member.Name;
        }

        var ally = CreateAlly(card, owner, AllySources.Story);
        ally.Position = command.To;
        EnterPlay(ally);
        Log("allyJoined", args: Args(("ally", NameOf(ally)), ("card", ally.Card), ("adventurer", owner)));
        return Result.Success();
    }

    /// <summary>
    /// The summoner sends an Esper away (p. 75): in their Status Phase, or (<see cref="GameCommand.Active"/>) to ignore
    /// the Damage dealt to them through it, and it then counts as Defeated.
    /// </summary>
    private Result DismissAlly(GameCommand command)
    {
        if (FindAlly(command.Instance) is not { } ally)
        {
            return AllyErrors.Unknown(command.Instance);
        }

        if (ally is not { Esper: true, Owner: not null })
        {
            return AllyErrors.NotSummoned(ally.Id);
        }

        if (command.Active == true)
        {
            DefeatAlly(ally);
            return Result.Success();
        }

        LeaveBoard(ally);
        Log("allyDismissed", args: Args(("ally", NameOf(ally)), ("adventurer", ally.Owner)));
        return Result.Success();
    }

    private Result EarnConduit(GameCommand command)
    {
        if (ActiveParty.FirstOrDefault(a => a.Name == command.Instance) is not { Defeated: false } member)
        {
            return AllyErrors.UnknownAdventurer(command.Instance);
        }

        if (!AllyRules.Knows(member, AllyRules.ConduitSummoning))
        {
            return AllyErrors.DisciplineMissing(member.Name);
        }

        if (command.Card is not { } card || member.Conduits.Contains(card) || !ConduitsDefeated().Contains(card))
        {
            return AllyErrors.NotACommandCard(command.Card);
        }

        member.Conduits.Add(card);
        Log("conduitEarned", args: Args(("adventurer", member.Name), ("card", card), ("name", Catalog.FindCombatant(card)?.Name)));
        return Result.Success();
    }

    /// <summary>Conduit Summoning's 1 SP: another card in front of the player, or their Adventurer Card again (no card given).</summary>
    private Result TakeConduitForm(GameCommand command)
    {
        if (ActiveParty.FirstOrDefault(a => a.Name == command.Instance) is not { Defeated: false } member)
        {
            return AllyErrors.UnknownAdventurer(command.Instance);
        }

        if (!AllyRules.Knows(member, AllyRules.ConduitSummoning))
        {
            return AllyErrors.DisciplineMissing(member.Name);
        }

        if (command.Card == member.ConduitForm)
        {
            return Result.Success();
        }

        if (command.Card is { } card && !member.Conduits.Contains(card))
        {
            return AllyErrors.NotEarned(card);
        }

        if (member.Stamina < 1)
        {
            return AllyErrors.NoStamina(member.Name);
        }

        member.Stamina--;
        member.ConduitForm = command.Card;
        UpdateAdventurerStats();
        member.Stamina = Math.Min(member.Stamina, member.StaminaMax);
        var name = command.Card is { } form ? Catalog.FindCombatant(form)?.Name.GetValueOrDefault("en") : null;
        Log(command.Card is null ? "conduitLeft" : "conduitForm", args: Args(("adventurer", member.Name), ("name", name)));
        return Result.Success();
    }

    private bool ControlsOne(AdventurerState member) =>
        LivingAllies().Any(a => a.Owner == member.Name && a.Source != AllySources.Story);

    private bool EsperAvailable(Combatant card) =>
        !State.Campaign.SpentEspers.Contains(card.Id) && LivingAllies().All(a => a.Card != card.Id);

    /// <summary>The Conduit cards of the opponents Defeated in this encounter: their own family's, else their Base's.</summary>
    private List<string> ConduitsDefeated()
    {
        var conduits = Catalog.Combatants.Where(AllyRules.IsConduit).ToLookup(c => c.Family, c => c.Id);
        return Encounter.Combatants.Where(c => c.Defeated)
            .SelectMany(c => conduits.Contains(c.Family) || BaseFamilyOf(c) is not { } family ? conduits[c.Family] : conduits[family])
            .Distinct()
            .ToList();
    }

    /// <summary>What each adventurer may do now, so that the GM's screen and the phones offer the same.</summary>
    private List<AllyOption> OptionsNow()
    {
        var options = new List<AllyOption>();
        if (Encounter.Ended)
        {
            return options;
        }

        foreach (var member in ActiveParty.Where(a => !a.Defeated))
        {
            var free = !ControlsOne(member);
            void Offer(string kind, IEnumerable<string> cards)
            {
                if (cards.ToList() is { Count: > 0 } list)
                {
                    options.Add(new AllyOption(member.Name, kind, list));
                }
            }

            if (free && AllyRules.Knows(member, AllyRules.Summoning) && !Encounter.UsedSummons.Contains($"{AllySources.Summoning}:{member.Name}"))
            {
                Offer(AllySources.Summoning, Catalog.Combatants.Where(c => AllyRules.IsEsper(c) && EsperAvailable(c)).Select(c => c.Id));
            }

            if (free && AllyRules.Knows(member, AllyRules.FelledAfterlife) && !Encounter.UsedSummons.Contains($"{AllySources.FelledAfterlife}:{member.Name}"))
            {
                Offer(AllySources.FelledAfterlife, Catalog.Combatants.Where(c => AllyRules.IsCommand(c) && c.Family == AllyRules.EnslavedSpirit).Select(c => c.Id));
            }

            // Not a Command Combatant: no limit of one at a time
            if (AllyRules.Knows(member, AllyRules.ExaltedSummoning) && !Encounter.UsedSummons.Contains($"{OptionKinds.Exalted}:{member.Name}"))
            {
                Offer(OptionKinds.Exalted, Catalog.Packs.SelectMany(p => p.Disciplines).Where(d => d.Tree == Discipline.ExaltedTree).Select(d => d.Id));
            }

            if (AllyRules.Knows(member, AllyRules.ConduitSummoning))
            {
                Offer(OptionKinds.EarnConduit, ConduitsDefeated().Except(member.Conduits));
                Offer(OptionKinds.Conduit, member.Conduits);
            }
        }

        return options;
    }
}
