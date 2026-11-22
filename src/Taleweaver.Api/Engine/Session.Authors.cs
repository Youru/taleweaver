using System.Text.Json;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Who did it (play-spec A6): "the adventurer who activated it", "who defeated it". The author is written into the
/// actions when they are triggered, so that it is still known after the questions they wait for.
/// </summary>
internal sealed partial class Session
{
    private const string AuthorRole = "author";
    private const string AskRole = "askAdventurer";
    private const string AuthorKey = "author";

    /// <summary>Who did what is being played: the command's author, then the one written in each action.</summary>
    private string? _author;

    /// <summary>An adventurer, or an ally for its summoner (p. 75: a Loyal Esper's gains go to its summoner).</summary>
    private string? AuthorOf(string? figure) =>
        IsAlly(figure) && State.Encounter is { } encounter
            ? Member(encounter.Allies.LastOrDefault(a => AllyPrefix + a.Id == figure)?.Owner)?.Name
            : MemberOf(figure)?.Name;

    /// <summary>The actions triggered, with their author. Unknown and needed: the GM is asked once for all of them.</summary>
    private List<ScenarioAction> WithAuthor(IEnumerable<ScenarioAction> triggered)
    {
        var actions = triggered.ToList();
        if (_author is { } author)
        {
            return Bind(actions, author);
        }

        if (!actions.Any(NeedsAuthor))
        {
            return actions;
        }

        var options = Candidates().Select(a => new ActionOption { Label = Text(a.Name, a.Name), Actions = Bind(actions, a.Name) }).ToList();
        return options.Count == 0
            ? actions
            : [new ScenarioAction { Do = "askChoice", Question = Text("Which adventurer did it?", "Quel aventurier l’a fait ?"), Options = options }];
    }

    /// <summary>The adventurers a question may name, the one playing first once the turns have begun.</summary>
    private List<AdventurerState> Candidates()
    {
        var pool = State.Encounter is null ? State.Campaign.Party.ToList() : ActiveParty.ToList();
        return Playing() is { } playing ? pool.OrderBy(a => a.Name == playing ? 0 : 1).ToList() : pool;
    }

    private string? Playing() =>
        State.Encounter is { Round: > 0 } e && e.CurrentTurn >= 0 && e.CurrentTurn < e.InitiativeTrack.Count && IsAdventurer(e.InitiativeTrack[e.CurrentTurn])
            ? MemberOf(AdventurerOfCard(e.InitiativeTrack[e.CurrentTurn]))?.Name
            : null;

    /// <summary>An extra Initiative Card of an adventurer is "adventurer:Rook#2".</summary>
    private static string AdventurerOfCard(string card) => card[AdventurerCard.Length..].Split('#')[0];

    private static bool NeedsAuthor(ScenarioAction action)
    {
        if (WhoIs(action) == AuthorRole || ExtraString(action, "adventurer") == AuthorRole)
        {
            return true;
        }

        // These name their own author: who arrives, who tests, who decides
        return action.Do is not ("joinParty" or "askSkillCheck")
               && !(action.Do == "askChoice" && action.Who is not null)
               && Children(action).Any(NeedsAuthor);
    }

    private static IEnumerable<ScenarioAction> Children(ScenarioAction action) =>
        new[] { action.Pass, action.Masterful, action.Fail, action.Fallback, action.Actions, action.Then, action.Else }
            .Concat(action.Rewards ?? [])
            .Concat((action.Options ?? []).SelectMany(o => new[] { o.Actions, o.Pass }))
            .SelectMany(list => list ?? []);

    private static List<ScenarioAction> Bind(IEnumerable<ScenarioAction> actions, string author) => actions.Select(a => Bound(a, author)).ToList();

    private static ScenarioAction Bound(ScenarioAction action, string author)
    {
        IReadOnlyList<ScenarioAction>? All(IReadOnlyList<ScenarioAction>? list) => list is null ? null : Bind(list, author);
        return With(action, AuthorKey, author) with
        {
            Pass = All(action.Pass),
            Masterful = All(action.Masterful),
            Fail = All(action.Fail),
            Fallback = All(action.Fallback),
            Actions = All(action.Actions),
            Then = All(action.Then),
            Else = All(action.Else),
            Rewards = action.Rewards?.Select(r => All(r)!).ToList(),
            Options = action.Options?.Select(o => o with { Actions = All(o.Actions), Pass = All(o.Pass) }).ToList(),
        };
    }

    private static ScenarioAction With(ScenarioAction action, string key, object? value)
    {
        var extra = action.Extra is { } kept ? new Dictionary<string, JsonElement>(kept) : [];
        if (value is null)
        {
            extra.Remove(key);
        }
        else
        {
            extra[key] = JsonSerializer.SerializeToElement(value, Common.JsonDefaults.Options);
        }

        return action with { Extra = extra.Count > 0 ? extra : null };
    }

    private static List<string>? ExtraList(ScenarioAction action, string key) =>
        action.Extra?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : null;

    private static bool ExtraTrue(ScenarioAction action, string key) =>
        action.Extra?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.True;

    private static string? WhoIs(ScenarioAction action) => action.Who is { ValueKind: JsonValueKind.String } who ? who.GetString() : null;

    /// <summary>
    /// The action with the adventurer it aims at: "author", "askAdventurer", "random", or "@name" for who was drawn.
    /// Null when it waits for the GM's answer.
    /// </summary>
    private ScenarioAction? Aimed(ScenarioAction action)
    {
        if (action.Each == true)
        {
            return action;
        }

        var byName = ExtraString(action, "adventurer");
        var who = byName ?? WhoIs(action);
        if (who is null)
        {
            return action;
        }

        ScenarioAction To(string name) =>
            byName is null ? action with { Who = JsonSerializer.SerializeToElement(name) } : With(action, "adventurer", name);

        var found = who switch
        {
            AuthorRole => _author,
            "random" => DrawAdventurers(action, 1).FirstOrDefault(),
            _ when who.StartsWith('@') => Drawn(who[1..]).FirstOrDefault(),
            _ => null,
        };
        if (found is not null)
        {
            return To(found);
        }

        // gainItem and defeatAdventurer have their own question
        var asked = who == AuthorRole || (who == AskRole && action.Do is not ("gainItem" or "defeatAdventurer"));
        if (!asked || Candidates() is not { Count: > 0 } candidates)
        {
            return action;
        }

        Push([new ScenarioAction
        {
            Do = "askChoice",
            Question = action.Do == "askChoice" ? Text("Who decides?", "Qui décide ?") : Text("Which adventurer?", "Quel aventurier ?"),
            Options = candidates.Select(a => new ActionOption { Label = Text(a.Name, a.Name), Actions = [To(a.Name)] }).ToList(),
        }]);
        return null;
    }

    private List<string> Drawn(string variable) =>
        !State.Campaign.Variables.TryGetValue(variable, out var value) ? []
        : value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
        : value.ValueKind == JsonValueKind.String ? [value.GetString()!]
        : [];

    /// <summary>Adventurers at random (play-spec A16), "except" left out: names, "author", or "@name".</summary>
    private List<string> DrawAdventurers(ScenarioAction action, int count)
    {
        var pool = (ExtraString(action, "from") ?? action.From) == "party" ? State.Campaign.Party.Select(a => a.Name).ToList() : ActiveParty.Select(a => a.Name).ToList();
        var left = pool.Except(Excepted(action), StringComparer.OrdinalIgnoreCase).ToList();
        var drawn = new List<string>();
        while (drawn.Count < count && left.Count > 0)
        {
            var name = left[NextRandom(left.Count)];
            left.Remove(name);
            drawn.Add(name);
        }

        if (drawn.Count > 0)
        {
            Log("adventurerDrawn", args: Args(("adventurers", string.Join(", ", drawn))));
        }

        return drawn;
    }

    private List<string> Excepted(ScenarioAction action) =>
        (ExtraList(action, "except") ?? (ExtraString(action, "except") is { } one ? [one] : []))
        .SelectMany(name => name == AuthorRole ? (_author is { } author ? [author] : []) : name.StartsWith('@') ? Drawn(name[1..]) : (IEnumerable<string>)[name])
        .ToList();

    private List<AdventurerState> EachBut(ScenarioAction action)
    {
        var out_ = Excepted(action);
        return State.Campaign.Party.Where(a => !out_.Contains(a.Name, StringComparer.OrdinalIgnoreCase)).ToList();
    }
}
