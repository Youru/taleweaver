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
    private const int ActiveGroupSize = 4;

    private static class PartyErrors
    {
        public static Error UnknownAdventurer(string? name) => Error.NotFound("Party.UnknownAdventurer", $"No adventurer '{name}' in the party.");

        public static Error NotLearnable(string? card) => Error.Validation("Party.NotLearnable", $"'{card}' is an Exalted Esper: it is used, not learnt.");

        public static Error FamiliarNeeded(string name) => Error.Conflict("Party.FamiliarNeeded", $"{name} must learn the Familiar Discipline first.");

        public static Error FamiliarChosen(string name) => Error.Conflict("Party.FamiliarChosen", $"{name} already has a Familiar: it cannot be changed.");

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
        State.Encounter?.Participants is { } imposed
            ? State.Campaign.Party.Where(a => imposed.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
            : State.Campaign.Party.Where(a => !a.Injured && !a.Unselectable
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
            .Where(q => !State.Campaign.SideQuestsAttempted.Contains(q.Key) && (q.If ?? []).All(IsTrue))
            .Select(q => new PromptOption(QuestPrefix + q.Key, q.Label ?? Text(q.Node, q.Node)));
        var bounties = BountiesOffered(node)
            .Select(b => new PromptOption(BountyPrefix + b.Id, b.Title));
        var ways = node.VentureForth.Select((exit, index) => (Exit: exit, Index: index)).ToList();
        var open = ways.Where(w => (w.Exit.If ?? []).All(IsTrue)).ToList();
        if (open.Count == 0 && ways.Count > 0)
        {
            // Every way closed would stop the game: all shown, the GM says
            Log("noExitOpen");
            open = ways;
        }

        var exits = open.Select(w => new PromptOption(VenturePrefix + Number(w.Index), w.Exit.Label ?? Text(w.Exit.Node, w.Exit.Node)));
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
        LeaveEncounter();
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

        if (card?.Tree == Discipline.ExaltedTree)
        {
            return PartyErrors.NotLearnable(discipline);
        }

        // A Familiar (rulebook p. 76): one, chosen after learning Familiar, never changed
        if (card?.Tree == Discipline.FamiliarTree)
        {
            if (!AllyRules.Knows(member, AllyRules.Familiar))
            {
                return PartyErrors.FamiliarNeeded(member.Name);
            }

            if (member.Disciplines.Any(d => Catalog.FindDiscipline(d.Id)?.Tree == Discipline.FamiliarTree))
            {
                return PartyErrors.FamiliarChosen(member.Name);
            }
        }

        var known = member.Disciplines.Select(d => d.Id).ToList();
        if (DisciplineRules.MissingLevel(Catalog, known, card) is { } missing)
        {
            return PartyErrors.MissingLevel(member.Name, card!.Tree, missing);
        }

        var cost = DisciplineRules.Cost(Catalog, known, card) ?? Math.Max(0, xp);
        var left = member.Xp - member.Disciplines.Sum(d => d.Xp);
        if (cost > left)
        {
            return PartyErrors.NotEnoughXp(member.Name, cost, left);
        }

        member.Disciplines.Add(new LearnedDiscipline { Id = discipline, Xp = cost });
        Log("disciplineLearnt", args: Args(("adventurer", member.Name), ("discipline", discipline), ("xp", Number(cost))));
        return Result.Success();
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

        var free = Options.XpLeniency && !State.Campaign.FreeRespecs.Contains(member.Name);
        var price = free ? 0 : RespecPrice();
        if (price > State.Campaign.Gold)
        {
            return PartyErrors.NotEnoughGold(price, State.Campaign.Gold);
        }

        if (free)
        {
            State.Campaign.FreeRespecs.Add(member.Name);
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

        if (names.Count > ActiveGroupSize)
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
                KeepTheFourActive(recovers: !value && member.Injured);
                member.Injured = value;
                break;
            case "unselectable":
                KeepTheFourActive(recovers: !value && member.Unselectable);
                member.Unselectable = value;
                break;
            default:
                return PartyErrors.UnknownStatus(status);
        }

        Log("adventurerStatus", args: Args(("adventurer", member.Name), ("status", status), ("value", value ? "true" : "false")));
        return Result.Success();
    }

    /// <summary>
    /// Without a chosen group everybody eligible plays: once four do, they become the group, and whoever joins or
    /// recovers waits to be chosen (p. 10).
    /// </summary>
    private void KeepTheFourActive(bool recovers)
    {
        if (!recovers || State.Campaign.ActiveGroup is not null)
        {
            return;
        }

        var active = ActiveParty.Select(a => a.Name).ToList();
        if (active.Count >= ActiveGroupSize)
        {
            State.Campaign.ActiveGroup = active;
        }
    }

    /// <summary>
    /// "Choose your Active Group": asked each time the story says it, four among the eligible (four or fewer: all of
    /// them, nothing to ask). Items may be exchanged while the question waits.
    /// </summary>
    private void ChooseActiveGroup(ScenarioAction action)
    {
        var eligible = State.Campaign.Party.Where(a => !a.Injured && !a.Unselectable).Select(a => a.Name).ToList();
        if (eligible.Count <= ActiveGroupSize)
        {
            State.Campaign.ActiveGroup = null;
            Log("activeGroup", args: Args(("adventurers", string.Join(", ", eligible))));
            return;
        }

        Log("chooseActiveGroup", action.Text);
        Ask(PromptKinds.ActiveGroup, Text("Choose the 4 adventurers of the Active Group.", "Choisir les 4 aventuriers du groupe actif."),
            eligible.Select(name => new PromptOption(name, Text(name, name))), action);
        State.Prompt!.Max = ActiveGroupSize;
    }

    private Result AnswerActiveGroup(Prompt prompt, IReadOnlyList<string>? names)
    {
        var chosen = names?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        // The party may have changed while the question waited
        var eligible = State.Campaign.Party.Count(a => !a.Injured && !a.Unselectable);
        if (names?.Count != chosen.Count || chosen.Count != Math.Min(prompt.Max ?? ActiveGroupSize, eligible))
        {
            return GameEngine.Errors.InvalidAnswer(string.Join(", ", names ?? []));
        }

        var result = SetActiveGroup(chosen);
        if (result.IsSuccess)
        {
            State.Prompt = null;
        }

        return result;
    }

    /// <summary>An arrival (play-spec A3): what the story gives them, then the GM finishes with "setup".</summary>
    private void SetUpArrival(ScenarioAction action, AdventurerState member)
    {
        if (ExtraNumber(action, "gold") is { } gold)
        {
            GainGold(gold);
        }

        foreach (var id in ExtraList(action, "items") ?? [])
        {
            if (Catalog.FindItem(id) is not { } card)
            {
                Log("manualAction", args: Args(("do", "gainItem"), ("item", id), ("adventurer", member.Name)));
                continue;
            }

            State.Campaign.Items.Add(card.Id);
            member.Equipped.Add(new OwnedItem { Id = NewItemId(), Item = card.Id, Source = "scenario" });
            Log("gainItem", args: Args(("item", card.Id), ("adventurer", member.Name)));
        }

        var actions = (ExtraList(action, "disciplines") ?? [])
            .Select(id => With(new ScenarioAction { Do = "gainDiscipline", Ref = id }, "adventurer", member.Name))
            .Concat(Bind(action.Actions ?? [], member.Name));
        if (ExtraTrue(action, "setup"))
        {
            actions = actions.Append(With(new ScenarioAction { Do = InternalActions.AskArrival }, "adventurer", member.Name));
        }

        Push(actions);
    }

    private void AskArrival(string? name)
    {
        Ask(PromptKinds.Arrival,
            Text($"{name} joins the party: learn Disciplines, equip, give a seat, then continue.",
                $"{name} rejoint le groupe : apprendre ses disciplines, l’équiper, lui donner un joueur, puis continuer."),
            [new PromptOption("continue")], instance: name);
    }

    private void AfterArrival()
    {
        if (State.Campaign.Party.Count(a => !a.Injured && !a.Unselectable) > ActiveGroupSize)
        {
            Push([new ScenarioAction { Do = "setActiveGroup" }]);
        }
    }

    /// <summary>joinParty, leaveParty, setInjured, setUnselectable, setActiveGroup: the story changes the party.</summary>
    private void DoPartyAction(ScenarioAction action)
    {
        var name = ExtraString(action, "adventurer");
        switch (action.Do)
        {
            case "joinParty" when name is not null:
                var arrives = Member(name) is null;
                if (arrives)
                {
                    // "XP equal to the total XP earned by the party (a single member) so far"
                    var xp = action.Amount ?? State.Campaign.Party.Select(a => a.Xp).DefaultIfEmpty(0).Max();
                    KeepTheFourActive(recovers: true);
                    State.Campaign.Party.Add(new AdventurerState { Name = name, Xp = xp });
                    if (State.Campaign.ActiveGroup is { } group && group.Count < ActiveGroupSize)
                    {
                        group.Add(name);
                    }
                }

                Log("joinParty", action.Text, Args(("adventurer", name)));
                if (arrives)
                {
                    SetUpArrival(action, Member(name)!);
                }

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
                    ChooseActiveGroup(action);
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
