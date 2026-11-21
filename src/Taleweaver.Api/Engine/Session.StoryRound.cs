using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Story Rounds (rulebook p. 13) and the party between encounters (p. 10-11): the three phases (Shop and Train,
/// Explore, Venture Forth), the Store's Unique Items drawn once per named Story Round, Side Quests that come back
/// with "@currentStoryRound", and the members of the party (Active Group, Injured, Unselectable, joining, leaving).
/// </summary>
internal sealed partial class Session
{
    /// <summary>Reserved target: back to the Story Round the party left for a Side Quest.</summary>
    public const string CurrentStoryRound = "@currentStoryRound";

    private const string ExploreOption = "explore";
    private const string QuestPrefix = "quest:";
    private const string VenturePrefix = "venture:";
    private const string BountyPrefix = "bounty:";

    private static class PartyErrors
    {
        public static Error UnknownAdventurer(string? name) => Error.NotFound("Party.UnknownAdventurer", $"No adventurer '{name}' in the party.");

        public static Error NotSelectable(string name) => Error.Conflict("Party.NotSelectable", $"{name} is Injured or Unselectable: not in the Active Group.");

        public static readonly Error EmptyGroup = Error.Validation("Party.EmptyGroup", "The Active Group needs at least one adventurer.");

        public static Error TooLarge(int count) => Error.Validation("Party.TooLarge", $"The Active Group has at most 4 adventurers, not {count}.");

        public static Error UnknownDiscipline(string? discipline) => Error.NotFound("Party.UnknownDiscipline", $"No Discipline '{discipline}'.");

        public static Error NotEnoughXp(string name, int cost, int left) =>
            Error.Conflict("Party.NotEnoughXp", $"The Discipline costs {cost} XP, {name} has {left} left.");

        public static Error AlreadyLearnt(string name, string discipline) =>
            Error.Conflict("Party.AlreadyLearnt", $"{name} already knows '{discipline}'.");

        public static Error MissingLevel(string name, string tree, int level) =>
            Error.Conflict("Party.MissingLevel", $"{name} needs a level {level} {tree} Discipline first.");

        public static Error NotEnoughGold(int price, int gold) => Error.Conflict("Party.NotEnoughGold", $"Re-specing costs {price} Gold, the party has {gold}.");

        public static Error UnknownStatus(string? status) => Error.Validation("Party.UnknownStatus", $"'{status}' is neither \"injured\" nor \"unselectable\".");
    }

    /// <summary>The adventurers who play the encounters: the Active Group, without the Injured and the Unselectable.</summary>
    private IEnumerable<AdventurerState> ActiveParty =>
        State.Campaign.Party.Where(a => !a.Injured && !a.Unselectable
                                        && (State.Campaign.ActiveGroup is not { } group || group.Contains(a.Name, StringComparer.OrdinalIgnoreCase)));

    private void EnterStoryRound(Node node, bool returning)
    {
        State.Campaign.StoryRound = new StoryRoundState { Node = node.Id };
        if (returning)
        {
            Log("storyRoundReturn", args: Args(("node", node.Id), ("name", node.Name?.GetValueOrDefault("en"))));
        }
        else
        {
            Log("storyRound", node.Narrative, Args(("node", node.Id), ("name", (node.Name ?? node.Title)?.GetValueOrDefault("en")), ("page", Number(node.Page))));
        }

        DrawStoreUniques(node);
        // The page's mechanics first (rewards, party changes), then the phases.
        Push([.. returning ? [] : node.Mechanics, new ScenarioAction { Do = InternalActions.AskStoryRound }]);
    }

    /// <summary>The Store's random Unique Items: drawn the first time the party reaches this Story Round, kept until it Ventures Forth.</summary>
    private void DrawStoreUniques(Node node)
    {
        if (node.Store?.Unique is not { } unique || State.Campaign.StoreUniques.ContainsKey(node.Id))
        {
            return;
        }

        var drawn = new List<string>();
        State.Campaign.StoreUniques[node.Id] = drawn;
        for (var i = 0; i < unique.Count; i++)
        {
            var left = UniquesLeft(unique.Tier).Except(drawn).ToList();
            if (left.Count == 0)
            {
                Log("storeUniqueNone", args: Args(("tier", unique.Tier)));
                break;
            }

            var item = left[NextRandom(left.Count)];
            drawn.Add(item);
            Log("storeUnique", args: Args(("item", item), ("tier", unique.Tier)));
        }
    }

    /// <summary>
    /// Unique Items of a tier the party may still come across: not held, and not for sale in a Store (p. 13: the Store's
    /// Unique "will also not be available during an Encounter when drawing a random Unique Item of the same Tier").
    /// </summary>
    private IEnumerable<string> UniquesLeft(string? tier) =>
        Catalog.Packs.SelectMany(p => p.Items)
            .Where(i => i.Unique && i.Tier == tier && !State.Campaign.Items.Contains(i.Id)
                        && !State.Campaign.StoreUniques.Values.Any(list => list.Contains(i.Id)))
            .Select(i => i.Id)
            .Distinct();

    private void AskStoryRound()
    {
        if (State.Campaign.StoryRound is not { } round || Campaign.Nodes.FirstOrDefault(n => n.Id == round.Node) is not { } node)
        {
            return;
        }

        if (round.Phase == StoryRoundPhases.Shop)
        {
            Ask(PromptKinds.StoryRound, Text("Shop and Train: buy, sell, learn Disciplines, trade items.", "Boutique et entraînement : acheter, vendre, apprendre des disciplines, échanger des objets."),
                [new PromptOption(ExploreOption, Text("Explore", "Explorer"))]);
            return;
        }

        var quests = node.SideQuests
            .Where(q => !State.Campaign.SideQuestsAttempted.Contains(q.Key))
            .Select(q => new PromptOption(QuestPrefix + q.Key, q.Label ?? Text(q.Node, q.Node)));
        var bounties = BountiesOffered(node)
            .Select(b => new PromptOption(BountyPrefix + b.Id, b.Title));
        var exits = node.VentureForth
            .Select((v, i) => new PromptOption(VenturePrefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture), v.Label ?? Text(v.Node, v.Node)));
        Ask(PromptKinds.StoryRound, Text("Explore: choose a Side Quest, or Venture Forth.", "Explorer : choisir une quête annexe, ou continuer l'histoire."), quests.Concat(bounties).Concat(exits));
    }

    private void AnswerStoryRound(string option)
    {
        var round = State.Campaign.StoryRound!;
        var node = Campaign.Nodes.First(n => n.Id == round.Node);
        if (option == ExploreOption)
        {
            DiscardUnusedConsumables();
            round.Phase = StoryRoundPhases.Explore;
            Log("storyRoundExplore", args: Args(("node", node.Id)));
            AskStoryRound();
            return;
        }

        if (option.StartsWith(QuestPrefix, StringComparison.Ordinal))
        {
            var quest = node.SideQuests.First(q => q.Key == option[QuestPrefix.Length..]);
            State.Campaign.SideQuestsAttempted.Add(quest.Key);
            round.Phase = StoryRoundPhases.SideQuest;
            Log("sideQuest", quest.Label, Args(("quest", quest.Key), ("node", quest.Node), ("page", Number(quest.Page))));
            GoToNode(quest.Node);
            return;
        }

        if (option.StartsWith(BountyPrefix, StringComparison.Ordinal))
        {
            StartBounty(node, context.Bounties[option[BountyPrefix.Length..]]);
            return;
        }

        var exit = node.VentureForth[int.Parse(option[VenturePrefix.Length..], System.Globalization.CultureInfo.InvariantCulture)];
        State.Campaign.StoryRound = null;
        State.Campaign.StoryRoundsLeft.Add(node.Id);
        Log("ventureForth", exit.Label, Args(("node", exit.Node), ("from", node.Id), ("page", Number(exit.Page))));
        GoToNode(exit.Node);
    }

    /// <summary>
    /// The Bounties this Story Round allows (p. 13): its difficulty or less (a Bounty without one is allowed), never
    /// one already won, and only one per named Story Round.
    /// </summary>
    private IEnumerable<Scenario> BountiesOffered(Node node) =>
        node.BountyDifficulty is not { } most || State.Campaign.BountyStoryRounds.Contains(node.Id)
            ? []
            : context.Bounties.Values
                .Where(b => (b.Difficulty ?? 0) <= most && !State.Campaign.BountiesCompleted.Contains(b.Id))
                .OrderBy(b => b.Difficulty ?? 0).ThenBy(b => b.Id, StringComparer.Ordinal);

    private void StartBounty(Node storyRound, Scenario bounty)
    {
        State.Campaign.StoryRound!.Phase = StoryRoundPhases.SideQuest;
        State.Campaign.BountyStoryRounds.Add(storyRound.Id);
        if (!State.Campaign.BountiesAttempted.Contains(bounty.Id))
        {
            State.Campaign.BountiesAttempted.Add(bounty.Id);
        }

        State.Bounty = new BountyState { Scenario = bounty.Id, StoryRound = storyRound.Id };
        Log("bountyStart", bounty.Title, Args(("scenario", bounty.Id), ("difficulty", Number(bounty.Difficulty))));
        GoToNode(bounty.Start);
    }

    /// <summary>
    /// The Bounty is over, whatever its ending says: won when it ends on "@victory", or on a won encounter. Its rewards
    /// are kept either way (p. 13); the party is back at the Story Round's Shop and Train.
    /// </summary>
    private void EndBounty(BountyState bounty, string ending)
    {
        var won = ending == "@victory" || (ending != "@defeat" && bounty.LastEnd == "win");
        if (won && !State.Campaign.BountiesCompleted.Contains(bounty.Scenario))
        {
            State.Campaign.BountiesCompleted.Add(bounty.Scenario);
        }

        if (!State.Campaign.AttemptedScenarios.Contains(bounty.Scenario))
        {
            State.Campaign.AttemptedScenarios.Add(bounty.Scenario);
        }

        State.Bounty = null;
        State.Encounter = null;
        State.PendingActions.Clear();
        Log("bountyEnd", args: Args(("scenario", bounty.Scenario), ("won", won ? "true" : "false")));
        var storyRound = Campaign.Nodes.First(n => n.Id == bounty.StoryRound);
        State.Campaign.CurrentNode = storyRound.Id;
        EnterStoryRound(storyRound, returning: true);
    }

    /// <summary>End of Shop and Train (p. 25, 27): every Consumable neither equipped nor sold is discarded.</summary>
    private void DiscardUnusedConsumables()
    {
        var lists = new[] { State.Campaign.Stash, State.Campaign.Unassigned }.Concat(State.Campaign.Party.Select(a => a.Pack));
        foreach (var list in lists)
        {
            foreach (var owned in list.Where(o => (Catalog.FindItem(o.Item)?.Type ?? o.Type) == "consumable").ToList())
            {
                list.Remove(owned);
                Log("consumableDiscarded", args: Args(("item", owned.Item ?? owned.Type), ("id", owned.Id)));
            }
        }
    }

    // ---- Disciplines ----

    /// <summary>
    /// An adventurer learns a Discipline (rulebook p. 24, 32). A card of the catalogue costs the XP printed on it,
    /// +1 per Discipline of the same level already learnt, and needs one Discipline of each lower level of its tree;
    /// a Story Discipline is given by the story, for free. A card the catalogue does not know costs <paramref name="xp"/>.
    /// </summary>
    private Result LearnDiscipline(string? adventurer, string? discipline, int xp)
    {
        if (Member(adventurer) is not { } member)
        {
            return PartyErrors.UnknownAdventurer(adventurer);
        }

        if (string.IsNullOrWhiteSpace(discipline))
        {
            return PartyErrors.UnknownDiscipline(discipline);
        }

        var card = Catalog.FindDiscipline(discipline);
        if (member.Disciplines.Any(d => d.Id == discipline))
        {
            return PartyErrors.AlreadyLearnt(member.Name, discipline);
        }

        if (card is { Story: false, Level: { } level } && MissingLevel(member, card.Tree, level) is { } missing)
        {
            return PartyErrors.MissingLevel(member.Name, card.Tree, missing);
        }

        var cost = DisciplineCost(member, card) ?? Math.Max(0, xp);
        var left = member.Xp - member.Disciplines.Sum(d => d.Xp);
        if (cost > left)
        {
            return PartyErrors.NotEnoughXp(member.Name, cost, left);
        }

        member.Disciplines.Add(new LearnedDiscipline { Id = discipline, Xp = cost });
        Log("disciplineLearnt", args: Args(("adventurer", member.Name), ("discipline", discipline), ("xp", Number(cost))));
        return Result.Success();
    }

    /// <summary>The XP a card costs this adventurer: printed cost +1 per Discipline of the same level; null when unknown.</summary>
    private int? DisciplineCost(AdventurerState member, Discipline? card) => card switch
    {
        { Story: true } => 0,
        { Level: { } level } => (card.Cost ?? 2 * level + 1)
                                + member.Disciplines.Count(d => Catalog.FindDiscipline(d.Id) is { Story: false } other && other.Level == level),
        _ => null,
    };

    /// <summary>The lowest level of the tree the adventurer still lacks below <paramref name="level"/>, or null.</summary>
    private int? MissingLevel(AdventurerState member, string tree, int level)
    {
        var known = member.Disciplines.Select(d => Catalog.FindDiscipline(d.Id))
            .Where(d => d is { Story: false } && d.Tree == tree).Select(d => d!.Level).ToHashSet();
        return Enumerable.Range(1, Math.Max(0, level - 1)).Cast<int?>().FirstOrDefault(l => !known.Contains(l));
    }

    /// <summary>
    /// Re-specing (rulebook p. 87): while Shopping and Training, an adventurer pays 25 Gold, +25 per Loot Level beyond
    /// Mundane of the Story Round's Items for sale, to lose every Discipline but the Story ones and get their XP back.
    /// </summary>
    private Result Respec(string? adventurer)
    {
        if (Member(adventurer) is not { } member)
        {
            return PartyErrors.UnknownAdventurer(adventurer);
        }

        var price = RespecPrice();
        if (price > State.Campaign.Gold)
        {
            return PartyErrors.NotEnoughGold(price, State.Campaign.Gold);
        }

        var lost = member.Disciplines.RemoveAll(d => Catalog.FindDiscipline(d.Id) is not { Story: true });
        State.Campaign.Gold -= price;
        Log("respec", args: Args(("adventurer", member.Name), ("price", Number(price)), ("gold", Number(State.Campaign.Gold)), ("count", Number(lost))));
        return Result.Success();
    }

    /// <summary>25 Gold, +25 per Loot Level beyond Mundane of the current Story Round's Store (its highest tier).</summary>
    private int RespecPrice()
    {
        var store = State.Campaign.StoryRound is { } round ? Campaign.Nodes.FirstOrDefault(n => n.Id == round.Node)?.Store : null;
        var tiers = (store?.Items ?? []).SelectMany(f => f.Tiers).Append(store?.Unique?.Tier);
        return 25 * (1 + Math.Max(0, tiers.Select(LootLevels.Rank).DefaultIfEmpty(0).Max()));
    }

    private Result ForgetDiscipline(string? adventurer, string? discipline)
    {
        if (Member(adventurer) is not { } member)
        {
            return PartyErrors.UnknownAdventurer(adventurer);
        }

        var index = member.Disciplines.FindLastIndex(d => d.Id == discipline);
        if (index < 0)
        {
            return PartyErrors.UnknownDiscipline(discipline);
        }

        member.Disciplines.RemoveAt(index);
        Log("disciplineForgotten", args: Args(("adventurer", member.Name), ("discipline", discipline)));
        return Result.Success();
    }

    // ---- The party ----

    private AdventurerState? Member(string? name) =>
        State.Campaign.Party.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The GM chooses the Active Group (when the story says so): at most 4 members, none Injured or Unselectable.</summary>
    private Result SetActiveGroup(IReadOnlyList<string>? names)
    {
        if (names is not { Count: > 0 })
        {
            return PartyErrors.EmptyGroup;
        }

        if (names.Count > 4)
        {
            return PartyErrors.TooLarge(names.Count);
        }

        var members = new List<string>();
        foreach (var name in names)
        {
            if (Member(name) is not { } member)
            {
                return PartyErrors.UnknownAdventurer(name);
            }

            if (member.Injured || member.Unselectable)
            {
                return PartyErrors.NotSelectable(member.Name);
            }

            members.Add(member.Name);
        }

        State.Campaign.ActiveGroup = members;
        Log("activeGroup", args: Args(("adventurers", string.Join(", ", members))));
        return Result.Success();
    }

    /// <summary>The GM marks a member Injured or Unselectable (or no longer), as the story says.</summary>
    private Result SetAdventurerStatus(string? name, string? status, bool value)
    {
        if (Member(name) is not { } member)
        {
            return PartyErrors.UnknownAdventurer(name);
        }

        switch (status)
        {
            case "injured":
                member.Injured = value;
                break;
            case "unselectable":
                member.Unselectable = value;
                break;
            default:
                return PartyErrors.UnknownStatus(status);
        }

        Log("adventurerStatus", args: Args(("adventurer", member.Name), ("status", status), ("value", value ? "true" : "false")));
        return Result.Success();
    }

    /// <summary>joinParty, leaveParty, setInjured, setUnselectable, setActiveGroup: the story changes the party.</summary>
    private void DoPartyAction(ScenarioAction action)
    {
        var name = ExtraString(action, "adventurer");
        switch (action.Do)
        {
            case "joinParty" when name is not null:
                if (Member(name) is null)
                {
                    State.Campaign.Party.Add(new AdventurerState { Name = name, Xp = action.Amount ?? 0 });
                    State.Campaign.ActiveGroup?.Add(name);
                }

                Log("joinParty", action.Text, Args(("adventurer", name)));
                break;
            case "leaveParty" when Member(name) is { } member:
                // Their items go to the Party Stash unless the story says they are lost ("items": "discard").
                var discard = ExtraString(action, "items") == "discard";
                if (!discard)
                {
                    State.Campaign.Stash.AddRange(member.Equipped.Concat(member.Pack));
                }

                State.Campaign.Party.Remove(member);
                State.Campaign.ActiveGroup?.RemoveAll(n => string.Equals(n, member.Name, StringComparison.OrdinalIgnoreCase));
                Log("leaveParty", action.Text, Args(("adventurer", member.Name), ("items", discard ? "discard" : StashOwner)));
                break;
            case "setInjured" or "setUnselectable" when Member(name) is not null:
                var value = action.Extra?.TryGetValue("value", out var flag) != true || flag.ValueKind != System.Text.Json.JsonValueKind.False;
                SetAdventurerStatus(name, action.Do == "setInjured" ? "injured" : "unselectable", value);
                break;
            case "setActiveGroup":
                var names = action.Extra?.TryGetValue("adventurers", out var list) == true && list.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? list.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null;
                if (names is null)
                {
                    // No names: the GM chooses (the story says "choose your Active Group").
                    State.Campaign.ActiveGroup = null;
                    Log("chooseActiveGroup", action.Text);
                }
                else if (!SetActiveGroup(names).IsSuccess)
                {
                    Log("manualAction", action.Text, Args(("do", action.Do)));
                }

                break;
            default:
                Log("manualAction", action.Text, Args(("do", action.Do), ("adventurer", name)));
                break;
        }
    }
}
