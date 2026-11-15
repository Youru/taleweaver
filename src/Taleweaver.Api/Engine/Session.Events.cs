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

        Push(actions);
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

        if (condition.EndConditionActive is { } end && !Encounter.ActiveEndConditions.Contains(end))
        {
            return false;
        }

        return condition.Var is null || Compare(Variable(condition.Var), condition.Op ?? "==", condition.Value);
    }

    /// <summary>Built-in variables (§3.5, §3.8), then the scenario's own.</summary>
    private JsonElement? Variable(string name)
    {
        var (prefix, rest) = name.IndexOf('.') is var dot and > 0 ? (name[..dot], name[(dot + 1)..]) : (name, "");
        object? value = prefix switch
        {
            "round" => Encounter.Round,
            "urgency" => Encounter.Urgency,
            "gold" => State.Campaign.Gold,
            "placed" => Encounter.PlacedDiagrams.Contains(rest),
            "activated" => Encounter.ActivatedTokens.Contains(rest),
            "revealed" => Encounter.RevealedAreas.Contains(rest),
            "defeated" => Encounter.Combatants.Count(c => c.Defeated && c.Family == rest),
            "alive" => Alive().Count(c => c.Family == rest),
            _ => null,
        };
        if (value is not null)
        {
            return JsonSerializer.SerializeToElement(value);
        }

        return State.Campaign.Variables.TryGetValue(name, out var stored) ? stored : null;
    }

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
