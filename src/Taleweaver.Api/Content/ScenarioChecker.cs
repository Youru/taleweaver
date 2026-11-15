using System.Text.Json;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Content;

/// <summary>A problem found in a scenario. <see cref="Code"/> is stable; <see cref="Where"/> names the node, diagram or event.</summary>
public sealed record ScenarioIssue(string Code, string Where, string Message);

/// <summary>The card the loot level selects for a family placed by family name.</summary>
public sealed record CardChoice(string Encounter, string Family, string CombatantId);

public sealed record ScenarioReport(IReadOnlyList<ScenarioIssue> Errors, IReadOnlyList<ScenarioIssue> Warnings, IReadOnlyList<CardChoice> Cards)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Consistency checks of a scenario against the catalogue (port of tools/check_scenario.py):
/// unique ids, resolved references, combatant cards for the loot level, and pieces placed on the tiles.
/// Errors make the scenario unplayable; warnings do not.
/// </summary>
public static class ScenarioChecker
{
    private static readonly HashSet<string> CatalogKinds = ["combatant", "item", "item-upgrade", "tile", "token", "initiative"];
    private static readonly HashSet<string> BlockingTerrain = ["obstructing", "dangerous"];

    public static ScenarioReport Check(Scenario scenario, Catalog catalog)
    {
        var run = new Run(catalog);
        run.CheckNodes(scenario);
        run.CheckCatalogReferences(scenario);
        return new ScenarioReport(
            run.Errors,
            run.Warnings,
            run.Cards.Distinct().OrderBy(c => c.Encounter, StringComparer.Ordinal).ThenBy(c => c.Family, StringComparer.Ordinal).ToList());
    }

    private sealed class Run(Catalog catalog)
    {
        public List<ScenarioIssue> Errors { get; } = [];

        public List<ScenarioIssue> Warnings { get; } = [];

        public List<CardChoice> Cards { get; } = [];

        private void Error(string code, string where, string message) => Errors.Add(new ScenarioIssue($"Scenarios.{code}", where, message));

        private void Warn(string code, string where, string message) => Warnings.Add(new ScenarioIssue($"Scenarios.{code}", where, message));

        public void CheckNodes(Scenario scenario)
        {
            var ids = scenario.Nodes.Select(n => n.Id).ToList();
            foreach (var duplicate in ids.GroupBy(i => i).Where(g => g.Count() > 1))
            {
                Error("DuplicateNode", "nodes", $"duplicate node id '{duplicate.Key}'");
            }

            if (!ids.Contains(scenario.Start))
            {
                Error("UnknownStart", "start", $"unknown node '{scenario.Start}'");
            }

            foreach (var node in scenario.Nodes)
            {
                var targets = node.EndConditions.Select(e => e.Next).Prepend(node.Next);
                foreach (var target in targets.OfType<string>().Where(t => t.Length > 0 && !t.StartsWith('@') && !ids.Contains(t)))
                {
                    Error("UnknownNext", node.Id, $"unknown next node '{target}'");
                }

                if (node.IsEncounter)
                {
                    CheckEncounter(node);
                }
            }
        }

        /// <summary>Every "pack:kind/name" string of a catalogue kind must exist in the enabled packs.</summary>
        public void CheckCatalogReferences(Scenario scenario)
        {
            var packs = catalog.Packs.Select(p => p.Id).Concat(scenario.Packs).ToHashSet();
            var json = JsonSerializer.SerializeToElement(scenario, JsonDefaults.Options);
            var references = Strings(json)
                .Where(s => ContentId.Parse(s) is { Pack: { } pack, Kind: { } kind } && packs.Contains(pack) && CatalogKinds.Contains(kind))
                .Distinct()
                .Order(StringComparer.Ordinal);
            foreach (var reference in references.Where(r => !catalog.Contains(r)))
            {
                Warn("NotInCatalog", "refs", $"not in the catalogue: '{reference}'");
            }
        }

        private void CheckEncounter(Node encounter)
        {
            var id = encounter.Id;
            var boards = new List<(string Name, BoardMap Map, Setup Setup, bool ReplacesTerrain)>
            {
                ("map", encounter.Map ?? new BoardMap(), encounter.Setup ?? new Setup(), false),
            };
            boards.AddRange(encounter.Diagrams.Select(d => (d.Id, d.Map ?? new BoardMap(), d.Setup ?? new Setup(), d.ReplacesTerrain == true)));
            var diagrams = encounter.Diagrams.Select(d => d.Id).ToHashSet();

            var actions = encounter.Events.SelectMany(e => ScenarioAction.Flatten(e.Actions))
                .Concat(encounter.HiddenRules.SelectMany(r => ScenarioAction.Flatten(r.Actions)))
                .ToList();
            CheckCombatants(encounter, boards.SelectMany(b => b.Setup.Combatants), actions);

            // Tokens on the boards, by id and by "type:colour" key
            var tokenIds = new HashSet<string>();
            var tokenKeys = new HashSet<string>();
            foreach (var (name, map, _, _) in boards)
            {
                foreach (var token in map.Tokens)
                {
                    if (!tokenIds.Add(token.Id))
                    {
                        Error("DuplicateToken", $"{id}/{name}", $"duplicate token '{token.Id}'");
                    }

                    if (token.Color is not null)
                    {
                        tokenKeys.Add(TokenKey(token.Type, token.Color));
                    }
                }
            }

            var events = encounter.Events.Select(e => e.Id).ToHashSet();
            var rules = encounter.HiddenRules.Select(r => r.Id).ToHashSet();
            var ends = encounter.EndConditions.Select(e => e.Id).ToHashSet();

            // Tokens placed during play are known too
            actions.AddRange(encounter.EndConditions.SelectMany(e => ScenarioAction.Flatten(e.Mechanics)));
            var placements = actions.Where(a => a.Do == "placeToken" && a.Type is not null).ToList();
            var knownKeys = placements.Where(a => a.Color is not null and not "random").Select(a => TokenKey(a.Type!, a.Color!)).ToHashSet();
            knownKeys.UnionWith(tokenKeys);
            var randomLoot = placements.Any(a => a.Color == "random");

            foreach (var (key, rule) in encounter.Tokens)
            {
                if (!knownKeys.Contains(key) && !(randomLoot && key.StartsWith("loot:", StringComparison.Ordinal)))
                {
                    Warn("TokenNeverPlaced", id, $"token '{key}' is described but never placed");
                }

                foreach (var ev in rule.Events.Where(e => !events.Contains(e)))
                {
                    Error("UnknownEvent", id, $"tokens.{key}: unknown event '{ev}'");
                }
            }

            foreach (var ev in encounter.Events)
            {
                foreach (var key in (ev.Trigger.Token ?? []).Where(k => !encounter.Tokens.ContainsKey(k)))
                {
                    Error("UndescribedToken", $"{id}/{ev.Id}", $"trigger on a token that is not described '{key}'");
                }
            }

            foreach (var action in actions)
            {
                CheckAction(id, action, rules, diagrams, ends, tokenIds, encounter.Tokens, knownKeys);
            }

            CheckGeometry(id, boards.Select(b => (b.Name, b.Map, b.Setup, b.ReplacesTerrain)).ToList());
        }

        /// <summary>Each combatant must resolve: a known card (ref), or a family with a card at the loot level.</summary>
        private void CheckCombatants(Node encounter, IEnumerable<CombatantPlacement> placed, IEnumerable<ScenarioAction> actions)
        {
            var used = placed.Select(c => (c.Ref, c.Family))
                .Concat(actions.Where(a => a.Do == "spawn").Select(a => (a.Ref, a.Family)));
            foreach (var (reference, family) in used)
            {
                if (reference is not null && catalog.FindCombatant(reference) is null)
                {
                    Error("UnknownCombatant", encounter.Id, $"combatant not in the catalogue '{reference}'");
                }
                else if (family is not null)
                {
                    if (catalog.IntelligentCards(family).Count == 0)
                    {
                        Error("UnknownFamily", encounter.Id, $"family not in the catalogue '{family}'");
                    }
                    else if (catalog.CardFor(family, encounter.LootLevel) is { } card)
                    {
                        Cards.Add(new CardChoice(encounter.Id, family, card.Id));
                    }
                    else
                    {
                        Error("NoCardForLootLevel", encounter.Id, $"no '{family}' card of loot level ≤ {encounter.LootLevel}");
                    }
                }
            }
        }

        private void CheckAction(
            string id,
            ScenarioAction action,
            HashSet<string> rules,
            HashSet<string> diagrams,
            HashSet<string> ends,
            HashSet<string> tokenIds,
            IReadOnlyDictionary<string, TokenRule> describedTokens,
            HashSet<string> knownKeys)
        {
            switch (action.Do)
            {
                case "readHiddenRule" when !rules.Contains(action.Rule ?? ""):
                    Error("UnknownHiddenRule", id, $"unknown hidden rule '{action.Rule}'");
                    break;
                case "placeDiagram" or "navigate" when !diagrams.Contains(action.Diagram ?? ""):
                    Error("UnknownDiagram", id, $"unknown diagram '{action.Diagram}'");
                    break;
                case "endEncounter" or "addEndCondition" when !ends.Contains(action.EndCondition ?? ""):
                    Error("UnknownEndCondition", id, $"unknown end condition '{action.EndCondition}'");
                    break;
                case "removeToken" when !tokenIds.Contains(action.Token ?? ""):
                    Error("UnknownToken", id, $"removeToken: unknown token '{action.Token}'");
                    break;
                case "activateToken" when !describedTokens.ContainsKey(action.Token ?? ""):
                    Error("UndescribedToken", id, $"activateToken: token not described '{action.Token}'");
                    break;
            }

            if (action.At is { ValueKind: JsonValueKind.Object } at)
            {
                foreach (var property in new[] { "on", "adjacentTo" })
                {
                    if (at.TryGetProperty(property, out var target) && target.ValueKind == JsonValueKind.String
                        && target.GetString() is { } value && value.StartsWith("token:", StringComparison.Ordinal)
                        && !knownKeys.Contains(value[6..]))
                    {
                        Error("AbsentToken", id, $"{action.Do}: {property} refers to a token that is never on the board '{value}'");
                    }
                }
            }
        }

        /// <summary>The map and every diagram add up on the board: pieces must be on a tile, figures not on blocking terrain.</summary>
        private void CheckGeometry(string id, List<(string Name, BoardMap Map, Setup Setup, bool ReplacesTerrain)> boards)
        {
            var board = boards
                .SelectMany(b => b.Map.Tiles)
                .SelectMany(t => new Rect(t.X, t.Y, Tile.SquaresPerSide, Tile.SquaresPerSide).Cells())
                .ToHashSet();
            var terrain = new Dictionary<Cell, (string Token, string Kind)>();
            foreach (var (name, map, setup, replacesTerrain) in boards)
            {
                var where = $"{id}/{name}";
                foreach (var token in map.Tokens)
                {
                    var cells = token.Area.Cells().ToList();
                    var outside = cells.Where(c => !board.Contains(c)).Order().ToList();
                    if (outside.Count > 0)
                    {
                        Error("OffBoard", where, $"token '{token.Id}' is off the tiles {string.Join(", ", outside)}");
                    }

                    var kind = token.Type.Split('/');
                    if (kind.Length > 2 && kind[^2] == "terrain")
                    {
                        var terrainKind = kind[^1].Replace("-long", "", StringComparison.Ordinal);
                        foreach (var cell in cells)
                        {
                            if (terrain.TryGetValue(cell, out var existing) && !replacesTerrain)
                            {
                                Warn("TerrainOverlap", where, $"'{token.Id}' overlaps '{existing.Token}' at {cell}");
                            }

                            terrain[cell] = (token.Id, terrainKind);
                        }
                    }
                }

                foreach (var zone in map.Zones.Where(z => !z.Rect.Cells().All(board.Contains)))
                {
                    Error("OffBoard", where, $"zone '{zone.Id}' is off the tiles");
                }

                for (var i = 0; i < setup.Combatants.Count; i++)
                {
                    var combatant = setup.Combatants[i];
                    if (combatant.At is not { } at)
                    {
                        continue;
                    }

                    var who = combatant.Instance ?? combatant.Family ?? combatant.Ref;
                    if (!board.Contains(at))
                    {
                        Error("OffBoard", where, $"{who} #{i + 1} is off the tiles {at}");
                    }
                    else if (terrain.TryGetValue(at, out var under) && BlockingTerrain.Contains(under.Kind))
                    {
                        Warn("OnBlockingTerrain", where, $"{who} #{i + 1} is placed on {under.Kind} ({under.Token}) at {at}");
                    }
                }
            }
        }

        private static string TokenKey(string type, string color) => $"{type[(type.LastIndexOf('/') + 1)..]}:{color}";

        private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Strings(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
            JsonValueKind.String => [element.GetString()!],
            _ => [],
        };
    }
}
