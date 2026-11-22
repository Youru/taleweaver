using System.Globalization;
using System.Text.Json;
using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Skill Checks (rulebook p. 68): 2 purple dice and the Skill. The GM types the total, or just says passed or failed.
/// "combined": the totals are added up; "collective": each tests, all must pass ("needed": so many).
/// </summary>
internal sealed partial class Session
{
    private const string Combined = "combined";
    private const string Collective = "collective";
    private const string TotalOption = "total-";

    private void AskSkillCheck(ScenarioAction action)
    {
        var testers = TestersOf(action);
        var options = (action.Options ?? [])
            .Select((o, i) => new PromptOption($"pass-{i}", Text($"{o.Skill} {o.Difficulty}: passed", $"{o.Skill} {o.Difficulty} : réussi")))
            .Concat(action.Masterful is null ? [] : [new PromptOption("masterful", Text("Masterful Pass", "Réussite magistrale"))])
            .Append(new PromptOption("fail", Text("Failed", "Raté")));
        var question = action.Who is { ValueKind: JsonValueKind.Object } described ? described.Deserialize<Dictionary<string, string>>() : action.Question;
        Ask(PromptKinds.SkillCheck, question, options, action);

        var prompt = State.Prompt!;
        prompt.Testers = testers.Select(name => new SkillTester(name, Catalog.FindAdventurer(name)?.Skills ?? new Dictionary<string, int>())).ToList();
        prompt.Checks = (action.Options ?? []).Select(o => new SkillOption(o.Skill, o.Difficulty)).ToList();
    }

    private List<string> TestersOf(ScenarioAction action)
    {
        if (ExtraList(action, "waiting") is { Count: > 0 } waiting)
        {
            return [waiting[0]];
        }

        var all = AllTesters(action);
        return ModeOf(action) is null || all.Count == 0 ? all : [all[0]];
    }

    private List<string> AllTesters(ScenarioAction action)
    {
        var active = ActiveParty.Select(a => a.Name).Except(Excepted(action)).ToList();
        if (action.Who is { ValueKind: JsonValueKind.Array } list)
        {
            return list.EnumerateArray().Select(e => Member(e.GetString())?.Name).OfType<string>().ToList();
        }

        return WhoIs(action) switch
        {
            null or "each" => active,
            { } who when who.StartsWith('@') => Drawn(who[1..]),
            { } who => Member(who) is { } member ? [member.Name] : active,
        };
    }

    private static string? ModeOf(ScenarioAction action) => ExtraString(action, "mode") is { } mode && mode is Combined or Collective ? mode : null;

    private static bool IsTotal(Prompt prompt, GameCommand command) =>
        prompt.Kind == PromptKinds.SkillCheck && command.Value is not null
        && command.Option?.StartsWith(TotalOption, StringComparison.Ordinal) == true
        && int.TryParse(command.Option[TotalOption.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
        && index < Math.Max(1, prompt.Action?.Options?.Count ?? 0);

    private Result AnswerSkillCheck(Prompt prompt, GameCommand command)
    {
        var action = prompt.Action!;
        var only = prompt.Testers is { Count: 1 } testers ? testers[0].Name : null;
        var tester = MemberOf(command.Instance)?.Name ?? only;
        var option = command.Option!;
        if (!IsTotal(prompt, command))
        {
            var who = ModeOf(action) is null ? tester : null;
            var branch = option switch
            {
                "fail" => action.Fail,
                "masterful" => action.Masterful,
                _ => action.Options![int.Parse(option["pass-".Length..], CultureInfo.InvariantCulture)] is var chosen ? chosen.Pass ?? action.Pass : null,
            };
            PushOutcome(branch, who);
            return Result.Success();
        }

        var index = int.Parse(option[TotalOption.Length..], CultureInfo.InvariantCulture);
        var total = Math.Max(0, command.Value!.Value);
        var asked = action.Options is { Count: > 0 } choices ? choices[index] : null;
        Log("skillCheck", args: Args(("adventurer", tester), ("skill", asked?.Skill), ("difficulty", Number(asked?.Difficulty)), ("total", Number(total))));

        var mode = ModeOf(action);
        var totals = (action.Extra?.TryGetValue("totals", out var before) == true ? before.EnumerateArray().Select(e => e.GetInt32()) : []).Append(total).ToList();
        if (mode is not null)
        {
            var left = (ExtraList(action, "waiting") ?? AllTesters(action)).Skip(1).ToList();
            if (left.Count > 0)
            {
                // The skill chosen by the first holds for the others
                var next = With(With(action, "totals", totals), "waiting", left);
                Push([asked is null ? next : next with { Options = [asked] }]);
                return Result.Success();
            }
        }

        var difficulty = asked?.Difficulty;
        var (passed, result) = mode switch
        {
            Combined => (difficulty is null || totals.Sum() >= difficulty, totals.Sum()),
            Collective => (difficulty is null || totals.Count(t => t >= difficulty) >= (ExtraNumber(action, "needed") ?? totals.Count),
                totals.Count(t => difficulty is null || t >= difficulty)),
            _ => (difficulty is null || total >= difficulty, total),
        };
        Log(passed ? "skillPassed" : "skillFailed", args: Args(("adventurer", mode is null ? tester : null), ("result", Number(result))));
        // "Gold equal to the result × N", and the result kept for what follows
        if (passed && ExtraNumber(action, "goldPerPoint") is { } each)
        {
            GainGold(result * each);
        }

        if (ExtraString(action, "var") is { } variable)
        {
            SetVariable(variable, JsonSerializer.SerializeToElement(result));
        }

        PushOutcome(passed ? asked?.Pass ?? action.Pass : action.Fail, mode is null ? tester : null);
        return Result.Success();
    }

    private void PushOutcome(IReadOnlyList<ScenarioAction>? branch, string? tester) =>
        Push(tester is null ? branch ?? [] : Bind(branch ?? [], tester));
}
