using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Figures that are more than their card (play-spec A12, A13, A15, A21): written by the scenario, of another side,
/// changed by a rule, or scaled on their summoner (opposing Espers, p. 72 and 76).
/// </summary>
internal sealed partial class Session
{
    private const string FigurePrefix = "figure:";
    private const string PlayersSide = "players";
    private const string EnemiesSide = "enemies";

    /// <summary>Root tokens "count as figures with 6 HP, 10 Defense, and 6 Armor" and drop no loot (p. 97).</summary>
    private static readonly SceneFigure Root = new()
    {
        Id = "root",
        Name = Text("Root", "Racine"),
        Stats = new FigureStats { Health = 6, Defense = 10, Armor = 6 },
        Tags = ["RESISTANCE: PHYSICAL DMG (except AXE)", "RESISTANCE: MAGIC DMG (except AXE)", "IMMUNITY: SPELLS, EFFECTS"],
    };

    private static bool IsOpponent(CombatantState figure) => figure.Side != PlayersSide;

    private static string SideOf(CombatantState figure) => figure.Side ?? EnemiesSide;

    private string UniqueInstance(string name)
    {
        var instance = name;
        for (var copy = 2; Alive().Any(c => c.Instance == instance); copy++)
        {
            instance = $"{name}-{copy}";
        }

        return instance;
    }

    private CombatantState? SceneFigureOf(ScenarioAction action)
    {
        var id = action.Ref![FigurePrefix.Length..];
        var described = EncounterNode.Figures.FirstOrDefault(f => f.Id == id) ?? (id == Root.Id ? Root : null);
        if (described is null)
        {
            return null;
        }

        return new CombatantState
        {
            Instance = action.Instance ?? UniqueInstance(id),
            Card = action.Ref,
            Family = id,
            Name = described.Name?.ToDictionary(n => n.Key, n => n.Value),
            Figure = described.Figure,
            Health = described.Stats?.Health,
            Stats = described.Stats is { } stats ? stats with { Health = null } : null,
            Side = described.Side,
            InitiativeCards = described.InitiativeCard is { } card ? [card] : [],
            AddedTags = [.. described.Tags],
            NoLoot = described.Loot != true,
            Untargetable = described.Attackable == false,
        };
    }

    private IReadOnlyList<string> TagsOf(CombatantState figure) =>
        (Catalog.FindCombatant(figure.Card)?.Tags ?? []).Concat(figure.AddedTags).Except(figure.RemovedTags).ToList();

    private int? MaxHealthOf(CombatantState figure) => figure.Health ?? Catalog.FindCombatant(figure.Card)?.Stats?.Health?.Value;

    /// <summary>The "*" of an opposing Esper are its summoner's, at their maximum; its Force is the SUMMONER X.</summary>
    private void ScaleOnSummoner(CombatantState figure, string summonerId, bool fromSetup)
    {
        figure.Summoner = summonerId;
        if (!fromSetup)
        {
            figure.SpawnedBy ??= summonerId;
        }

        if (Find(summonerId) is not { } summoner)
        {
            Log("summonerMissing", args: Args(("instance", figure.Instance), ("summoner", summonerId)));
            return;
        }

        var card = Catalog.FindCombatant(figure.Card)?.Stats;
        var theirs = Catalog.FindCombatant(summoner.Card)?.Stats;
        if (card?.Health is { IsVariable: true })
        {
            figure.Health = MaxHealthOf(summoner);
        }

        static bool Scaled(IReadOnlyList<string>? dice) => dice?.Any(d => d.Contains('*')) == true;

        var stats = figure.Stats ?? new FigureStats();
        figure.Stats = stats with
        {
            Defense = card?.Defense is { IsVariable: true } ? summoner.Stats?.Defense ?? theirs?.Defense?.Value : stats.Defense,
            Armor = card?.Armor is { IsVariable: true } ? summoner.Stats?.Armor ?? theirs?.Armor?.Value : stats.Armor,
            Movement = card?.Movement is { IsVariable: true } ? summoner.Stats?.Movement ?? theirs?.Movement?.Value : stats.Movement,
            CombatDice = Scaled(card?.CombatDice) ? summoner.Stats?.CombatDice ?? theirs?.CombatDice : stats.CombatDice,
            ConvictionDice = Scaled(card?.ConvictionDice) ? summoner.Stats?.ConvictionDice ?? theirs?.ConvictionDice : stats.ConvictionDice,
            CastingDie = Scaled(card?.CastingDie) ? summoner.Stats?.CastingDie ?? theirs?.CastingDie : stats.CastingDie,
        };
        figure.Force = TagsOf(summoner).Select(t => Regex.Match(t, @"^SUMMONER\s+(\d+)$")).Where(m => m.Success)
            .Select(m => (int?)int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).FirstOrDefault();
    }

    /// <summary>The GM reports what the summoner's AI says.</summary>
    private Result SummonEsper(string? card, string? summoner, Cell? at)
    {
        var offered = EncounterNode.EncounterEspers.Any(e => e.Ref == card && (e.Summoner is null || e.Summoner == summoner));
        if (!offered || Find(summoner) is not { Defeated: false })
        {
            return MechanicsErrors.NotSummonable(card);
        }

        var action = new ScenarioAction { Do = "spawn", Ref = card, At = at is { } cell ? JsonSerializer.SerializeToElement(cell, JsonDefaults.Options) : null };
        Spawn(With(action, "summoner", summoner), fromSetup: false);
        return Result.Success();
    }

    private void ModifyFigures(ScenarioAction action)
    {
        if (action.Instance is { } instance)
        {
            if (Find(instance) is { Defeated: false } named)
            {
                Modify(named, action);
            }
            else
            {
                Log("unknownCombatant", args: Args(("instance", instance)));
            }

            return;
        }

        if (action.Family is not { } family)
        {
            Log("manualAction", action.Text, Args(("do", action.Do)));
            return;
        }

        // A rule about a family holds for those still to come
        Encounter.Modifiers.Add(action);
        foreach (var figure in Alive().Where(f => IsOfFamily(f, family)).ToList())
        {
            Modify(figure, action);
        }
    }

    private bool IsOfFamily(CombatantState figure, string family) => figure.Family == family || BaseFamilyOf(figure) == family;

    private void Modify(CombatantState figure, ScenarioAction action)
    {
        var printed = Catalog.FindCombatant(figure.Card)?.Stats;
        var now = figure.Stats ?? new FigureStats();
        var set = ExtraObject<FigureStats>(action, "stats") ?? new FigureStats();
        var bonus = ExtraObject<FigureStats>(action, "bonus") ?? new FigureStats();

        static int? Value(int? current, int? printed, int? given, int? added)
        {
            var value = given ?? current;
            return added is { } more ? Math.Max(0, (value ?? printed ?? 0) + more) : value;
        }

        figure.Health = Value(figure.Health, printed?.Health?.Value, set.Health, bonus.Health);
        var changed = new FigureStats
        {
            Defense = Value(now.Defense, printed?.Defense?.Value, set.Defense, bonus.Defense),
            Armor = Value(now.Armor, printed?.Armor?.Value, set.Armor, bonus.Armor),
            Movement = Value(now.Movement, printed?.Movement?.Value, set.Movement, bonus.Movement),
            CombatDice = set.CombatDice ?? now.CombatDice,
            ConvictionDice = set.ConvictionDice ?? now.ConvictionDice,
            CastingDie = set.CastingDie ?? now.CastingDie,
        };
        figure.Stats = changed == new FigureStats() ? null : changed;

        figure.AddedTags = figure.AddedTags.Union(ExtraList(action, "addTags") ?? []).ToList();
        figure.RemovedTags = figure.RemovedTags.Union(ExtraList(action, "removeTags") ?? []).ToList();
        if (action.Extra?.TryGetValue("skipSteps", out var skipped) == true && skipped.ValueKind == JsonValueKind.Array)
        {
            figure.SkippedSteps = skipped.EnumerateArray().Select(s => s.GetInt32()).ToList();
        }

        if (action.Extra?.TryGetValue("asleep", out var asleep) == true)
        {
            figure.Asleep = asleep.ValueKind == JsonValueKind.True;
            figure.WakesWhenDamaged = figure.Asleep && ExtraTrue(action, "wakesWhenDamaged");
        }

        if (ExtraString(action, "side") is { } side)
        {
            figure.Side = side == EnemiesSide ? null : side;
        }

        Log("figureModified", action.Text, Args(("instance", figure.Instance)));
        if (MaxHealthOf(figure) is { } max && figure.Damage > 0 && figure.Damage >= max)
        {
            DefeatFigure(figure);
        }
    }

    private static T? ExtraObject<T>(ScenarioAction action, string key) where T : class =>
        action.Extra?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.Object
            ? value.Deserialize<T>(JsonDefaults.Options)
            : null;
}
