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
            InitiativeTrack = State.Campaign.Party.Select(a => AdventurerCard + a.Name).ToList(),
        };
        Log("encounter", node.Intro, Args(("node", node.Id), ("name", node.Name?.GetValueOrDefault("en")), ("lootLevel", node.LootLevel), ("locationType", node.LocationType)));
        foreach (var card in node.Setup?.RevealHiddenCards ?? [])
        {
            State.Campaign.RevealedHiddenCards.Add($"{card.Size}:{card.Code}");
            Log("revealHiddenCard", args: Args(("size", card.Size), ("code", card.Code)));
        }

        foreach (var rule in node.SpecialRules)
        {
            Log("specialRule", rule.Text, Args(("rule", rule.Id), ("name", rule.Name.GetValueOrDefault("en"))));
        }

        AddTokens(node.Map);
        RevealAtStart(node.Map);
        BuildLootDeck(node);
        PlaceAdventurers(node.Map, node.Setup);
        foreach (var placement in node.Setup?.Combatants ?? [])
        {
            Spawn(SpawnOf(placement), fromSetup: true, placement.InitiativeCard);
        }

        Fire(e => e.Trigger.On == "encounterStart");
    }

    private Result NextTurn()
    {
        var encounter = Encounter;
        if (encounter.Round == 0 || encounter.CurrentTurn + 1 >= encounter.InitiativeTrack.Count)
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

        encounter.CurrentTurn++;
        BeginTurn();
        return Result.Success();
    }

    private void StartRound()
    {
        var encounter = Encounter;
        encounter.Round++;
        encounter.RoundLog = new RoundLog();
        ShuffleTrack();
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
        var encounter = Encounter;
        encounter.Urgency += amount;
        Log("urgency", args: Args(("urgency", Number(encounter.Urgency))));
        if (encounter.Urgency >= 4)
        {
            // A 4th Urgency token defeats every Adventurer.
            var node = EncounterNode;
            var lose = node.EndConditions.FirstOrDefault(e => e.Kind == "lose" && encounter.ActiveEndConditions.Contains(e.Id));
            Log("urgencyDefeat");
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
        var adventurer = card.StartsWith(AdventurerCard, StringComparison.Ordinal) ? card[AdventurerCard.Length..] : null;
        var statusEffects = Encounter.Adventurers.FirstOrDefault(a => a.Name == adventurer)?.Effects.Where(IsStatusEffect).ToList() ?? [];
        Log("turn", args: Args(("card", card), ("adventurer", adventurer),
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
    private void Spawn(ScenarioAction action, bool fromSetup, string? initiativeCard = null)
    {
        var lootLevel = action.LootLevel ?? EncounterNode.LootLevel;
        for (var i = 0; i < (action.Count ?? 1); i++)
        {
            var figure = action.Ref is not null ? NamedFigure(action) : FamilyFigure(action.Family!, lootLevel);
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

    private CombatantState? FamilyFigure(string family, string? lootLevel)
    {
        var chosen = Catalog.CardFor(family, lootLevel);
        if (chosen is null)
        {
            return null;
        }

        var cards = Catalog.IntelligentCards(family).Where(c => c.LootLevel == chosen.LootLevel).ToList();
        var taken = Alive().Where(c => c.Family == family).Select(c => c.Number).ToHashSet();
        var numbered = cards.SelectMany(c => ActivationNumbers(c.Activation).Select(n => (Number: n, Card: c))).OrderBy(x => x.Number).ToList();
        var (number, card) = numbered.Count == 0
            ? (taken.Count == 0 ? 1 : taken.Max() + 1, chosen)
            : numbered.FirstOrDefault(x => !taken.Contains(x.Number));
        if (card is null)
        {
            return null;
        }

        return new CombatantState
        {
            Instance = $"{family}-{number}",
            Card = card.Id,
            Family = family,
            Number = number,
            InitiativeCards = [.. card.InitiativeCards],
        };
    }

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
            if (encounter.Round > 0 && encounter.CurrentTurn >= 0)
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
            if (!card.StartsWith(AdventurerCard, StringComparison.Ordinal) && !used.Contains(card))
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
        var health = Catalog.FindCombatant(figure.Card)?.Stats?.Health?.Value;
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
                  && (e.Trigger.Family is null || e.Trigger.Family == figure.Family));
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
        BankLootGold();
        if (condition.Reward is not null)
        {
            Log("reward", condition.Reward);
        }

        Push([.. condition.Mechanics, Continue(), GoTo(condition.Next)]);
    }

    private IEnumerable<CombatantState> Alive() => Encounter.Combatants.Where(c => !c.Defeated);

    private CombatantState? Find(string? instance) => Encounter.Combatants.FirstOrDefault(c => c.Instance == instance);

    private bool IsStatusEffect(string effect) =>
        Catalog.Packs.Any(p => p.Rules?.Effects.Any(e => e.Id == effect && e.Condition == "status") == true);
}
