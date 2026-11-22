using Taleweaver.Api.Common;

namespace Taleweaver.Api.Engine;

/// <summary>Cards Exhausted, Used or Flipped, Ability tokens (rulebook p. 37, 90, 95, table 8), free Re-Equips (p. 70).</summary>
internal sealed partial class Session
{
    private const string Exhausted = "exhausted";
    private const string Used = "used";
    private const string Flipped = "flipped";
    private const string SummonToken = "summon";

    private static class CardErrors
    {
        public static Error Unknown(string? card) => Error.NotFound("Cards.Unknown", $"No card '{card}' to mark.");
    }

    /// <summary>An item the party holds (by its id), or another card of an adventurer or an ally (by the catalogue's).</summary>
    private Result MarkCard(string? figure, string? card, string? mark, bool on)
    {
        if (mark is not (Exhausted or Used or Flipped))
        {
            return GameEngine.Errors.InvalidAnswer(mark);
        }

        if (string.IsNullOrWhiteSpace(card))
        {
            return CardErrors.Unknown(card);
        }

        if (FindOwned(card) is { } found)
        {
            var owned = found.Owned;
            _ = mark switch
            {
                Exhausted => owned.Exhausted = on,
                Used => owned.Used = on,
                _ => owned.Flipped = on,
            };
        }
        else
        {
            var ally = State.Encounter is not null ? FindAlly(figure) : null;
            var member = ally is null ? MemberOf(figure) : null;
            var list = (mark, ally, member) switch
            {
                (Exhausted, { } a, _) => a.ExhaustedCards,
                (Used, { } a, _) => a.UsedCards,
                (Exhausted, _, { } m) => m.ExhaustedCards,
                (Used, _, { } m) => m.UsedCards,
                (Flipped, _, { } m) => m.FlippedCards,
                _ => null,
            };
            if (list is null)
            {
                return CardErrors.Unknown(card);
            }

            list.Remove(card);
            if (on)
            {
                list.Add(card);
            }
        }

        Log("cardMarked", args: Args(("instance", figure), ("card", card), ("mark", mark), ("value", on ? "true" : "false")));
        return Result.Success();
    }

    private Result SetAbilityToken(string? figure, string? token, int value)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return GameEngine.Errors.UnknownToken(token);
        }

        var encounter = State.Encounter;
        var ally = encounter is null ? null : FindAlly(figure);
        var member = ally is null ? MemberOf(figure) : null;
        var opponent = ally is null && member is null && encounter is not null ? Find(figure) : null;
        if (ally is null && member is null && opponent is null)
        {
            return GameEngine.Errors.UnknownFigure(figure);
        }

        var count = Math.Max(0, value);
        var limit = Catalog.Packs.SelectMany(p => p.Rules?.AbilityTokens ?? []).LastOrDefault(t => t.Id == token)?.Limit;
        if (limit is { } most && count > most)
        {
            Log("tokenLimit", args: Args(("instance", figure), ("token", token), ("limit", Number(most))));
            count = most;
        }

        if (member is not null && token == SummonToken)
        {
            member.SummonTokens = count;
        }
        else
        {
            var tokens = ally?.AbilityTokens ?? member?.AbilityTokens ?? opponent?.AbilityTokens ?? [];
            tokens[token] = count;
            if (count == 0)
            {
                tokens.Remove(token);
            }

            var kept = tokens.Count > 0 ? tokens : null;
            if (ally is not null)
            {
                ally.AbilityTokens = kept;
            }
            else if (member is not null)
            {
                member.AbilityTokens = kept;
            }
            else
            {
                opponent!.AbilityTokens = kept;
            }
        }

        Log("abilityToken", args: Args(("instance", figure), ("token", token), ("value", Number(count))));
        return Result.Success();
    }

    /// <summary>Refresh Phase (p. 37).</summary>
    private static void UnexhaustOf(AdventurerState member)
    {
        member.ExhaustedCards.Clear();
        foreach (var owned in member.Equipped.Concat(member.Pack))
        {
            owned.Exhausted = false;
        }
    }

    /// <summary>
    /// An encounter begins: every card Un-Exhausted, "Per Encounter" Abilities back. A card in the Pack keeps its Used
    /// token until the Restore (p. 24).
    /// </summary>
    private void ReadyCardsForEncounter()
    {
        foreach (var member in State.Campaign.Party)
        {
            UnexhaustOf(member);
            member.UsedCards.Clear();
            foreach (var owned in member.Equipped)
            {
                owned.Used = false;
            }
        }

        foreach (var owned in State.Campaign.Stash.Concat(State.Campaign.Unassigned))
        {
            owned.Exhausted = false;
        }

        foreach (var ally in State.Campaign.KeptAllies)
        {
            ally.ExhaustedCards.Clear();
            ally.UsedCards.Clear();
        }
    }

    /// <summary>Restore (table 8): every mark and every Ability token gone.</summary>
    private void RestoreCards()
    {
        var campaign = State.Campaign;
        foreach (var member in campaign.Party)
        {
            UnexhaustOf(member);
            member.UsedCards.Clear();
            member.FlippedCards.Clear();
            member.AbilityTokens = null;
        }

        foreach (var owned in campaign.Party.SelectMany(a => a.Equipped.Concat(a.Pack)).Concat(campaign.Stash).Concat(campaign.Unassigned))
        {
            owned.Exhausted = false;
            owned.Used = false;
        }

        foreach (var ally in State.Encounter?.Allies ?? [])
        {
            ally.ExhaustedCards.Clear();
            ally.UsedCards.Clear();
            ally.AbilityTokens = null;
        }
    }

    private void OfferReequip(AdventurerState? member)
    {
        if (member is not null && !State.Campaign.FreeReequip.Contains(member.Name))
        {
            State.Campaign.FreeReequip.Add(member.Name);
        }
    }

    private Result Reequipped(string? figure)
    {
        if (MemberOf(figure) is not { } member)
        {
            return GameEngine.Errors.UnknownFigure(figure);
        }

        State.Campaign.FreeReequip.Remove(member.Name);
        Log("reequipped", args: Args(("adventurer", member.Name)));
        return Result.Success();
    }
}
