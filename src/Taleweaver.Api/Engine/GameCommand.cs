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

    /// <summary>MoveInitiative: the card moved on the track (its places in <see cref="Value"/>: negative toward the front).</summary>
    public string? Card { get; init; }

    /// <summary>AddItem: the catalogue id of the card.</summary>
    public string? Item { get; init; }

    /// <summary>Items: who gets it, an adventurer's name, "stash" or "unassigned".</summary>
    public string? Owner { get; init; }

    /// <summary>Items given to an adventurer: "equipped" or "pack".</summary>
    public string? Slot { get; init; }

    /// <summary>AddItem: the item still to be drawn (Item Loot…) that this card is.</summary>
    public string? Replaces { get; init; }

    /// <summary>
    /// Who asked for it, when a player's request from the room was accepted (room-spec §9): the journal names
    /// them. Null for the GM's own commands.
    /// </summary>
    public string? By { get; init; }

    /// <summary>Answer to an "abraxis" question: the symbols extra or missing of each player still in, in their order.</summary>
    public IReadOnlyList<int>? Misses { get; init; }

    /// <summary>ChooseAdventurer: who, and their kit and purchases.</summary>
    public PartyChoice? Choice { get; init; }

    /// <summary>Damage to a Loyal Esper: from an opponent (its summoner takes it too); null: yes.</summary>
    public bool? FromOpponent { get; init; }

    /// <summary>Damage to a Loyal Esper: Summon tokens discarded, 2 less Damage each.</summary>
    public int? Tokens { get; init; }

    public static GameCommand ChooseAdventurer(string adventurer, string? kit = null, IReadOnlyList<string>? items = null, IReadOnlyList<string>? disciplines = null) =>
        new() { Type = GameCommands.ChooseAdventurer, Choice = new PartyChoice { Adventurer = adventurer, Kit = kit, Items = items, Disciplines = disciplines } };

    public static GameCommand RemoveAdventurer(string adventurer) => new() { Type = GameCommands.RemoveAdventurer, Instance = adventurer };

    public static GameCommand ShowHiddenAdventurers(bool shown) => new() { Type = GameCommands.ShowHiddenAdventurers, Active = shown };

    public static GameCommand SetOut() => new() { Type = GameCommands.SetOut };

    public static GameCommand Summon(string adventurer, string card, Cell? at = null) =>
        new() { Type = GameCommands.Summon, Instance = adventurer, Card = card, To = at };

    public static GameCommand AddAlly(string card, string? owner = null) => new() { Type = GameCommands.AddAlly, Instance = owner, Card = card };

    /// <summary><paramref name="ignoreDamage"/>: instead of its summoner's Damage; it counts as Defeated.</summary>
    public static GameCommand DismissAlly(string figure, bool ignoreDamage = false) =>
        new() { Type = GameCommands.DismissAlly, Instance = figure, Active = ignoreDamage };

    public static GameCommand EarnConduit(string adventurer, string card) => new() { Type = GameCommands.EarnConduit, Instance = adventurer, Card = card };

    /// <summary>No card: the Adventurer Card again.</summary>
    public static GameCommand ConduitForm(string adventurer, string? card) => new() { Type = GameCommands.ConduitForm, Instance = adventurer, Card = card };

    public static GameCommand AnswerAbraxis(IReadOnlyList<int> misses, int penalty = 0) =>
        new() { Type = GameCommands.Answer, Misses = misses, Value = penalty };

    public static GameCommand CompleteAchievement(string achievement, string adventurer) =>
        new() { Type = GameCommands.CompleteAchievement, Item = achievement, Instance = adventurer };

    /// <summary>SetActiveGroup: the adventurers chosen.</summary>
    public IReadOnlyList<string>? Adventurers { get; init; }

    public static GameCommand SetActiveGroup(params string[] adventurers) => new() { Type = GameCommands.SetActiveGroup, Adventurers = adventurers };

    /// <summary>Marks an adventurer <paramref name="status"/> ("injured", "unselectable") or no longer.</summary>
    public static GameCommand SetAdventurerStatus(string adventurer, string status, bool value) =>
        new() { Type = GameCommands.SetAdventurerStatus, Instance = adventurer, Effect = status, Active = value };

    /// <summary>An adventurer learns a Discipline (catalogue id, or its name) for <paramref name="xp"/> XP (used when the catalogue does not know its cost).</summary>
    public static GameCommand LearnDiscipline(string adventurer, string discipline, int xp) =>
        new() { Type = GameCommands.LearnDiscipline, Instance = adventurer, Item = discipline, Value = xp };

    /// <summary>The Discipline is given back (a mistake, or a re-specialisation): its XP is spent no more.</summary>
    public static GameCommand ForgetDiscipline(string adventurer, string discipline) =>
        new() { Type = GameCommands.ForgetDiscipline, Instance = adventurer, Item = discipline };

    /// <summary>The adventurer re-specs (rulebook p. 87): pays Gold, loses every Discipline but the Story ones.</summary>
    public static GameCommand Respec(string adventurer) => new() { Type = GameCommands.Respec, Instance = adventurer };

    /// <summary>An Item Upgrade is unlocked (a Material traded in, the story): for sale from now on.</summary>
    public static GameCommand UnlockUpgrade(string upgrade) => new() { Type = GameCommands.UnlockUpgrade, Item = upgrade };

    /// <summary>An Item Upgrade is attached to the card <paramref name="id"/>, for <paramref name="price"/> Gold (0: a Material traded in).</summary>
    public static GameCommand AttachUpgrade(string id, string upgrade, int? price = null) =>
        new() { Type = GameCommands.AttachUpgrade, Instance = id, Item = upgrade, Value = price };

    public static GameCommand DetachUpgrade(string id, string upgrade) => new() { Type = GameCommands.DetachUpgrade, Instance = id, Item = upgrade };

    public static GameCommand Answer(string option) => new() { Type = GameCommands.Answer, Option = option };

    public static GameCommand AnswerNumber(int value) => new() { Type = GameCommands.Answer, Value = value };

    public static GameCommand NextTurn() => new() { Type = GameCommands.NextTurn };

    public static GameCommand Damage(string instance, int amount) => new() { Type = GameCommands.Damage, Instance = instance, Value = amount };

    public static GameCommand SetEffect(string instance, string effect, bool active) =>
        new() { Type = GameCommands.SetEffect, Instance = instance, Effect = effect, Active = active };

    /// <summary>The adventurer now holds <paramref name="value"/> SP (spent, or gained by an ability).</summary>
    public static GameCommand SetStamina(string adventurer, int value) =>
        new() { Type = GameCommands.SetStamina, Instance = adventurer, Value = value };

    public static GameCommand Defeat(string instance) => new() { Type = GameCommands.Defeat, Instance = instance };

    public static GameCommand ActivateToken(string token) => new() { Type = GameCommands.ActivateToken, Token = token };

    /// <summary>Moves a figure: a combatant's instance, or "adventurer:&lt;name&gt;".</summary>
    public static GameCommand Move(string figure, Cell to) => new() { Type = GameCommands.Move, Instance = figure, To = to };

    /// <summary>Moves an initiative card: <paramref name="by"/> places, negative toward the front.</summary>
    public static GameCommand MoveInitiative(string card, int by) => new() { Type = GameCommands.MoveInitiative, Card = card, Value = by };

    public static GameCommand RevealArea(string area) => new() { Type = GameCommands.RevealArea, Area = area };

    /// <summary>The GM hands out a card: bought (<paramref name="price"/> Gold), looted, or the card drawn for <paramref name="replaces"/>.</summary>
    public static GameCommand AddItem(string item, string owner, string? slot = null, int? price = null, string? replaces = null) =>
        new() { Type = GameCommands.AddItem, Item = item, Owner = owner, Slot = slot, Value = price, Replaces = replaces };

    public static GameCommand MoveItem(string id, string owner, string? slot = null) =>
        new() { Type = GameCommands.MoveItem, Instance = id, Owner = owner, Slot = slot };

    /// <summary>The card leaves the party: discarded, or sold for <paramref name="gold"/>.</summary>
    public static GameCommand DiscardItem(string id, int? gold = null) => new() { Type = GameCommands.DiscardItem, Instance = id, Value = gold };

    public static GameCommand FlipItem(string id, bool flipped) => new() { Type = GameCommands.FlipItem, Instance = id, Active = flipped };

    /// <summary>
    /// An item is used: <paramref name="discard"/> it (a Consumable), and place a token on the board if it says so
    /// (Ubiquitous Plank: "Place a Plank token on an adjacent space").
    /// </summary>
    public static GameCommand UseItem(string id, bool discard, string? token = null, Cell? at = null) =>
        new() { Type = GameCommands.UseItem, Instance = id, Active = discard, Token = token, To = at };

    public static GameCommand ReachEndCondition(string endCondition) => new() { Type = GameCommands.ReachEndCondition, EndCondition = endCondition };
}

public static class GameCommands
{
    public const string Answer = "answer";
    public const string NextTurn = "nextTurn";
    public const string CompleteAchievement = "completeAchievement";
    public const string Damage = "damage";
    public const string SetEffect = "setEffect";
    public const string Defeat = "defeat";
    public const string SetStamina = "setStamina";
    public const string ActivateToken = "activateToken";
    public const string ReachEndCondition = "reachEndCondition";
    public const string Move = "move";
    public const string RevealArea = "revealArea";
    public const string MoveInitiative = "moveInitiative";
    public const string AddItem = "addItem";
    public const string MoveItem = "moveItem";
    public const string DiscardItem = "discardItem";
    public const string FlipItem = "flipItem";
    public const string UseItem = "useItem";
    public const string SetActiveGroup = "setActiveGroup";
    public const string SetAdventurerStatus = "setAdventurerStatus";
    public const string LearnDiscipline = "learnDiscipline";
    public const string ForgetDiscipline = "forgetDiscipline";
    public const string Respec = "respec";
    public const string UnlockUpgrade = "unlockUpgrade";
    public const string AttachUpgrade = "attachUpgrade";
    public const string DetachUpgrade = "detachUpgrade";

    public const string ChooseAdventurer = "chooseAdventurer";
    public const string RemoveAdventurer = "removeAdventurer";
    public const string ShowHiddenAdventurers = "showHiddenAdventurers";
    public const string SetOut = "setOut";
    public const string Summon = "summon";
    public const string AddAlly = "addAlly";
    public const string DismissAlly = "dismissAlly";
    public const string EarnConduit = "earnConduit";
    public const string ConduitForm = "conduitForm";

    public static bool IsPreparationCommand(string type) => type is ChooseAdventurer or RemoveAdventurer or ShowHiddenAdventurers or SetOut;

    public static bool IsAllyCommand(string type) => type is Summon or AddAlly or DismissAlly or EarnConduit or ConduitForm;

    /// <summary>Commands on the party's members: allowed out of encounters, whatever the question pending.</summary>
    public static bool IsPartyCommand(string type) => type is SetActiveGroup or SetAdventurerStatus or LearnDiscipline or ForgetDiscipline or Respec;

    /// <summary>Commands on the party's items: allowed in and out of encounters, whatever the question pending.</summary>
    public static bool IsItemCommand(string type) =>
        type is AddItem or MoveItem or DiscardItem or FlipItem or UseItem or UnlockUpgrade or AttachUpgrade or DetachUpgrade;
}
