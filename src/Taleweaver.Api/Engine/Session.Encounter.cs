using System.Text.RegularExpressions;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>Encounters: setup, rounds and initiative, Urgency, combatants and their AI (rulebook p. 18, 36, 56-61).</summary>
internal sealed partial class Session
{
    private const string AdventurerCard = "adventurer:";

    /// <summary>Golden Rule #4: a combatant with an Aggro token moves towards the nearest opponent.</summary>
    private static readonly AiStep AggroStep = new()
    {
        Condition = new AiCondition(Text("Otherwise (Aggro)", "Sinon (Aggro)"), "always"),
        Instructions =
        [
            new AiInstruction
            {
                Text = Text(
                    "Move towards the nearest opponent. This figure may Jump up to 4 spaces during this movement.",
                    "Se déplace vers l'adversaire le plus proche. Cette figurine peut sauter jusqu'à 4 cases pendant ce déplacement."),
                Action = "move",
            },
        ],
    };

    private void EnterEncounter(Node node)
    {
        State.Encounter = new EncounterState
        {
            Node = node.Id,
            ActiveEndConditions = node.EndConditions.Where(e => e.Active != false).Select(e => e.Id).ToList(),
            InitiativeTrack = ActiveParty.Select(a => AdventurerCard + a.Name).ToList(),
        };
        PrepareAdventurers();
        Log("encounter", node.Intro, Args(("node", node.Id), ("name", node.Name?.GetValueOrDefault("en")), ("lootLevel", node.LootLevel), ("locationType", node.LocationType)));
        foreach (var card in node.Setup?.RevealHiddenCards ?? [])
        {
            RevealHiddenCard(card.Size, card.Code);
        }

        foreach (var rule in node.SpecialRules)
        {
            Log("specialRule", rule.Text, Args(("rule", rule.Id), ("name", rule.Name.GetValueOrDefault("en"))));
        }

        AddTokens(node.Map);
        RevealAtStart(node.Map);
        BuildLootDeck(node);
        PlaceAdventurers(node.Map, node.Setup);
        BringKeptAllies();
        foreach (var placement in node.Setup?.Combatants ?? [])
        {
            Spawn(SpawnOf(placement), fromSetup: true, placement.InitiativeCard);
        }

        Fire(e => e.Trigger.On == "encounterStart");
    }

    private Result NextTurn()
    {
        var encounter = Encounter;
        // The card nearest the front that has not played yet: a turn is never skipped nor played twice (p. 36)
        var next = encounter.Round == 0 ? -1 : encounter.InitiativeTrack.FindIndex(c => !encounter.PlayedCards.Contains(c));
        if (next < 0)
        {
            if (encounter.Round > 0)
            {
                EndRound();
            }

            if (!encounter.Ended)
            {
                StartRound();
            }

            return Result.Success();
        }

        encounter.CurrentTurn = next;
        BeginTurn();
        return Result.Success();
    }

    /// <summary>
    /// An item or an ability moves a card forward (toward the front, <paramref name="by"/> negative) or backward
    /// (p. 36). The card playing goes on; a card moved in front of it that has not played plays next.
    /// </summary>
    private Result MoveInitiative(string? card, int by)
    {
        var encounter = Encounter;
        var track = encounter.InitiativeTrack;
        var from = card is null ? -1 : track.IndexOf(card);
        if (from < 0)
        {
            return GameEngine.Errors.UnknownCard(card);
        }

        var to = Math.Clamp(from + by, 0, track.Count - 1);
        if (to == from)
        {
            return Result.Success();
        }

        var current = encounter.CurrentTurn >= 0 && encounter.CurrentTurn < track.Count ? track[encounter.CurrentTurn] : null;
        track.RemoveAt(from);
        track.Insert(to, card!);
        if (current is not null)
        {
            encounter.CurrentTurn = track.IndexOf(current);
        }

        Log("initiativeMoved", args: Args(("card", card), ("from", Number(from + 1)), ("to", Number(to + 1))));
        return Result.Success();
    }

    private void StartRound()
    {
        var encounter = Encounter;
        encounter.Round++;
        encounter.RoundLog = new RoundLog();
        if (encounter.Round == 1 || !Options.StaticMonsters)
        {
            ShuffleTrack();
        }

        encounter.PlayedCards.Clear();
        encounter.CurrentTurn = 0;
        Log("roundStart", args: Args(("round", Number(encounter.Round)), ("track", string.Join(", ", encounter.InitiativeTrack))));
        Fire(e => e.Trigger.On == "roundStart" && (e.Trigger.Round is null || e.Trigger.Round == encounter.Round));
        BeginTurn();
    }

    private void ShuffleTrack()
    {
        var track = Encounter.InitiativeTrack;
        for (var i = track.Count - 1; i > 0; i--)
        {
            var j = NextRandom(i + 1);
            (track[i], track[j]) = (track[j], track[i]);
        }
    }

    /// <summary>Urgency (rulebook p. 18): a round with no true AI step, no opponent defeated and none spawned adds a token.</summary>
    private void EndRound()
    {
        var encounter = Encounter;
        Log("roundEnd", args: Args(("round", Number(encounter.Round))));
        Fire(e => e.Trigger.On == "roundEnd" && (e.Trigger.Round is null || e.Trigger.Round == encounter.Round));
        var log = encounter.RoundLog;
        if (!log.AiStepTrue && log.Defeated == 0 && log.Spawned == 0)
        {
            AddUrgency(1);
        }
    }

    private void AddUrgency(int amount)
    {
        if (Options.RemoveUrgency)
        {
            return;
        }

        var encounter = Encounter;
        encounter.Urgency += amount;
        Log("urgency", args: Args(("urgency", Number(encounter.Urgency))));
        if (encounter.Urgency >= ((Options.NoPlayerElimination ? Options.UrgencyLimit : null) ?? GameOptions.DefaultUrgencyLimit))
        {
            // The last Urgency token (the 4th, as printed) defeats every Adventurer.
            var node = EncounterNode;
            var lose = node.EndConditions.FirstOrDefault(e => e.Kind == "lose" && encounter.ActiveEndConditions.Contains(e.Id));
            Log("urgencyDefeat");
            DefeatEveryAdventurer();
            if (lose is not null)
            {
                EndEncounter(lose.Id);
            }
        }
    }

    private void BeginTurn()
    {
        var encounter = Encounter;
        if (encounter.CurrentTurn >= encounter.InitiativeTrack.Count)
        {
            return;
        }

        var card = encounter.InitiativeTrack[encounter.CurrentTurn];
        if (!encounter.PlayedCards.Contains(card))
        {
            encounter.PlayedCards.Add(card);
        }

        var adventurer = card.StartsWith(AdventurerCard, StringComparison.Ordinal) ? card[AdventurerCard.Length..] : null;
        if (adventurer is not null)
        {
            ReturnToPlay(adventurer);
            RefreshStamina(adventurer);
        }

        // Every ally of the card takes its turn, in the order the players choose (p. 58)
        var allies = IsAlly(card) ? LivingAllies().Where(a => a.TrackCard == card).ToList() : [];
        allies.ForEach(BeginAllyTurn);
        var carried = allies.Count > 0
            ? allies.SelectMany(a => a.Effects).Distinct()
            : Encounter.Adventurers.FirstOrDefault(a => a.Name == adventurer)?.Effects ?? [];
        var statusEffects = carried.Where(IsStatusEffect).ToList();
        Log("turn", args: Args(("card", card), ("adventurer", adventurer), ("ally", allies.Count > 0 ? string.Join(", ", allies.Select(NameOf)) : null),
            ("statusEffects", statusEffects.Count > 0 ? string.Join(", ", statusEffects) : null)));
        var figures = Alive().Where(c => c.InitiativeCards.Contains(card)).OrderBy(c => c.Number).Select(c => c.Instance).ToList();
        if (figures.Count > 0)
        {
            encounter.Activation = new Activation { InitiativeCard = card, Figures = figures };
            StartFigure(figures[0]);
        }
    }

    private void StartFigure(string instance)
    {
        var figure = Find(instance)!;
        var statusEffects = figure.Effects.Where(IsStatusEffect).ToList();
        Log("figureTurn", args: Args(("instance", figure.Instance), ("card", figure.Card), ("number", Number(figure.Number)),
            ("statusEffects", statusEffects.Count > 0 ? string.Join(", ", statusEffects) : null)));
    }

    /// <summary>The acting figure goes through its AI steps, top to bottom: the GM answers each condition.</summary>
    private void ContinueActivation()
    {
        var activation = Encounter.Activation!;
        while (activation.Figures.Count > 0 && Find(activation.Figures[0]) is not { Defeated: false })
        {
            NextFigure(activation);
        }

        if (activation.Figures.Count == 0)
        {
            Encounter.Activation = null;
            return;
        }

        var figure = Find(activation.Figures[0])!;
        var steps = StepsOf(figure);
        if (activation.Step >= steps.Count)
        {
            if (!activation.AnyStepTrue && figure.Attacked && !figure.Aggro)
            {
                figure.Aggro = true;
                Log("aggro", args: Args(("instance", figure.Instance)));
            }

            figure.Attacked = false;
            Log("figureTurnEnd", args: Args(("instance", figure.Instance)));
            NextFigure(activation);
            if (activation.Figures.Count > 0 && Find(activation.Figures[0]) is { Defeated: false } next)
            {
                StartFigure(next.Instance);
            }

            return;
        }

        var step = steps[activation.Step];
        if (step.Condition.Kind == "always")
        {
            AnswerAiCondition(true);
        }
        else
        {
            Ask(PromptKinds.AiCondition, step.Condition.Text, [new PromptOption("yes"), new PromptOption("no")], instance: figure.Instance);
        }
    }

    private void AnswerAiCondition(bool isTrue)
    {
        var activation = Encounter.Activation!;
        var figure = Find(activation.Figures[0])!;
        var step = StepsOf(figure)[activation.Step];
        if (activation.AskingContinue)
        {
            activation.AskingContinue = false;
            activation.Step = isTrue ? activation.Step + 1 : int.MaxValue;
            return;
        }

        if (!isTrue)
        {
            activation.Step++;
            return;
        }

        activation.AnyStepTrue = true;
        Encounter.RoundLog.AiStepTrue = true;
        foreach (var instruction in step.Instructions)
        {
            Log("aiInstruction", instruction.Text, Args(("instance", figure.Instance), ("target", instruction.Target?.Priority)));
        }

        if (step.ContinueDownIf is { } question)
        {
            activation.AskingContinue = true;
            Ask(PromptKinds.AiCondition, question, [new PromptOption("yes"), new PromptOption("no")], instance: figure.Instance);
            return;
        }

        activation.Step = step.ContinueDown ? activation.Step + 1 : int.MaxValue;
    }

    private static void NextFigure(Activation activation)
    {
        activation.Figures.RemoveAt(0);
        activation.Step = 0;
        activation.AnyStepTrue = false;
        activation.AskingContinue = false;
    }

    private List<AiStep> StepsOf(CombatantState figure)
    {
        var steps = new List<AiStep>(Catalog.FindCombatant(figure.Card)?.AiSteps ?? []);
        steps.AddRange(Encounter.ExtraAiSteps.GetValueOrDefault(figure.Family) ?? []);
        if (figure.Aggro)
        {
            steps.Add(AggroStep);
        }

        return steps;
    }

    private static ScenarioAction SpawnOf(CombatantPlacement placement) => new()
    {
        Do = "spawn",
        Family = placement.Family,
        Ref = placement.Ref,
        Instance = placement.Instance,
        Figure = placement.Figure,
        Count = 1,
        LootLevel = placement.LootLevel,
        Effects = placement.Effects,
        At = placement.At is { } at ? System.Text.Json.JsonSerializer.SerializeToElement(at, JsonDefaults.Options) : null,
    };

    /// <summary>
    /// Rulebook p. 61: cards matching the encounter's loot level, lowest free activation number first;
    /// a new initiative card goes right after the card taking its turn.
    /// </summary>
    /// <param name="possibleSpawns">
    /// Alternate and Variation cards may Spawn instead when a new card is required (rulebook p. 80): never during
    /// the encounter's Placement Phase, always otherwise (totems, diagrams, NAVIGATE).
    /// </param>
    private void Spawn(ScenarioAction action, bool fromSetup, string? initiativeCard = null, bool? possibleSpawns = null)
    {
        var lootLevel = action.LootLevel ?? EncounterNode.LootLevel;
        var count = action.Count ?? 1;
        if (HiddenAreaOf(action.At) is { } area)
        {
            // Under the fog: not in play until revealed (neither figure, nor number, nor card on the track)
            for (var i = 0; i < count; i++)
            {
                Encounter.Waiting.Add(new WaitingCombatant
                {
                    Area = area,
                    // Only the first one stands on the square named, as for figures spawned in view
                    Action = action with { Count = 1, At = i == 0 ? action.At : null },
                    InitiativeCard = initiativeCard,
                    PossibleSpawns = possibleSpawns ?? !fromSetup,
                });
            }

            return;
        }

        if (action.Ref is { } named && Catalog.FindCombatant(named) is { } command && AllyRules.IsCommand(command))
        {
            SpawnAlly(action, command, fromSetup, initiativeCard);
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var figure = action.Ref is not null ? NamedFigure(action) : FamilyFigure(action.Family!, lootLevel, possibleSpawns ?? !fromSetup);
            if (figure is null)
            {
                Log("spawnImpossible", args: Args(("family", action.Family), ("ref", action.Ref)));
                return;
            }

            // The setup may give another initiative card (e.g. a story combatant played with another figure).
            if (initiativeCard is not null)
            {
                figure.InitiativeCards = [initiativeCard];
            }

            figure.Figure = action.Figure ?? figure.Figure;
            figure.Health = HealthInThisGame(figure.Card);
            figure.Effects.AddRange((action.Effects ?? []).Except(figure.Effects));
            figure.Position = i == 0 ? Locate(action.At) : null;
            var existing = Encounter.Combatants.FindIndex(c => c.Instance == figure.Instance);
            if (existing >= 0)
            {
                Encounter.Combatants[existing] = figure;
            }
            else
            {
                Encounter.Combatants.Add(figure);
            }

            AddToTrack(figure.InitiativeCards);
            if (!fromSetup)
            {
                Encounter.RoundLog.Spawned++;
            }

            Log(fromSetup ? "place" : "spawn", args: Args(("instance", figure.Instance), ("card", figure.Card), ("number", Number(figure.Number)),
                ("figure", figure.Figure), ("at", Json(action.At))));
        }
    }

    private CombatantState? NamedFigure(ScenarioAction action)
    {
        var card = Catalog.FindCombatant(action.Ref!);
        if (card is null)
        {
            return null;
        }

        return new CombatantState
        {
            Instance = action.Instance ?? $"{card.Family}-{Encounter.Combatants.Count + 1}",
            Card = card.Id,
            Family = card.Family,
            InitiativeCards = [.. card.InitiativeCards],
        };
    }

    /// <summary>
    /// Rulebook p. 61 and 80-81: complete a card already in use first (lowest free activation number); when a new
    /// card is required, draw one at random among the Possible Spawns: the Base cards whose group is free, their
    /// Variations (same group), and the Alternates of this family not on the board, prerequisites met.
    /// </summary>
    private CombatantState? FamilyFigure(string family, string? lootLevel, bool possibleSpawns)
    {
        possibleSpawns &= !Options.LessVariety;
        var chosen = Options.NoScaling ? Catalog.LowestCardFor(family) : Catalog.CardFor(family, lootLevel);
        if (chosen is null)
        {
            return null;
        }

        var bases = Catalog.IntelligentCards(family).Where(c => c.LootLevel == chosen.LootLevel).ToList();
        var variations = possibleSpawns ? Replacements(family, chosen.LootLevel).Where(c => c.SpawnRule!.VariationOf is not null).ToList() : [];
        var sameFigures = bases.Concat(variations).Select(c => c.Id).ToHashSet();
        var taken = Alive().Where(c => sameFigures.Contains(c.Card) || c.Family == family).Select(c => c.Number).ToHashSet();

        // A card in use with a free number: no new card required.
        var inUse = Alive().Where(c => sameFigures.Contains(c.Card)).Select(c => c.Card).Distinct()
            .Select(id => Catalog.FindCombatant(id)!)
            .SelectMany(c => ActivationNumbers(GroupOf(c)).Where(n => !taken.Contains(n)).Select(n => (Number: n, Card: c)))
            .OrderBy(x => x.Number)
            .FirstOrDefault();
        if (inUse.Card is not null)
        {
            return Figure(inUse.Card, inUse.Number);
        }

        // A new card: the next free Base group, and when allowed its Variations and the Alternates not on the board.
        var next = bases.Where(c => ActivationNumbers(c.Activation).Any() && ActivationNumbers(c.Activation).All(n => !taken.Contains(n)))
            .OrderBy(c => ActivationNumbers(c.Activation).First())
            .FirstOrDefault();
        var candidates = next is null ? new List<Combatant>() : [next];
        if (possibleSpawns)
        {
            candidates.AddRange(variations.Where(c => next is not null && GroupOf(c) == next.Activation));
            candidates.AddRange(Replacements(family, chosen.LootLevel)
                .Where(c => c.SpawnRule!.VariationOf is null && Alive().All(a => a.Family != c.Family)));
        }

        if (candidates.Count == 0)
        {
            // Cards without activation numbers (a single figure): the next number, as before.
            if (bases.All(c => !ActivationNumbers(c.Activation).Any()))
            {
                return Figure(chosen, taken.Count == 0 ? 1 : taken.Max() + 1);
            }

            return null;
        }

        var card = candidates.Count == 1 ? candidates[0] : candidates[NextRandom(candidates.Count)];
        if (card.SpawnRule is { VariationOf: null })
        {
            var own = Alive().Where(c => c.Family == card.Family).Select(c => c.Number).ToHashSet();
            var first = ActivationNumbers(card.Activation).FirstOrDefault(n => !own.Contains(n));
            Log("possibleSpawn", args: Args(("family", family), ("card", card.Id)));
            return Figure(card, first == 0 ? 1 : first);
        }

        if (card.SpawnRule is not null)
        {
            Log("possibleSpawn", args: Args(("family", family), ("card", card.Id)));
        }

        return Figure(card, ActivationNumbers(GroupOf(card)).First(n => !taken.Contains(n)));
    }

    private CombatantState Figure(Combatant card, int number) => new()
    {
        Instance = $"{card.Family}-{number}",
        Card = card.Id,
        Family = card.Family,
        Number = number,
        InitiativeCards = [.. InitiativeOf(card)],
    };

    /// <summary>Special Encounter Rules about a Base Combatant also mean the Possible Spawns that replace it (p. 82).</summary>
    private string? BaseFamilyOf(CombatantState figure) => Catalog.FindCombatant(figure.Card)?.SpawnRule?.BaseFamily;

    /// <summary>A Variation shares the group of its Base card ("cave-sickle-1-3" → "1-3").</summary>
    private static string? GroupOf(Combatant card) =>
        card.SpawnRule?.VariationOf is { } of ? Regex.Match(of, @"\d+-\d+$").Value : card.Activation;

    /// <summary>A Variation without its own initiative card takes its Base card's.</summary>
    private IReadOnlyList<string> InitiativeOf(Combatant card)
    {
        if (card.InitiativeCards.Count > 0 || card.SpawnRule?.VariationOf is null)
        {
            return card.InitiativeCards;
        }

        var group = GroupOf(card);
        return Catalog.IntelligentCards(card.SpawnRule.BaseFamily)
            .FirstOrDefault(c => c.Activation == group && c.LootLevel == card.LootLevel)?.InitiativeCards ?? [];
    }

    /// <summary>The Alternate and Variation cards that may replace this family at this Loot LVL, prerequisites met.</summary>
    private IEnumerable<Combatant> Replacements(string family, string? lootLevel) =>
        Catalog.Combatants.Where(c => c.Kind == "intelligent" && c.SpawnRule?.BaseFamily == family && c.Family != family
                                      && (c.LootLevel == lootLevel || c.LootLevel == LootLevels.Any)
                                      && c.SpawnRule.Requires.All(PrerequisiteMet(c)));

    private Func<SpawnPrerequisite, bool> PrerequisiteMet(Combatant card) => prerequisite =>
        (prerequisite.Attempted is not { } scenario || State.Campaign.AttemptedScenarios.Contains(scenario))
        && (prerequisite.OncePerEncounter != true || Encounter.Combatants.All(c => c.Family != card.Family));

    /// <summary>Activation numbers printed on the card: "1-3" → 1, 2, 3; "1 + Root ×4" → 1; an adventurer's name → none.</summary>
    internal static IEnumerable<int> ActivationNumbers(string? activation)
    {
        var match = Regex.Match(activation ?? "", @"^\s*(\d+)(?:\s*-\s*(\d+))?");
        if (!match.Success)
        {
            return [];
        }

        var first = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var last = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : first;
        return Enumerable.Range(first, last - first + 1);
    }

    private void AddToTrack(IEnumerable<string> cards)
    {
        var encounter = Encounter;
        foreach (var card in cards.Where(c => !encounter.InitiativeTrack.Contains(c)))
        {
            if (encounter.Round > 0 && encounter.CurrentTurn >= 0 && !Options.SlowMonsters)
            {
                encounter.InitiativeTrack.Insert(encounter.CurrentTurn + 1, card);
            }
            else
            {
                encounter.InitiativeTrack.Add(card);
            }
        }
    }

    /// <summary>A card leaves the track when no figure on the board uses it any more.</summary>
    private void RemoveUnusedCards()
    {
        var encounter = Encounter;
        var used = Alive().SelectMany(c => c.InitiativeCards).ToHashSet();
        for (var i = encounter.InitiativeTrack.Count - 1; i >= 0; i--)
        {
            var card = encounter.InitiativeTrack[i];
            if (!card.StartsWith(AdventurerCard, StringComparison.Ordinal) && !IsAlly(card) && !used.Contains(card))
            {
                encounter.InitiativeTrack.RemoveAt(i);
                if (i <= encounter.CurrentTurn)
                {
                    encounter.CurrentTurn--;
                }

                Log("initiativeRemoved", args: Args(("card", card)));
            }
        }
    }

    private void ReplaceInitiativeCard(string from, string to)
    {
        var track = Encounter.InitiativeTrack;
        var index = track.IndexOf(from);
        if (index >= 0)
        {
            track[index] = to;
        }

        foreach (var figure in Encounter.Combatants)
        {
            figure.InitiativeCards = figure.InitiativeCards.Select(c => c == from ? to : c).ToList();
        }

        Log("replaceInitiativeCard", args: Args(("from", from), ("to", to)));
    }

    private Result ApplyDamage(string? instance, int amount)
    {
        if (Find(instance) is not { Defeated: false } figure)
        {
            return GameEngine.Errors.UnknownCombatant(instance);
        }

        figure.Damage = Math.Max(0, figure.Damage + amount);
        figure.Attacked |= amount > 0;
        var health = figure.Health ?? Catalog.FindCombatant(figure.Card)?.Stats?.Health?.Value;
        Log("damage", args: Args(("instance", figure.Instance), ("amount", Number(amount)), ("damage", Number(figure.Damage)), ("health", Number(health))));
        if (health is { } max && figure.Damage >= max)
        {
            DefeatFigure(figure);
        }

        return Result.Success();
    }

    /// <summary>On a combatant, or an adventurer ("adventurer:Rook"); a figure has at most one of each Effect.</summary>
    private Result SetEffect(string? instance, string? effect, bool active)
    {
        var effects = instance?.StartsWith(AdventurerCard, StringComparison.Ordinal) == true
            ? Encounter.Adventurers.FirstOrDefault(a => a.Name == instance[AdventurerCard.Length..])?.Effects
            : IsAlly(instance) ? FindAlly(instance)?.Effects
            : Find(instance) is { Defeated: false } figure ? figure.Effects : null;
        if (effects is null)
        {
            return GameEngine.Errors.UnknownCombatant(instance);
        }

        if (string.IsNullOrWhiteSpace(effect))
        {
            return GameEngine.Errors.InvalidAnswer(effect);
        }

        effects.Remove(effect);
        if (active)
        {
            effects.Add(effect);
        }

        Log(active ? "effectAdded" : "effectRemoved", args: Args(("instance", instance), ("effect", effect)));
        return Result.Success();
    }

    private Result Defeat(string? instance)
    {
        if (Find(instance) is not { Defeated: false } figure)
        {
            return GameEngine.Errors.UnknownCombatant(instance);
        }

        DefeatFigure(figure);
        return Result.Success();
    }

    private void DefeatFigure(CombatantState figure)
    {
        figure.Defeated = true;
        figure.Effects.Clear();
        figure.Aggro = false;
        Encounter.RoundLog.Defeated++;
        Log("defeated", args: Args(("instance", figure.Instance), ("card", figure.Card)));
        DropLoot(figure);
        if (State.Prompt?.Instance == figure.Instance && State.Prompt.Kind == PromptKinds.AiCondition)
        {
            State.Prompt = null;
        }

        RemoveUnusedCards();
        Fire(e => e.Trigger.On == "combatantDefeated"
                  && (e.Trigger.Instance is null || e.Trigger.Instance == figure.Instance)
                  && (e.Trigger.Family is null || e.Trigger.Family == figure.Family || e.Trigger.Family == BaseFamilyOf(figure)));
    }

    private void RestoreCombatant(ScenarioAction action, int damage)
    {
        if (Find(action.Instance) is not { } figure)
        {
            Log("unknownCombatant", args: Args(("instance", action.Instance)));
            return;
        }

        figure.Defeated = false;
        figure.Damage = damage;
        AddToTrack(figure.InitiativeCards);
        Log("restoreCombatant", args: Args(("instance", figure.Instance), ("damage", Number(damage)), ("at", Json(action.At))));
    }

    private Result ActivateTokenCommand(string? key)
    {
        var known = key is not null && (EncounterNode.Tokens.ContainsKey(key) || Encounter.Tokens.Any(t => t.Key == key && !t.Removed));
        if (!known)
        {
            return GameEngine.Errors.UnknownToken(key);
        }

        ActivateToken(key!);
        return Result.Success();
    }

    private Result ReachEndCondition(string? id)
    {
        if (id is null || !Encounter.ActiveEndConditions.Contains(id))
        {
            return GameEngine.Errors.InactiveEndCondition(id);
        }

        EndEncounter(id);
        return Result.Success();
    }

    /// <summary>The encounter stops at once: what was pending is dropped, the reward is applied, then the next node.</summary>
    private void EndEncounter(string id)
    {
        var condition = EncounterNode.EndConditions.FirstOrDefault(e => e.Id == id);
        if (condition is null)
        {
            Log("unknownEndCondition", args: Args(("endCondition", id)));
            return;
        }

        Encounter.Ended = true;
        Encounter.Activation = null;
        State.Prompt = null;
        State.PendingActions.Clear();
        Log("encounterEnd", condition.Text, Args(("endCondition", condition.Id), ("kind", condition.Kind)));
        if (State.Bounty is { } bounty)
        {
            bounty.LastEnd = condition.Kind;
        }

        BankLootGold();
        if (condition.Reward is not null)
        {
            Log("reward", condition.Reward);
        }

        Push([.. condition.Mechanics, Continue(), GoTo(condition.Next)]);
    }

    /// <summary>Tougher Mobs: 50% more HP to every non-BOSS opponent (rounded up).</summary>
    private int? HealthInThisGame(string card) =>
        Options.TougherMobs && Catalog.FindCombatant(card) is { IsBoss: false, Kind: "intelligent" } combatant && combatant.Stats?.Health?.Value is { } health
            ? health + ((health + 1) / 2)
            : null;

    private IEnumerable<CombatantState> Alive() => Encounter.Combatants.Where(c => !c.Defeated);

    private CombatantState? Find(string? instance) => Encounter.Combatants.FirstOrDefault(c => c.Instance == instance);

    private bool IsStatusEffect(string effect) =>
        Catalog.Packs.Any(p => p.Rules?.Effects.Any(e => e.Id == effect && e.Condition == "status") == true);
}
