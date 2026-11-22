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

    /// <summary>Who did it (defeated, activated, reached the end): an adventurer, or an ally for its summoner.</summary>
    public string? Author { get; init; }

    /// <summary>React, Tether: whom it is against, or on.</summary>
    public string? Against { get; init; }

    /// <summary>Resolve: how the Attack or Spell of <see cref="Instance"/> on <see cref="Against"/> ended.</summary>
    public Resolution? Resolution { get; init; }

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

    /// <summary>Exalted Summoning (rulebook p. 76): an Exalted Esper's Ability, once per encounter.</summary>
    public static GameCommand ExaltedSummon(string adventurer, string esper) =>
        new() { Type = GameCommands.ExaltedSummon, Instance = adventurer, Card = esper };

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

    /// <summary>The total rolled at a Skill Check, for the <paramref name="skill"/>th option.</summary>
    public static GameCommand AnswerTotal(int skill, int total, string? tester = null) =>
        new() { Type = GameCommands.Answer, Option = $"total-{skill}", Value = total, Instance = tester };

    public static GameCommand AnswerGroup(params string[] adventurers) => new() { Type = GameCommands.Answer, Adventurers = adventurers };

    /// <summary>What is carried goes to <paramref name="figure"/>; nobody: put down.</summary>
    public static GameCommand Carry(string id, string? figure, bool lost = false) =>
        new() { Type = GameCommands.Carry, Item = id, Instance = figure, Effect = lost ? "lose" : null };

    /// <summary>An action's cost taken off the SP: move, extraMove, attack, encounterAction, reequip, dodge, empower, counter, counterDodged.</summary>
    public static GameCommand Spend(string figure, string action) => new() { Type = GameCommands.Spend, Instance = figure, Effect = action };

    /// <summary>counter, counterDodged, break, redirect, poppet.</summary>
    public static GameCommand React(string figure, string kind, string? against) =>
        new() { Type = GameCommands.React, Instance = figure, Effect = kind, Against = against };

    public static GameCommand EndReaction() => new() { Type = GameCommands.EndReaction };

    public static GameCommand NoInstruction() => new() { Type = GameCommands.NoInstruction };

    public static GameCommand MarkCard(string figure, string card, string mark, bool on) =>
        new() { Type = GameCommands.MarkCard, Instance = figure, Item = card, Effect = mark, Active = on };

    public static GameCommand AbilityToken(string figure, string token, int value) =>
        new() { Type = GameCommands.AbilityToken, Instance = figure, Token = token, Value = value };

    public static GameCommand Tether(string placer, string? target) => new() { Type = GameCommands.Tether, Instance = placer, Against = target };

    public static GameCommand Reequipped(string adventurer) => new() { Type = GameCommands.Reequipped, Instance = adventurer };

    public static GameCommand FlipToken(string token) => new() { Type = GameCommands.FlipToken, Token = token };

    public static GameCommand Counter(string id, int by) => new() { Type = GameCommands.Counter, Item = id, Value = by };

    public static GameCommand DealInitiative() => new() { Type = GameCommands.DealInitiative };

    public static GameCommand SummonEsper(string card, string summoner, Cell? at = null) =>
        new() { Type = GameCommands.SummonEsper, Card = card, Instance = summoner, To = at };

    /// <summary>Several: the figures concerned.</summary>
    public IReadOnlyList<string>? Figures { get; init; }

    /// <summary>One of <see cref="Corrections"/>: what it is about (adventurer, flag, card, step) and what it becomes.</summary>
    public static GameCommand Correct(string field, int? value = null, string? instance = null, string? item = null, string? option = null, string? card = null, bool? active = null) =>
        new() { Type = GameCommands.Correct, Effect = field, Value = value, Instance = instance, Item = item, Option = option, Card = card, Active = active };

    public static GameCommand Ready() => new() { Type = GameCommands.Ready };

    /// <summary>Damage, or an Effect, on several figures at once.</summary>
    public static GameCommand Several(IReadOnlyList<string> figures, int? amount, string? effect, bool? active) =>
        new() { Type = GameCommands.Several, Figures = figures, Value = amount, Effect = effect, Active = active };

    /// <summary>"I am done", said during the turn of <paramref name="card"/> in <paramref name="round"/>: no other turn is ended.</summary>
    public static GameCommand EndTurnOf(string card, int round) => new() { Type = GameCommands.NextTurn, Card = card, Value = round };

    /// <summary>A player's claim on what waits to be handed out: nothing once someone has it.</summary>
    public static GameCommand Claim(string id, string adventurer) =>
        new() { Type = GameCommands.MoveItem, Instance = id, Owner = adventurer, Slot = "pack", Option = "unassigned" };

    public static GameCommand NextTurn() => new() { Type = GameCommands.NextTurn };

    public static GameCommand Damage(string instance, int amount) => new() { Type = GameCommands.Damage, Instance = instance, Value = amount };

    public static GameCommand SetEffect(string instance, string effect, bool active) =>
        new() { Type = GameCommands.SetEffect, Instance = instance, Effect = effect, Active = active };

    /// <summary>The adventurer now holds <paramref name="value"/> SP (spent, or gained by an ability).</summary>
    public static GameCommand SetStamina(string adventurer, int value) =>
        new() { Type = GameCommands.SetStamina, Instance = adventurer, Value = value };

    /// <summary>The target of an opponent's Attack, Spell or Ability, hit or not (Aggro, p. 60).</summary>
    public static GameCommand Target(string instance) => new() { Type = GameCommands.Target, Instance = instance };

    /// <summary>An Attack or a Spell validated from the duel, with its <paramref name="amount"/> of Damage.</summary>
    public static GameCommand Resolve(string attacker, string target, int amount, Resolution resolution, int? tokens = null) =>
        new() { Type = GameCommands.Resolve, Instance = attacker, Against = target, Value = amount, Resolution = resolution, Tokens = tokens };

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
    public const string Correct = "correct";
    public const string Ready = "ready";
    public const string Several = "several";
    public const string CompleteAchievement = "completeAchievement";
    public const string Damage = "damage";
    public const string SetEffect = "setEffect";
    public const string Defeat = "defeat";
    public const string Target = "target";
    public const string Resolve = "resolve";
    public const string SetStamina = "setStamina";
    public const string ActivateToken = "activateToken";
    public const string ReachEndCondition = "reachEndCondition";
    public const string Move = "move";
    public const string Carry = "carry";
    public const string Counter = "counter";
    public const string DealInitiative = "dealInitiative";
    public const string SummonEsper = "summonEsper";
    public const string Spend = "spend";
    public const string React = "react";
    public const string EndReaction = "endReaction";
    public const string NoInstruction = "noInstruction";
    public const string MarkCard = "card";
    public const string AbilityToken = "abilityToken";
    public const string Tether = "tether";
    public const string Reequipped = "reequipped";
    public const string FlipToken = "flipToken";
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
    public const string ExaltedSummon = "exaltedSummon";
    public const string AddAlly = "addAlly";
    public const string DismissAlly = "dismissAlly";
    public const string EarnConduit = "earnConduit";
    public const string ConduitForm = "conduitForm";

    public static bool IsPreparationCommand(string type) => type is ChooseAdventurer or RemoveAdventurer or ShowHiddenAdventurers or SetOut;

    public static bool IsAllyCommand(string type) => type is Summon or ExaltedSummon or AddAlly or DismissAlly or EarnConduit or ConduitForm;

    /// <summary>Commands on the party's members: allowed out of encounters, whatever the question pending.</summary>
    public static bool IsPartyCommand(string type) => type is SetActiveGroup or SetAdventurerStatus or LearnDiscipline or ForgetDiscipline or Respec;

    /// <summary>Commands on the party's items: allowed in and out of encounters, whatever the question pending.</summary>
    public static bool IsItemCommand(string type) =>
        type is AddItem or MoveItem or DiscardItem or FlipItem or UseItem or UnlockUpgrade or AttachUpgrade or DetachUpgrade;
}
