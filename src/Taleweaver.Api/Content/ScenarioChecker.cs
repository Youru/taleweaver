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
/// unique ids, resolved references, combatant cards for the loot level, pieces placed on the tiles,
/// and what the engine really plays (actions, triggers, token modes, spots, fog areas, typed
/// conditions, figures beyond the numbered cards, hidden cards).
/// Errors make the scenario unplayable; warnings do not.
/// </summary>
public static class ScenarioChecker
{
    private static readonly HashSet<string> CatalogKinds = ["combatant", "item", "item-upgrade", "tile", "token", "initiative"];
    private static readonly HashSet<string> BlockingTerrain = ["obstructing", "dangerous"];

    /// <summary>The actions the engine plays (Engine/Session.Actions.cs); any other is skipped in play.</summary>
    public static readonly IReadOnlySet<string> EngineActions = new HashSet<string>
    {
        "goToNode", "showText", "gainGold", "gainXp", "gainItem", "restore", "revealHiddenCard", "setFlag", "setVar",
        "askChoice", "askSkillCheck", "readHiddenRule", "placeToken", "removeToken", "activateToken", "spawn",
        "appendAiStep", "revealArea", "placeDiagram", "addEndCondition", "endEncounter", "addUrgency",
        "restoreCombatant", "replaceInitiativeCard", "defeatAdventurer",
    };

    /// <summary>The triggers the engine fires.</summary>
    public static readonly IReadOnlySet<string> Triggers = new HashSet<string>
    {
        "tokenActivated", "encounterStart", "roundStart", "roundEnd", "combatantDefeated", "adventurerDefeated",
        "areaRevealed", "hiddenRuleRead",
    };

    /// <summary>How an Adventurer activates a token (shown to the GM).</summary>
    public static readonly IReadOnlySet<string> TokenModes = new HashSet<string>
    {
        "enter", "adjacent", "onSpaceEncounterAction", "adjacentEncounterAction", "endTurnOn", "none", "adventure",
    };

    /// <summary>Sceneries that meet naturally; tiles of two groups in one encounter need a transition thought out.</summary>
    private static readonly Dictionary<string, string> ThemeGroups = new()
    {
        ["rocky-trail"] = "woods", ["forest"] = "woods", ["marsh"] = "woods", ["meadow"] = "meadow",
        ["cave"] = "underground", ["dark-cave"] = "underground", ["mine"] = "underground",
        ["crypt"] = "stone", ["ruins"] = "stone", ["tavern"] = "tavern",
    };

    private static readonly HashSet<string> BuiltInVariables = ["round", "urgency", "gold"];
    private static readonly string[] BooleanPrefixes = ["placed.", "activated.", "revealed."];
    private static readonly string[] CountPrefixes = ["defeated.", "alive."];

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

        /// <summary>Variables that hold true or false: declared flags, and those raised by setFlag.</summary>
        private readonly HashSet<string> _flags = [];

        /// <summary>Every variable the scenario declares or sets.</summary>
        private readonly HashSet<string> _variables = [];

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

            CollectVariables(scenario);
            foreach (var node in scenario.Nodes)
            {
                foreach (var action in ScenarioAction.Flatten(node.Mechanics))
                {
                    CheckKnownAction(node.Id, action);
                }

                // next, the end conditions, and every goToNode of the node (choices, skill checks, events)
                var jumps = ScenarioAction.Flatten(node.Mechanics
                        .Concat(node.EndConditions.SelectMany(e => e.Mechanics))
                        .Concat(node.Events.SelectMany(e => e.Actions)))
                    .Where(a => a.Do == "goToNode")
                    .Select(a => a.Node);
                var targets = node.EndConditions.Select(e => e.Next).Prepend(node.Next).Concat(jumps);
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

        private void CollectVariables(Scenario scenario)
        {
            foreach (var variable in scenario.Variables.Where(v => v.ValueKind == JsonValueKind.Object))
            {
                if (variable.TryGetProperty("id", out var id) && id.GetString() is { } name)
                {
                    _variables.Add(name);
                    if (variable.TryGetProperty("type", out var type) && type.GetString() == "flag")
                    {
                        _flags.Add(name);
                    }
                }
            }

            var actions = scenario.Nodes.SelectMany(n => ScenarioAction.Flatten(n.Mechanics
                .Concat(n.EndConditions.SelectMany(e => e.Mechanics))
                .Concat(n.Events.SelectMany(e => e.Actions))
                .Concat(n.HiddenRules.SelectMany(r => r.Actions))));
            foreach (var action in actions)
            {
                if (action.Do == "setFlag" && Extra(action, "flag") is { } flag)
                {
                    _flags.Add(flag);
                }
                else if (action.Do == "setVar" && Extra(action, "var") is { } variable)
                {
                    _variables.Add(variable);
                }
            }

            _variables.UnionWith(_flags);
        }

        private void CheckKnownAction(string where, ScenarioAction action)
        {
            if (!EngineActions.Contains(action.Do))
            {
                Warn("UnknownAction", where, $"action '{action.Do}' is skipped by the engine");
            }
        }

        /// <summary>Variables compared as they hold: true/false ones only with == or != against a boolean.</summary>
        private void CheckConditions(string where, IEnumerable<EventCondition> conditions, HashSet<string> tokenKeys, HashSet<string> areas, HashSet<string> diagrams)
        {
            foreach (var condition in conditions.SelectMany(Simple))
            {
                if (condition.Var is not { } name)
                {
                    continue;
                }

                var op = condition.Op ?? "==";
                var boolean = BooleanPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)) || _flags.Contains(name);
                if (boolean && (op is not ("==" or "!=") || condition.Value is { ValueKind: not (JsonValueKind.True or JsonValueKind.False) }))
                {
                    Error("BadCondition", where, $"condition '{name} {op} {condition.Value?.GetRawText()}': it is true or false, compare it with == true / false");
                }

                var rest = name.Contains('.', StringComparison.Ordinal) ? name[(name.IndexOf('.', StringComparison.Ordinal) + 1)..] : "";
                if (name.StartsWith("activated.", StringComparison.Ordinal) && !tokenKeys.Contains(rest))
                {
                    Warn("AbsentToken", where, $"condition on a token never on the board '{rest}'");
                }
                else if (name.StartsWith("revealed.", StringComparison.Ordinal) && !areas.Contains(rest))
                {
                    Error("UnknownArea", where, $"condition on an unknown fog area '{rest}'");
                }
                else if (name.StartsWith("placed.", StringComparison.Ordinal) && !diagrams.Contains(rest))
                {
                    Error("UnknownDiagram", where, $"condition on an unknown diagram '{rest}'");
                }
                else if (!BuiltInVariables.Contains(name) && !BooleanPrefixes.Concat(CountPrefixes).Any(p => name.StartsWith(p, StringComparison.Ordinal))
                         && !_variables.Contains(name))
                {
                    Warn("UnknownVariable", where, $"variable never declared nor set '{name}'");
                }
            }
        }

        private static IEnumerable<EventCondition> Simple(EventCondition condition) =>
            new[] { condition }
                .Concat((condition.All ?? []).SelectMany(Simple))
                .Concat((condition.Any ?? []).SelectMany(Simple))
                .Concat(condition.Not is { } not ? Simple(not) : []);

        private static string? Extra(ScenarioAction action, string key) =>
            action.Extra?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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

            // Fog areas: declared before tiles and tokens refer to them
            var areas = boards.SelectMany(b => b.Map.Areas).Select(a => a.Id).ToHashSet();
            foreach (var (name, map, _, _) in boards)
            {
                foreach (var area in map.Tiles.Select(t => t.Area).Concat(map.Tokens.Select(t => t.Area)).OfType<string>().Distinct().Where(a => !areas.Contains(a)))
                {
                    Error("UnknownArea", $"{id}/{name}", $"fog area not declared in map.areas '{area}'");
                }
            }

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

            foreach (var (key, rule) in encounter.Tokens.Where(t => t.Value.Mode is { } mode && !TokenModes.Contains(mode)))
            {
                Warn("UnknownTokenMode", id, $"tokens.{key}: unknown activation mode '{rule.Mode}'");
            }

            foreach (var ev in encounter.Events)
            {
                if (!Triggers.Contains(ev.Trigger.On))
                {
                    Warn("UnknownTrigger", $"{id}/{ev.Id}", $"trigger '{ev.Trigger.On}' is never fired by the engine");
                }

                if (ev.Trigger.On == "areaRevealed" && ev.Trigger.Area is { } area && !areas.Contains(area))
                {
                    Error("UnknownArea", $"{id}/{ev.Id}", $"unknown fog area '{area}'");
                }

                CheckConditions($"{id}/{ev.Id}", ev.Conditions, knownKeys.Concat(encounter.Tokens.Keys).ToHashSet(), areas, diagrams);
            }

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
                CheckKnownAction(id, action);
                if (action.Do == "revealArea" && !areas.Contains(Extra(action, "area") ?? ""))
                {
                    Error("UnknownArea", id, $"revealArea: unknown fog area '{Extra(action, "area")}'");
                }

                if (action.Do is "spawn" or "placeToken" && action.At is { } spot && !Understood(spot))
                {
                    Error("BadSpot", id, $"{action.Do}: spot not understood {spot.GetRawText()} (expected {{x, y}}, {{on: \"token:…\"}}, {{adjacentTo: …}} or {{where: …}})");
                }
            }

            CheckTiles(id, boards.SelectMany(b => b.Map.Tiles).ToList());
            CheckGeometry(id, boards.Select(b => (b.Name, b.Map, b.Setup, b.ReplacesTerrain)).ToList());
        }

        /// <summary>A spot the engine places ({x, y}, on a token or a figure) or leaves to the GM (next to, where).</summary>
        private static bool Understood(JsonElement spot) =>
            spot.ValueKind == JsonValueKind.Object
            && ((spot.TryGetProperty("x", out _) && spot.TryGetProperty("y", out _))
                || new[] { "on", "adjacentTo", "tile", "where" }.Any(k => spot.TryGetProperty(k, out _)));

        /// <summary>Each combatant must resolve: a known card (ref), or a family with a card at the loot level.</summary>
        private void CheckCombatants(Node encounter, IEnumerable<CombatantPlacement> placed, IEnumerable<ScenarioAction> actions)
        {
            var placedList = placed.ToList();
            var spawns = actions.Where(a => a.Do == "spawn").ToList();
            CheckFamilyCounts(encounter, placedList.Select(c => (c.Family, 1)).Concat(spawns.Select(a => (a.Family, a.Count ?? 1))));
            var used = placedList.Select(c => (c.Ref, c.Family))
                .Concat(spawns.Select(a => (a.Ref, a.Family)));
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

        /// <summary>
        /// More figures of a family than its numbered cards at the loot level: a spawn fails while none is
        /// defeated. And a hidden card (a boss, often) is a spoiler worth knowing.
        /// </summary>
        private void CheckFamilyCounts(Node encounter, IEnumerable<(string? Family, int Count)> wanted)
        {
            foreach (var group in wanted.Where(w => w.Family is not null).GroupBy(w => w.Family!))
            {
                if (catalog.CardFor(group.Key, encounter.LootLevel) is not { } card)
                {
                    continue;
                }

                var numbers = catalog.IntelligentCards(group.Key)
                    .Where(c => c.LootLevel == card.LootLevel)
                    .SelectMany(c => Engine.Session.ActivationNumbers(c.Activation))
                    .Distinct()
                    .Count();
                var total = group.Sum(w => w.Count);
                if (numbers > 0 && total > numbers)
                {
                    Warn("BeyondCards", encounter.Id,
                        $"{total} × {group.Key} planned (setup and spawns), but {numbers} numbered card(s) at level {card.LootLevel}: beyond that, a spawn fails while none is defeated");
                }

                if (card.Source?.StartsWith("hidden-cards", StringComparison.Ordinal) == true)
                {
                    Warn("HiddenCard", encounter.Id, $"{group.Key} → {card.Id} is a hidden card (spoiler, often a boss)");
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

        /// <summary>
        /// The tiles of an encounter (map and diagrams): each one is in the box once, front or back; their
        /// sceneries should meet; and they hold together, or some figures could never reach the others.
        /// </summary>
        private void CheckTiles(string id, List<MapTile> laid)
        {
            var tiles = catalog.Packs.SelectMany(p => p.Tiles).ToDictionary(t => t.Id);
            foreach (var twice in laid.GroupBy(t => t.Ref).Where(g => g.Count() > 1))
            {
                Error("TileTwice", id, $"tile laid twice '{twice.Key}' (there is one in the box)");
            }

            var refs = laid.Select(t => t.Ref).ToHashSet();
            foreach (var reference in refs.Order(StringComparer.Ordinal))
            {
                if (tiles.GetValueOrDefault(reference)?.PairedSide is { } other && refs.Contains(other)
                    && string.CompareOrdinal(reference, other) < 0)
                {
                    Error("BothSides", id, $"'{reference}' and '{other}' are the two sides of one tile");
                }
            }

            var sceneries = laid.Select(t => tiles.GetValueOrDefault(t.Ref)).OfType<Tile>().Where(t => t.Theme is not null).DistinctBy(t => t.Id).ToList();
            if (sceneries.Select(t => ThemeGroups.GetValueOrDefault(t.Theme!, t.Theme!)).Distinct().Count() > 1)
            {
                Warn("ThemesApart", id, $"sceneries that do not meet ({string.Join(", ", sceneries.Select(t => $"{t.Name} {t.Theme}"))}): check them with npm run map-preview");
            }

            if (laid.Count > 0)
            {
                var corners = laid.Select(t => (t.X, t.Y)).Distinct().ToList();
                var reached = new HashSet<(int X, int Y)> { corners[0] };
                var queue = new Queue<(int X, int Y)>(reached);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var next in corners.Where(c => !reached.Contains(c) && Touch(current, c)))
                    {
                        reached.Add(next);
                        queue.Enqueue(next);
                    }
                }

                var alone = corners.Where(c => !reached.Contains(c)).ToList();
                if (alone.Count > 0)
                {
                    Warn("TilesApart", id, $"tiles apart from the rest of the board at {string.Join(", ", alone.Select(c => $"({c.X}, {c.Y})"))}: no way through");
                }
            }
        }

        /// <summary>Two tiles meet when they share part of an edge.</summary>
        private static bool Touch((int X, int Y) a, (int X, int Y) b) =>
            (Math.Abs(a.X - b.X) == Tile.SquaresPerSide && Math.Abs(a.Y - b.Y) < Tile.SquaresPerSide)
            || (Math.Abs(a.Y - b.Y) == Tile.SquaresPerSide && Math.Abs(a.X - b.X) < Tile.SquaresPerSide);

        /// <summary>The map and every diagram add up on the board: pieces must be on a tile, figures not on blocking terrain.</summary>
        private void CheckGeometry(string id, List<(string Name, BoardMap Map, Setup Setup, bool ReplacesTerrain)> boards)
        {
            var board = boards
                .SelectMany(b => b.Map.Tiles)
                .SelectMany(t => new Rect(t.X, t.Y, Tile.SquaresPerSide, Tile.SquaresPerSide).Cells())
                .ToHashSet();
            var tiles = catalog.Packs.SelectMany(p => p.Tiles).ToDictionary(t => t.Id);
            var printed = new Dictionary<Cell, (string Token, string Kind)>();
            foreach (var laid in boards.SelectMany(b => b.Map.Tiles))
            {
                if (tiles.GetValueOrDefault(laid.Ref) is { } tile)
                {
                    foreach (var (cell, kind) in tile.PrintedTerrain(laid))
                    {
                        printed[cell] = ($"printed on {tile.Name}", kind);
                    }
                }
            }

            var terrain = new Dictionary<Cell, (string Token, string Kind)>();
            (string Token, string Kind)? Under(Cell cell) =>
                terrain.TryGetValue(cell, out var token) ? token : printed.TryGetValue(cell, out var square) ? square : null;
            foreach (var (name, map, setup, replacesTerrain) in boards)
            {
                var where = $"{id}/{name}";
                foreach (var token in map.Tokens)
                {
                    var cells = token.Footprint.Cells().ToList();
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
                    else if (Under(at) is { } under && BlockingTerrain.Contains(under.Kind))
                    {
                        Warn("OnBlockingTerrain", where, $"{who} #{i + 1} is placed on {under.Kind} ({under.Token}) at {at}");
                    }
                }
            }

            CheckPassages(id, boards[0], board, cell => Under(cell)?.Kind);
        }

        /// <summary>
        /// The adventurers can walk (orthogonal steps, around Obstructing and Dangerous Terrain, printed or
        /// laid) from their starting zone to every token, figure and spawn zone of the map. Only the
        /// printed terrain that was surveyed counts: an unsurveyed tile looks open.
        /// </summary>
        private void CheckPassages(string id, (string Name, BoardMap Map, Setup Setup, bool ReplacesTerrain) main, HashSet<Cell> board, Func<Cell, string?> kindAt)
        {
            bool Open(Cell c) => board.Contains(c) && !(kindAt(c) is { } kind && BlockingTerrain.Contains(kind));
            bool Pit(Cell c) => board.Contains(c) && kindAt(c) == "dangerous";
            (int X, int Y)[] steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];
            IEnumerable<Cell> Around(Cell c) => steps.Select(s => new Cell(c.X + s.X, c.Y + s.Y));

            var start = main.Map.Zones.Where(z => z.Kind == "startingZone").SelectMany(z => z.Rect.Cells()).Where(Open).ToHashSet();
            if (start.Count == 0)
            {
                return;
            }

            // On foot, then Jumping too (rulebook p. 68): in a straight line over 1 or 2 Dangerous
            // spaces, out of a Normal space, onto an open one
            HashSet<Cell> Walk(bool jump)
            {
                var reached = new HashSet<Cell>(start);
                var queue = new Queue<Cell>(start);
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    var next = Around(at).Where(Open).ToList();
                    if (jump && kindAt(at) is null)
                    {
                        foreach (var (dx, dy) in steps)
                        {
                            for (var gap = 1; gap <= 2 && Pit(new Cell(at.X + dx * gap, at.Y + dy * gap)); gap++)
                            {
                                next.Add(new Cell(at.X + dx * (gap + 1), at.Y + dy * (gap + 1)));
                            }
                        }
                    }

                    foreach (var cell in next.Where(n => Open(n) && reached.Add(n)))
                    {
                        queue.Enqueue(cell);
                    }
                }

                return reached;
            }

            var walked = Walk(jump: false);
            var jumped = Walk(jump: true);

            // A piece is reached when an adventurer can stand on it or next to it
            bool Reached(HashSet<Cell> reached, IEnumerable<Cell> cells) => cells.Any(c => reached.Contains(c) || Around(c).Any(reached.Contains));
            var targets = main.Map.Tokens
                .Where(t => t.Type.Split('/') is not { Length: > 2 } kind || kind[^2] != "terrain")
                .Select(t => (Name: $"token '{t.Id}'", Cells: t.Footprint.Cells().ToList()))
                .Concat(main.Map.Zones.Where(z => z.Kind is "spawn" or "exit").Select(z => (Name: $"zone '{z.Id}'", Cells: z.Rect.Cells().ToList())))
                .Concat(main.Setup.Combatants.Select((c, i) => (c, i)).Where(p => p.c.At is not null)
                    .Select(p => (Name: $"{p.c.Instance ?? p.c.Family ?? p.c.Ref} #{p.i + 1}", Cells: new List<Cell> { p.c.At!.Value })))
                .ToList();
            var cut = targets.Where(t => !Reached(jumped, t.Cells)).Select(t => t.Name).ToList();
            if (cut.Count > 0)
            {
                Warn("NoWayThrough", $"{id}/{main.Name}",
                    $"no way from the starting zone to {string.Join(", ", cut)} (printed walls, Obstructing Terrain, pits too wide to Jump): check with npm run map-preview");
            }

            var leaps = targets.Where(t => !Reached(walked, t.Cells) && Reached(jumped, t.Cells)).Select(t => t.Name).ToList();
            if (leaps.Count > 0)
            {
                Warn("JumpNeeded", $"{id}/{main.Name}",
                    $"only by Jumping over Dangerous Terrain from the starting zone to {string.Join(", ", leaps)}: is it meant?");
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
