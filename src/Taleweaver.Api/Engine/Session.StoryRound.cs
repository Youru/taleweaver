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

    private static class PartyErrors
    {
        public static Error UnknownAdventurer(string? name) => Error.NotFound("Party.UnknownAdventurer", $"No adventurer '{name}' in the party.");

        public static Error NotSelectable(string name) => Error.Conflict("Party.NotSelectable", $"{name} is Injured or Unselectable: not in the Active Group.");

        public static readonly Error EmptyGroup = Error.Validation("Party.EmptyGroup", "The Active Group needs at least one adventurer.");

        public static Error TooLarge(int count) => Error.Validation("Party.TooLarge", $"The Active Group has at most 4 adventurers, not {count}.");

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
        if (State.Campaign.StoryRound is not { } round || Scenario.Nodes.FirstOrDefault(n => n.Id == round.Node) is not { } node)
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
        var exits = node.VentureForth
            .Select((v, i) => new PromptOption(VenturePrefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture), v.Label ?? Text(v.Node, v.Node)));
        Ask(PromptKinds.StoryRound, Text("Explore: choose a Side Quest, or Venture Forth.", "Explorer : choisir une quête annexe, ou continuer l'histoire."), quests.Concat(exits));
    }

    private void AnswerStoryRound(string option)
    {
        var round = State.Campaign.StoryRound!;
        var node = Scenario.Nodes.First(n => n.Id == round.Node);
        if (option == ExploreOption)
        {
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

        var exit = node.VentureForth[int.Parse(option[VenturePrefix.Length..], System.Globalization.CultureInfo.InvariantCulture)];
        State.Campaign.StoryRound = null;
        State.Campaign.StoryRoundsLeft.Add(node.Id);
        Log("ventureForth", exit.Label, Args(("node", exit.Node), ("from", node.Id), ("page", Number(exit.Page))));
        GoToNode(exit.Node);
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
