using System.Text.Json;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>Scenario actions (data model §3.6 to §3.8).</summary>
internal sealed partial class Session
{
    private void Do(ScenarioAction action)
    {
        switch (action.Do)
        {
            case InternalActions.AwaitContinue:
                Ask(PromptKinds.Continue, null, [new PromptOption("continue")]);
                break;
            case "goToNode":
                GoToNode(action.Node);
                break;
            case "showText":
                Log("text", action.Text);
                break;
            case "gainGold":
                State.Campaign.Gold += action.Amount ?? 0;
                Log("gainGold", args: Args(("amount", Number(action.Amount)), ("gold", Number(State.Campaign.Gold))));
                break;
            case "gainXp":
                GainXp(action);
                break;
            case "gainItem":
                GainItem(action);
                break;
            case "restore":
                Log("restoreAdventurers");
                ReturnLootCards();
                break;
            case "revealHiddenCard":
                State.Campaign.RevealedHiddenCards.Add($"{action.Size}:{action.Code}");
                Log("revealHiddenCard", args: Args(("size", action.Size), ("code", action.Code)));
                break;
            case "setFlag":
                SetVariable(ExtraString(action, "flag"), JsonSerializer.SerializeToElement(true));
                break;
            case "addVar":
                // "from": the value of another variable (the score the GM typed), else "amount"
                var amount = action.From is { } from
                    ? (Variable(from) is { ValueKind: JsonValueKind.Number } number ? number.GetInt32() : 0)
                    : action.Amount ?? 1;
                AddToVariable(ExtraString(action, "var"), amount, ExtraNumber(action, "min"));
                break;
            case "branch":
                Push((action.If ?? []).All(IsTrue) ? action.Then ?? [] : action.Else ?? []);
                break;
            case "askNumber":
                Ask(PromptKinds.Number, action.Question ?? action.Text, [], action);
                break;
            case "setVar":
                SetVariable(ExtraString(action, "var"), action.Extra?.TryGetValue("value", out var value) == true ? value : JsonSerializer.SerializeToElement(true));
                break;
            case "askChoice":
                Ask(PromptKinds.Choice, action.Question,
                    (action.Options ?? []).Select((o, i) => new PromptOption(i.ToString(System.Globalization.CultureInfo.InvariantCulture), o.Label)), action);
                break;
            case "askSkillCheck":
                AskSkillCheck(action);
                break;
            case "readHiddenRule" when State.Encounter is not null:
                ReadHiddenRule(action.Rule);
                break;
            case "placeToken" when State.Encounter is not null:
                PlaceToken(action);
                break;
            case "removeToken" when State.Encounter is not null:
                RemoveToken(action.Token);
                break;
            case "activateToken" when State.Encounter is not null:
                ActivateToken(action.Token!);
                break;
            case "spawn" when State.Encounter is not null:
                if (action.Fallback is { Count: > 0 })
                {
                    // The app does not see the board: the GM says whether the figures fit.
                    Ask(PromptKinds.SpawnPossible, null, [new PromptOption("yes"), new PromptOption("no")], action);
                }
                else
                {
                    Spawn(action, fromSetup: false);
                }

                break;
            case "appendAiStep" when State.Encounter is not null && action.Family is not null && action.Step is not null:
                if (!Encounter.ExtraAiSteps.TryGetValue(action.Family, out var steps))
                {
                    Encounter.ExtraAiSteps[action.Family] = steps = [];
                }

                steps.Add(action.Step);
                Log("appendAiStep", action.Step.Condition.Text, Args(("family", action.Family)));
                break;
            case "revealArea" when State.Encounter is not null:
                RevealArea(ExtraString(action, "area"));
                break;
            case "placeDiagram" when State.Encounter is not null:
                PlaceDiagram(action.Diagram);
                break;
            case "navigate" when State.Encounter is not null:
                Navigate(action.Diagram);
                break;
            case "addEndCondition" when State.Encounter is not null && action.EndCondition is not null:
                if (!Encounter.ActiveEndConditions.Contains(action.EndCondition))
                {
                    Encounter.ActiveEndConditions.Add(action.EndCondition);
                }

                Log("addEndCondition", EncounterNode.EndConditions.FirstOrDefault(e => e.Id == action.EndCondition)?.Text, Args(("endCondition", action.EndCondition)));
                break;
            case "endEncounter" when State.Encounter is not null && action.EndCondition is not null:
                EndEncounter(action.EndCondition);
                break;
            case "addUrgency" when State.Encounter is not null:
                AddUrgency(action.Amount ?? 1);
                break;
            case "restoreCombatant" when State.Encounter is not null:
                if (action.Damage is { ValueKind: JsonValueKind.Number } damage)
                {
                    RestoreCombatant(action, damage.GetInt32());
                }
                else
                {
                    Ask(PromptKinds.Number, Text($"How much Damage does {action.Instance} have?", $"Combien de dégâts a {action.Instance} ?"), [], action, action.Instance);
                }

                break;
            case "replaceInitiativeCard" when State.Encounter is not null && action.From is not null && action.To is not null:
                ReplaceInitiativeCard(action.From, action.To);
                break;
            case "defeatAdventurer":
                if (action.Who is { ValueKind: JsonValueKind.String } who && who.GetString() == "askAdventurer")
                {
                    Ask(PromptKinds.Adventurer, Text("Which Adventurer is Defeated?", "Quel aventurier est vaincu ?"),
                        State.Campaign.Party.Select(a => new PromptOption(a.Name)));
                }
                else
                {
                    Log("adventurerDefeated", args: Args(("who", Json(action.Who))));
                    Fire(e => e.Trigger.On == "adventurerDefeated");
                }

                break;
            default:
                // Not handled yet: shown to the GM, who applies it by hand.
                Log("manualAction", action.Text, Args(("do", action.Do), ("action", JsonSerializer.Serialize(action, Common.JsonDefaults.Options))));
                break;
        }
    }

    private void GainXp(ScenarioAction action)
    {
        var amount = action.Amount ?? 0;
        if (action.Each == true)
        {
            State.Campaign.Party.ForEach(a => a.Xp += amount);
        }

        Log("gainXp", args: Args(("amount", Number(amount)), ("each", action.Each == true ? "true" : null)));
    }

    private void GainItem(ScenarioAction action)
    {
        var description = action.Ref
                          ?? $"{action.Count ?? 1} {string.Join("/", action.Tiers ?? [])} {action.Type}".Trim();
        State.Campaign.Items.Add(description);
        Log("gainItem", args: Args(("item", action.Ref), ("count", Number(action.Count)), ("tiers", action.Tiers is null ? null : string.Join("/", action.Tiers)), ("type", action.Type)));
    }

    private void SetVariable(string? name, JsonElement value)
    {
        if (name is not null)
        {
            State.Campaign.Variables[name] = value;
            Log("setVariable", args: Args(("name", name), ("value", value.GetRawText())));
        }
    }

    /// <summary>A counter of the campaign (the MAST grade…): adds, never below <paramref name="min"/> when given.</summary>
    private void AddToVariable(string? name, int amount, int? min)
    {
        if (name is null)
        {
            return;
        }

        var current = State.Campaign.Variables.TryGetValue(name, out var stored) && stored.ValueKind == JsonValueKind.Number ? stored.GetInt32() : 0;
        var next = current + amount;
        if (min is { } floor && next < floor)
        {
            next = floor;
        }

        SetVariable(name, JsonSerializer.SerializeToElement(next));
    }

    private static int? ExtraNumber(ScenarioAction action, string key) =>
        action.Extra?.TryGetValue(key, out var value) == true && value.TryGetInt32(out var number) ? number : null;

    private void AskSkillCheck(ScenarioAction action)
    {
        var options = (action.Options ?? [])
            .Select((o, i) => new PromptOption($"pass-{i}", Text($"{o.Skill} {o.Difficulty}: passed", $"{o.Skill} {o.Difficulty} : réussi")))
            .Concat(action.Masterful is null ? [] : [new PromptOption("masterful", Text("Masterful Pass", "Réussite magistrale"))])
            .Append(new PromptOption("fail", Text("Failed", "Raté")));
        var who = action.Who is { ValueKind: JsonValueKind.Object } w ? w.Deserialize<Dictionary<string, string>>() : null;
        Ask(PromptKinds.SkillCheck, who, options, action);
    }

    private void AnswerSkillCheck(ScenarioAction action, string option)
    {
        if (option == "fail")
        {
            Push(action.Fail ?? []);
            return;
        }

        if (option == "masterful")
        {
            Push(action.Masterful ?? []);
            return;
        }

        var chosen = action.Options![int.Parse(option["pass-".Length..], System.Globalization.CultureInfo.InvariantCulture)];
        Push(chosen.Pass ?? action.Pass ?? []);
    }

    private void ReadHiddenRule(string? id)
    {
        var rule = EncounterNode.HiddenRules.FirstOrDefault(r => r.Id == id);
        if (rule is null)
        {
            Log("unknownHiddenRule", args: Args(("rule", id)));
            return;
        }

        Encounter.ReadHiddenRules.Add(rule.Id);
        Log("hiddenRule", rule.Text, Args(("rule", rule.Id), ("name", rule.Name?.GetValueOrDefault("en"))));
        Push(rule.Actions);
        Fire(e => e.Trigger.On == "hiddenRuleRead" && (e.Trigger.Rule is null || e.Trigger.Rule == rule.Id));
    }

    /// <summary>
    /// A random loot token: one of those the encounter describes, not on the board yet (nor placed by the same map).
    /// Null when none is left.
    /// </summary>
    private string? RandomColor(string type, IReadOnlySet<string>? alsoTaken = null)
    {
        var onBoard = Encounter.Tokens.Where(t => !t.Removed).Select(t => t.Key).ToHashSet();
        var candidates = EncounterNode.Tokens.Keys
            .Where(k => k.StartsWith($"{type}:", StringComparison.Ordinal) && !onBoard.Contains(k) && alsoTaken?.Contains(k) != true)
            .ToList();
        if (candidates.Count == 0)
        {
            Log("noTokenLeft", args: Args(("type", type)));
            return null;
        }

        var color = candidates[NextRandom(candidates.Count)][(type.Length + 1)..];
        Log("randomToken", args: Args(("token", $"{type}:{color}")));
        return color;
    }

    private void PlaceToken(ScenarioAction action)
    {
        var type = TokenType(action.Type ?? "token");
        var color = action.Color;
        if (color == "random")
        {
            color = RandomColor(type);
            if (color is null)
            {
                return;
            }
        }

        var key = color is null ? type : $"{type}:{color}";
        var token = new BoardToken
        {
            Id = $"placed-{Encounter.Tokens.Count + 1}",
            Key = key,
            Type = action.Type,
            Color = color,
            Area = Locate(action.At) is { } cell ? new Rect(cell.X, cell.Y) : null,
        };
        Encounter.Tokens.Add(token);
        Log("placeToken", args: Args(("token", key), ("id", token.Id), ("at", Json(action.At))));
    }

    private void RemoveToken(string? id)
    {
        foreach (var token in Encounter.Tokens.Where(t => t.Id == id && !t.Removed))
        {
            token.Removed = true;
            Log("removeToken", args: Args(("id", token.Id), ("token", token.Key)));
        }
    }

    private void PlaceDiagram(string? id)
    {
        var diagram = EncounterNode.Diagrams.FirstOrDefault(d => d.Id == id);
        if (diagram is null || Encounter.PlacedDiagrams.Contains(diagram.Id))
        {
            return;
        }

        Log("placeDiagram", diagram.Label, Args(("diagram", diagram.Id), ("page", Number(diagram.Page))));
        LayDiagram(diagram);
        foreach (var placement in diagram.Setup?.Combatants ?? [])
        {
            Spawn(SpawnOf(placement), fromSetup: false, placement.InitiativeCard);
        }
    }

    /// <summary>Its tiles and tokens join the table; its Terrain replaces the Terrain under it when it says so.</summary>
    private void LayDiagram(Diagram diagram)
    {
        Encounter.PlacedDiagrams.Add(diagram.Id);
        if (diagram.ReplacesTerrain == true)
        {
            var covered = (diagram.Map?.Tokens ?? []).Where(t => IsTerrain(t.Type)).SelectMany(t => t.Footprint.Cells()).ToHashSet();
            foreach (var token in Encounter.Tokens.Where(t => !t.Removed && IsTerrain(t.Type) && t.Area is { } area && area.Cells().Any(covered.Contains)))
            {
                token.Removed = true;
                Log("terrainReplaced", args: Args(("id", token.Id), ("token", token.Key)));
            }
        }

        AddTokens(diagram.Map);
        RevealAtStart(diagram.Map);
        foreach (var rule in diagram.SpecialRules)
        {
            Log("specialRule", rule.Text, Args(("rule", rule.Id), ("name", rule.Name.GetValueOrDefault("en"))));
        }
    }

    /// <summary>
    /// The NAVIGATE tag (data model §3.7.1): the turn ends, the board is cleared (its figures are not Defeated),
    /// the diagram is set up with the adventurers on its starting zone, then the initiative track is rebuilt,
    /// shuffled, and the round goes on from its first turn. Round number, Urgency and defeated adventurers stay.
    /// </summary>
    private void Navigate(string? id)
    {
        var diagram = EncounterNode.Diagrams.FirstOrDefault(d => d.Id == id);
        if (diagram is null)
        {
            return;
        }

        var encounter = Encounter;
        Log("navigate", diagram.Label, Args(("diagram", diagram.Id), ("page", Number(diagram.Page))));
        encounter.Activation = null;
        encounter.MapCleared = true;
        encounter.PlacedDiagrams.Clear();
        foreach (var token in encounter.Tokens.Where(t => !t.Removed))
        {
            token.Removed = true;
        }

        encounter.Combatants.RemoveAll(c => !c.Defeated);
        encounter.InitiativeTrack = State.Campaign.Party.Select(a => AdventurerCard + a.Name).ToList();
        encounter.CurrentTurn = -1;
        LayDiagram(diagram);
        PlaceAdventurers(diagram.Map, diagram.Setup);
        foreach (var placement in diagram.Setup?.Combatants ?? [])
        {
            Spawn(SpawnOf(placement), fromSetup: true, placement.InitiativeCard, possibleSpawns: true);
        }

        if (encounter.Round > 0)
        {
            ShuffleTrack();
            encounter.CurrentTurn = 0;
            Log("initiativeRebuilt", args: Args(("track", string.Join(", ", encounter.InitiativeTrack))));
            BeginTurn();
        }
    }

    /// <summary>"um1:token/terrain/water" is a Terrain piece.</summary>
    private static bool IsTerrain(string? type) => type?.Split('/') is { Length: > 2 } kind && kind[^2] == "terrain";

    private void AddTokens(BoardMap? map)
    {
        var tokens = map?.Tokens ?? [];
        var explicitKeys = tokens.Where(t => t.Color is not null and not "random").Select(t => $"{TokenType(t.Type)}:{t.Color}").ToHashSet();
        foreach (var token in tokens)
        {
            var type = TokenType(token.Type);
            var color = token.Color == "random" ? RandomColor(type, explicitKeys) : token.Color;
            Encounter.Tokens.Add(new BoardToken
            {
                Id = token.Id,
                Key = color is null ? type : $"{type}:{color}",
                Type = token.Type,
                Color = color,
                Image = token.Image,
                Area = token.Footprint,
            });
        }
    }

    /// <summary>
    /// The square an action's "at" designates: explicit coordinates, or the square of a token ("token:totem:blue")
    /// or of a combatant ("instance:terror"). Null when only described in words (adjacent to…, any space on a tile…).
    /// </summary>
    private Cell? Locate(JsonElement? at)
    {
        if (at is not { ValueKind: JsonValueKind.Object } where)
        {
            return null;
        }

        if (where.TryGetProperty("x", out var x) && where.TryGetProperty("y", out var y)
            && x.TryGetInt32(out var column) && y.TryGetInt32(out var row))
        {
            return new Cell(column, row);
        }

        if (!where.TryGetProperty("on", out var on) || on.GetString() is not { } target)
        {
            return null;
        }

        if (target.StartsWith("token:", StringComparison.Ordinal))
        {
            var key = target["token:".Length..];
            return Encounter.Tokens.LastOrDefault(t => t.Key == key && t.Area is not null)?.Area is { } area ? new Cell(area.X, area.Y) : null;
        }

        return target.StartsWith("instance:", StringComparison.Ordinal) ? Find(target["instance:".Length..])?.Position : null;
    }

    /// <summary>"um1:token/objective" → "objective".</summary>
    private static string TokenType(string type) => type[(type.LastIndexOf('/') + 1)..];

    private static string? ExtraString(ScenarioAction action, string key) =>
        action.Extra?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
