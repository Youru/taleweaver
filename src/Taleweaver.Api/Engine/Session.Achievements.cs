using System.Text.Json;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Engine;

/// <summary>Achievements (rulebook p. 17): once each, their reward for the adventurer who completed it.</summary>
internal sealed partial class Session
{
    private static class AchievementErrors
    {
        public static Error Unknown(string? id) => Error.NotFound("Achievements.Unknown", $"This encounter has no Achievement '{id}'.");

        public static Error AlreadyDone(string id) => Error.Conflict("Achievements.AlreadyDone", $"Achievement '{id}' is already completed.");
    }

    /// <summary>Reported at any time of the encounter, its last page included (win within 3 rounds…).</summary>
    private Result CompleteAchievement(string? id, string? adventurer)
    {
        var node = EncounterNode;
        if (node.Achievements.FirstOrDefault(a => a.Id == id) is not { } achievement)
        {
            return AchievementErrors.Unknown(id);
        }

        if (MemberOf(adventurer) is not { } member)
        {
            return GameEngine.Errors.UnknownCombatant(adventurer);
        }

        if (State.Campaign.Achievements.Any(a => a.Scenario == Scenario.Id && a.Node == node.Id && a.Id == achievement.Id))
        {
            return AchievementErrors.AlreadyDone(achievement.Id);
        }

        State.Campaign.Achievements.Add(new CompletedAchievement
        {
            Id = achievement.Id,
            Scenario = Scenario.Id,
            Node = node.Id,
            Adventurer = member.Name,
            Name = achievement.Name.ToDictionary(n => n.Key, n => n.Value),
        });
        Log("achievement", achievement.Name, Args(("achievement", achievement.Id), ("adventurer", member.Name)));
        if (achievement.HiddenText is not null)
        {
            Log("achievementText", achievement.HiddenText, Args(("achievement", achievement.Id)));
        }

        // Unless it says otherwise, only the adventurer who completed it gains the reward
        var achiever = JsonSerializer.SerializeToElement(member.Name);
        var rewards = achievement.Actions.Select(a => a.Do is "gainXp" or "gainItem" && a.Each != true && a.Who is null ? a with { Who = achiever } : a).ToList();
        // What is simply won is won at once, even on the encounter's last page; the rest waits for the question pending
        foreach (var gain in rewards.Where(a => a.Do is "gainXp" or "gainItem" or "gainGold"))
        {
            Do(gain);
        }

        Push(rewards.Where(a => a.Do is not ("gainXp" or "gainItem" or "gainGold")));
        return Result.Success();
    }
}
