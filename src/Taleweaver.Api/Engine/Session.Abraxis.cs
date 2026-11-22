using Taleweaver.Api.Common;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// Abraxis Battle Dice (rulebook p. 85): the dice are the table's, the companion keeps the rounds, the points and
/// the cumulative rewards.
/// </summary>
internal sealed partial class Session
{
    private const int AbraxisRounds = 5;
    private const int AbraxisPoints = 10;

    private void StartAbraxis(ScenarioAction action)
    {
        State.Abraxis = new AbraxisGame
        {
            Round = 1,
            Name = action.Text?.ToDictionary(n => n.Key, n => n.Value),
            Rule = action.Note?.ToDictionary(n => n.Key, n => n.Value),
            Players = ActiveParty.Select(a => new AbraxisPlayer { Name = a.Name, Points = AbraxisPoints }).ToList(),
        };
        Log("abraxisStart", action.Text);
        Ask(PromptKinds.Abraxis, action.Note, [], action);
    }

    /// <summary>
    /// The end of a round: each player still in gives the symbols extra or missing (0: an exact match, +1 point),
    /// the card's own rule may take points from everyone.
    /// </summary>
    private Result AnswerAbraxis(GameCommand command, Prompt prompt)
    {
        var game = State.Abraxis!;
        var playing = game.Players.Where(p => p.Points > 0).ToList();
        var penalty = command.Value ?? 0;
        if (command.Misses is not { } misses || misses.Count != playing.Count || penalty < 0 || misses.Any(m => m < 0))
        {
            return GameEngine.Errors.InvalidAnswer(null);
        }

        State.Prompt = null;
        for (var i = 0; i < playing.Count; i++)
        {
            var player = playing[i];
            var change = (misses[i] == 0 ? 1 : -misses[i]) - penalty;
            player.Points = Math.Max(0, player.Points + change);
            Log("abraxisRound", args: Args(("adventurer", player.Name), ("round", Number(game.Round)), ("misses", Number(misses[i])), ("points", Number(player.Points))));
        }

        var survivors = game.Players.Count(p => p.Points > 0);
        if (game.Round < AbraxisRounds && survivors > 0)
        {
            game.Round++;
            Ask(PromptKinds.Abraxis, prompt.Question, [], prompt.Action);
            return Result.Success();
        }

        State.Abraxis = null;
        Log("abraxisEnd", args: Args(("survivors", Number(survivors))));
        Push((prompt.Action?.Rewards ?? []).Take(survivors).SelectMany(tier => tier));
        return Result.Success();
    }
}
