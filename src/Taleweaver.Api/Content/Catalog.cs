namespace Taleweaver.Api.Content;

/// <summary>
/// Read-only queries over the enabled packs. Built from what <see cref="Storage.IContentStore"/> returns,
/// so it does not depend on how the content is stored.
/// </summary>
public sealed class Catalog
{
    private readonly Dictionary<string, Combatant> _combatants;
    private readonly ILookup<string, Combatant> _families;
    private readonly HashSet<string> _ids;

    public Catalog(IEnumerable<Pack> packs)
    {
        Packs = packs.ToList();

        // Packs come dependencies first: a later pack replaces a card only when its manifest says so
        // (overrides: corrected "1.2" cards keep their original id)
        _combatants = [];
        foreach (var pack in Packs)
        {
            foreach (var combatant in pack.Combatants)
            {
                if (!_combatants.ContainsKey(combatant.Id) || pack.Manifest.Overrides.Contains(combatant.Id))
                {
                    _combatants[combatant.Id] = combatant;
                }
            }
        }

        _families = _combatants.Values.ToLookup(c => c.Family);
        _ids = Packs.SelectMany(AllIds).ToHashSet();
    }

    public IReadOnlyList<Pack> Packs { get; }

    public IEnumerable<Combatant> Combatants => _combatants.Values;

    /// <summary>Is this id ("um1:item/cave-sickle"…) defined by one of the packs?</summary>
    public bool Contains(string id) => _ids.Contains(id);

    public Combatant? FindCombatant(string id) => _combatants.GetValueOrDefault(id);

    /// <summary>The item card of this id (the last pack's, when several define it).</summary>
    public ItemUpgrade? FindUpgrade(string? id) => id is null ? null : Packs.SelectMany(p => p.ItemUpgrades).LastOrDefault(u => u.Id == id);

    public Discipline? FindDiscipline(string? id) => id is null ? null : Packs.SelectMany(p => p.Disciplines).LastOrDefault(d => d.Id == id);

    /// <summary>The Adventurer Card of a party member, by id or by name ("Rook", "rook"); the last pack's wins.</summary>
    public Adventurer? FindAdventurer(string? nameOrId) =>
        nameOrId is null
            ? null
            : Packs.SelectMany(p => p.Adventurers).LastOrDefault(a =>
                a.Id == nameOrId
                || a.Name.Values.Any(n => string.Equals(n, nameOrId, StringComparison.OrdinalIgnoreCase))
                || a.Id.EndsWith("/" + nameOrId, StringComparison.OrdinalIgnoreCase));

    public Item? FindItem(string? id) => id is null ? null : Packs.SelectMany(p => p.Items).LastOrDefault(i => i.Id == id);

    public TokenType? FindToken(string? id) => id is null ? null : Packs.SelectMany(p => p.Tokens).LastOrDefault(t => t.Id == id);

    /// <summary>The Intelligent cards of a family (empty when the family is unknown).</summary>
    public IReadOnlyList<Combatant> IntelligentCards(string family) =>
        _families[family].Where(c => c.Kind == "intelligent").ToList();

    /// <summary>
    /// Rulebook p. 79: the card of the family with the highest loot level that does not exceed the
    /// encounter's. A card with loot level "any" (or none) always qualifies, at the lowest priority.
    /// </summary>
    public Combatant? CardFor(string family, string? lootLevel)
    {
        var encounterRank = LootLevels.Rank(lootLevel);
        return IntelligentCards(family)
            .Where(c => c.LootLevel is null or LootLevels.Any
                        || (LootLevels.IsKnown(c.LootLevel) && encounterRank >= 0 && LootLevels.Rank(c.LootLevel) <= encounterRank))
            .OrderByDescending(c => LootLevels.Rank(c.LootLevel))
            .ThenBy(c => FirstActivation(c.Activation))
            .FirstOrDefault();
    }

    /// <summary>The first activation number printed ("1-3" → 1, "7-9" → 7): the first group comes first.</summary>
    private static int FirstActivation(string? activation) =>
        activation is not null && int.TryParse(new string(activation.TrimStart().TakeWhile(char.IsDigit).ToArray()), out var first) ? first : int.MaxValue;

    private static IEnumerable<string> AllIds(Pack pack) =>
        pack.Combatants.Select(c => c.Id)
            .Concat(pack.Items.Select(i => i.Id))
            .Concat(pack.ItemUpgrades.Select(u => u.Id))
            .Concat(pack.Disciplines.Select(d => d.Id))
            .Concat(pack.Adventurers.Select(a => a.Id))
            .Concat(pack.Tiles.Select(t => t.Id))
            .Concat(pack.Tokens.Select(t => t.Id))
            .Concat(pack.InitiativeCards.Select(i => i.Id));
}
