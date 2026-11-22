using System.Text.Json;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>Events of the encounter: trigger → conditions → actions (data model §3.6).</summary>
internal sealed partial class Session
{
    /// <summary>Fires the encounter's events matching the trigger, in scenario order; their actions run next.</summary>
    private void Fire(Func<ScenarioEvent, bool> matches)
    {
        if (State.Encounter is not { Ended: false } encounter)
        {
            return;
        }

        var actions = new List<ScenarioAction>();
        foreach (var ev in EncounterNode.Events.Where(matches))
        {
            if ((ev.Once == true && encounter.FiredEvents.Contains(ev.Id)) || !ev.Conditions.All(IsTrue))
            {
                continue;
            }

            encounter.FiredEvents.Add(ev.Id);
            if (ev.HiddenText is not null)
            {
                Log("hiddenText", ev.HiddenText, Args(("event", ev.Id)));
            }

            actions.AddRange(ev.Actions);
        }

        Push(WithAuthor(actions));
    }

    /// <summary>
    /// An Adventurer activates a token (or a text says to read it). Totems and loot are discarded once read
    /// (rulebook p. 17); objectives stay until a removeToken.
    /// </summary>
    private void ActivateToken(string key)
    {
        Encounter.ActivatedTokens.Add(key);
        Log("tokenActivated", args: Args(("token", key)));
        Fire(e => e.Trigger.On == "tokenActivated" && e.Trigger.Token?.Contains(key) == true);
        if (key.StartsWith("totem:", StringComparison.Ordinal) || key.StartsWith("loot:", StringComparison.Ordinal))
        {
            foreach (var token in Encounter.Tokens.Where(t => t.Key == key && !t.Removed))
            {
                token.Removed = true;
                Log("tokenDiscarded", args: Args(("id", token.Id), ("token", key)));
            }
        }
    }

    private bool IsTrue(EventCondition condition)
    {
        if (condition.Not is { } not && IsTrue(not))
        {
            return false;
        }

        if (condition.All is { } all && !all.All(IsTrue))
        {
            return false;
        }

        if (condition.Any is { } any && !any.Any(IsTrue))
        {
            return false;
        }

        if (condition.EndConditionActive is { } end && State.Encounter?.ActiveEndConditions.Contains(end) != true)
        {
            return false;
        }

        return condition.Var is null || Compare(Variable(condition.Var), condition.Op ?? "==", condition.Value);
    }

    /// <summary>Built-in variables (§3.5, §3.8), then the scenario's own.</summary>
    private JsonElement? Variable(string name)
    {
        var (prefix, rest) = name.IndexOf('.') is var dot and > 0 ? (name[..dot], name[(dot + 1)..]) : (name, "");
        // Read between encounters too (Story Round exits): what only an encounter has is then 0 or false
        var encounter = State.Encounter;
        object? value = prefix switch
        {
            "round" => encounter?.Round ?? 0,
            "urgency" => encounter?.Urgency ?? 0,
            "gold" => State.Campaign.Gold,
            "counter" => State.Campaign.Counters.FirstOrDefault(c => c.Id == rest)?.Value ?? 0,
            "carried" => State.Campaign.Carried.Any(c => c.Id == rest && c.Carrier is not null),
            "lost" => State.Campaign.Carried.Any(c => c.Id == rest && c.Lost),
            "placed" => encounter?.PlacedDiagrams.Contains(rest) == true,
            "activated" => encounter?.ActivatedTokens.Contains(rest) == true,
            "hiddenCard" => State.Campaign.RevealedHiddenCards.Contains(rest),
            "revealed" => encounter?.RevealedAreas.Contains(rest) == true,
            "defeated" or "alive" when encounter is null => 0,
            "defeated" => Encounter.Combatants.Count(c => c.Defeated && (c.Family == rest || BaseFamilyOf(c) == rest)),
            // Those still under the fog are left to defeat: "none alive" is not true before they appeared
            "alive" when rest.StartsWith("side:", StringComparison.Ordinal) => CountOfSide(rest["side:".Length..]),
            "alive" => Alive().Count(c => IsOpponent(c) && (c.Family == rest || BaseFamilyOf(c) == rest)) + Encounter.Waiting.Count(w => FamilyToCome(w.Action) == rest),
            _ => null,
        };
        if (value is not null)
        {
            return JsonSerializer.SerializeToElement(value);
        }

        return State.Campaign.Variables.TryGetValue(name, out var stored) ? stored : null;
    }

    private int CountOfSide(string side) =>
        Alive().Count(c => SideOf(c) == side) + Encounter.Waiting.Count(w => (ExtraString(w.Action, "side") ?? EnemiesSide) == side);

    private string? FamilyToCome(ScenarioAction action) =>
        action.Family ?? (action.Ref is { } id && Catalog.FindCombatant(id) is { } card && !AllyRules.IsCommand(card) ? card.Family : null);

    private static bool Compare(JsonElement? actual, string op, JsonElement? expected)
    {
        if (actual is { ValueKind: JsonValueKind.Number } a && expected is { ValueKind: JsonValueKind.Number } e)
        {
            var (x, y) = (a.GetDouble(), e.GetDouble());
            return op switch
            {
                "==" => x == y,
                "!=" => x != y,
                "<" => x < y,
                "<=" => x <= y,
                ">" => x > y,
                ">=" => x >= y,
                _ => false,
            };
        }

        // Booleans and text: equality only. A variable never set equals false.
        var left = actual?.GetRawText() ?? "false";
        var right = expected?.GetRawText() ?? "null";
        return op switch
        {
            "==" => left == right,
            "!=" => left != right,
            _ => false,
        };
    }
}
