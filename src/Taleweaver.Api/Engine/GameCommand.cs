using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// An action of the GM. The history of a game is its list of commands: replaying it from the start
/// rebuilds the state, which is how undo works. Only the fields of the given <see cref="Type"/> are set.
/// </summary>
public sealed record GameCommand
{
    public required string Type { get; init; }

    /// <summary>Answer: id of the chosen option.</summary>
    public string? Option { get; init; }

    /// <summary>Answer to a "number" question; damage amount.</summary>
    public int? Value { get; init; }

    public string? Instance { get; init; }

    /// <summary>Token key "type:colour".</summary>
    public string? Token { get; init; }

    public string? Effect { get; init; }

    public bool? Active { get; init; }

    public string? EndCondition { get; init; }

    /// <summary>Move: destination square.</summary>
    public Cell? To { get; init; }

    /// <summary>RevealArea: fog area.</summary>
    public string? Area { get; init; }

    public static GameCommand Answer(string option) => new() { Type = GameCommands.Answer, Option = option };

    public static GameCommand AnswerNumber(int value) => new() { Type = GameCommands.Answer, Value = value };

    public static GameCommand NextTurn() => new() { Type = GameCommands.NextTurn };

    public static GameCommand Damage(string instance, int amount) => new() { Type = GameCommands.Damage, Instance = instance, Value = amount };

    public static GameCommand SetEffect(string instance, string effect, bool active) =>
        new() { Type = GameCommands.SetEffect, Instance = instance, Effect = effect, Active = active };

    public static GameCommand Defeat(string instance) => new() { Type = GameCommands.Defeat, Instance = instance };

    public static GameCommand ActivateToken(string token) => new() { Type = GameCommands.ActivateToken, Token = token };

    /// <summary>Moves a figure: a combatant's instance, or "adventurer:&lt;name&gt;".</summary>
    public static GameCommand Move(string figure, Cell to) => new() { Type = GameCommands.Move, Instance = figure, To = to };

    public static GameCommand RevealArea(string area) => new() { Type = GameCommands.RevealArea, Area = area };

    public static GameCommand ReachEndCondition(string endCondition) => new() { Type = GameCommands.ReachEndCondition, EndCondition = endCondition };
}

public static class GameCommands
{
    public const string Answer = "answer";
    public const string NextTurn = "nextTurn";
    public const string Damage = "damage";
    public const string SetEffect = "setEffect";
    public const string Defeat = "defeat";
    public const string ActivateToken = "activateToken";
    public const string ReachEndCondition = "reachEndCondition";
    public const string Move = "move";
    public const string RevealArea = "revealArea";
}
