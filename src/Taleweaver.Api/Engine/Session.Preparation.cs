using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Before the departure (roadmap §5.2, §5.3): who comes and with what. Each change is a command, so the room works
/// already, undo works, and the game replays. The party is rebuilt from the choices after each of them.
/// </summary>
internal sealed partial class Session
{
    private static class PreparationErrors
    {
        public static readonly Error Preparing = Error.Conflict("Games.Preparing", "The party has not set out yet.");

        public static readonly Error SetOut = Error.Conflict("Games.SetOut", "The party has already set out.");
    }

    public void StartPreparation()
    {
        var preparation = new PartyPreparation();
        PartyRules.Describe(preparation, Campaign.Party, Catalog);
        preparation.Choices = preparation.Adventurers.Where(a => a.Required).Take(preparation.Max)
            .Select(a => NewChoice(preparation, a.Name)).ToList();
        PartyRules.Describe(preparation, Campaign.Party, Catalog);
        State.Preparation = preparation;
        RebuildParty();
    }

    /// <summary>An adventurer who has but one kit has nothing to choose: it is taken.</summary>
    private static PartyChoice NewChoice(PartyPreparation preparation, string name) =>
        new() { Adventurer = name, Kit = PartyRules.KitsFor(preparation, name) is [var only] ? only.Id : null };

    private Result ExecutePreparation(GameCommand command) => command.Type switch
    {
        GameCommands.ChooseAdventurer => ChooseAdventurer(command.Choice),
        GameCommands.RemoveAdventurer => RemoveAdventurer(command.Instance),
        GameCommands.ShowHiddenAdventurers => ShowHiddenAdventurers(command.Active ?? true),
        GameCommands.SetOut => SetOut(),
        _ => PreparationErrors.Preparing,
    };

    private Result ChooseAdventurer(PartyChoice? asked)
    {
        var preparation = State.Preparation!;
        var named = PartyRules.NameIn(preparation, Catalog, asked?.Adventurer);
        if (!named.IsSuccess)
        {
            return named.Error!;
        }

        var index = preparation.Choices.FindIndex(c => c.Adventurer == named.Value);
        var held = index >= 0 ? preparation.Choices[index] : NewChoice(preparation, named.Value);
        // A part not given is kept; an empty kit takes the kit away
        var chosen = held with
        {
            Kit = asked!.Kit is { } kit ? (kit.Length == 0 ? null : kit) : held.Kit,
            Items = asked.Items ?? held.Items,
            Disciplines = asked.Disciplines ?? held.Disciplines,
        };
        if (index >= 0)
        {
            preparation.Choices[index] = chosen;
        }
        else
        {
            preparation.Choices.Add(chosen);
        }

        return CheckedRebuild();
    }

    private Result RemoveAdventurer(string? name)
    {
        var preparation = State.Preparation!;
        if (preparation.Choices.FirstOrDefault(c => c.Adventurer == name) is not { } choice)
        {
            return PartyRules.Errors.NotOffered(name ?? "");
        }

        if (preparation.Adventurers.Any(a => a.Required && a.Name == choice.Adventurer))
        {
            return PartyRules.Errors.Required(choice.Adventurer);
        }

        preparation.Choices.Remove(choice);
        return CheckedRebuild();
    }

    private Result ShowHiddenAdventurers(bool shown)
    {
        State.Preparation!.HiddenShown = shown;
        return CheckedRebuild();
    }

    private Result CheckedRebuild()
    {
        var preparation = State.Preparation!;
        PartyRules.Describe(preparation, Campaign.Party, Catalog);
        if (PartyRules.Check(preparation, Catalog) is { } refused)
        {
            return refused;
        }

        RebuildParty();
        return Result.Success();
    }

    private void RebuildParty()
    {
        var preparation = State.Preparation!;
        var campaign = State.Campaign;
        campaign.Party = [];
        campaign.Items = [];
        campaign.Stash = [];
        campaign.ItemCount = 0;
        campaign.Gold = preparation.GoldLeft;
        foreach (var choice in preparation.Choices)
        {
            var member = new AdventurerState { Name = choice.Adventurer, Xp = preparation.Xp };
            campaign.Party.Add(member);
            var kit = preparation.Kits.FirstOrDefault(k => k.Id == choice.Kit);
            foreach (var item in kit?.Items ?? [])
            {
                GiveAtTheStart(member, item, "kit");
            }

            foreach (var item in choice.Items ?? [])
            {
                GiveAtTheStart(member, item, "start");
            }

            member.Disciplines = (kit?.Disciplines ?? []).Select(id => new LearnedDiscipline { Id = id }).ToList();
            foreach (var id in PartyRules.InOrder(Catalog, choice.Disciplines ?? []))
            {
                var cost = DisciplineRules.Cost(Catalog, member.Disciplines.Select(d => d.Id), Catalog.FindDiscipline(id)) ?? 0;
                member.Disciplines.Add(new LearnedDiscipline { Id = id, Xp = cost });
            }
        }
    }

    /// <summary>Equipped within the limits (p. 25), then in the Pack, then in the Party Stash.</summary>
    private void GiveAtTheStart(AdventurerState member, string id, string source)
    {
        var owned = new OwnedItem { Id = NewItemId(), Item = id, Source = source };
        State.Campaign.Items.Add(id);
        var list = Catalog.FindItem(id) is { } card && FitsEquipped(member, card) ? member.Equipped
            : member.Pack.Count < 3 ? member.Pack
            : State.Campaign.Stash;
        list.Add(owned);
    }

    private bool FitsEquipped(AdventurerState member, Item card)
    {
        var alike = member.Equipped.Select(o => Catalog.FindItem(o.Item)).Where(i => i?.Type == card.Type).ToList();
        return card.Type == "weapon"
            ? alike.Sum(i => i!.Stats?.Hands ?? 1) + (card.Stats?.Hands ?? 1) <= 2
            : alike.Count < (card.Type is "relic" or "consumable" ? 3 : 1);
    }

    private Result SetOut()
    {
        var preparation = State.Preparation!;
        PartyRules.Describe(preparation, Campaign.Party, Catalog);
        if ((PartyRules.Check(preparation, Catalog) ?? PartyRules.ToSetOut(preparation)) is { } refused)
        {
            return refused;
        }

        RebuildParty();
        State.Preparation = null;
        UpdateAdventurerStats();
        Log("setOut", args: Args(("adventurers", string.Join(", ", State.Campaign.Party.Select(a => a.Name)))));
        GoToNode(Campaign.Start);
        return Result.Success();
    }
}
