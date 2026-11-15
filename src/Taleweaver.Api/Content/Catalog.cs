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
        _combatants = Packs.SelectMany(p => p.Combatants).ToDictionary(c => c.Id);
        _families = _combatants.Values.ToLookup(c => c.Family);
        _ids = Packs.SelectMany(AllIds).ToHashSet();
    }

    public IReadOnlyList<Pack> Packs { get; }

    public IEnumerable<Combatant> Combatants => _combatants.Values;

    /// <summary>Is this id ("um1:item/cave-sickle"…) defined by one of the packs?</summary>
    public bool Contains(string id) => _ids.Contains(id);

    public Combatant? FindCombatant(string id) => _combatants.GetValueOrDefault(id);

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
            .FirstOrDefault();
    }

    private static IEnumerable<string> AllIds(Pack pack) =>
        pack.Combatants.Select(c => c.Id)
            .Concat(pack.Items.Select(i => i.Id))
            .Concat(pack.ItemUpgrades.Select(u => u.Id))
            .Concat(pack.Tiles.Select(t => t.Id))
            .Concat(pack.Tokens.Select(t => t.Id))
            .Concat(pack.InitiativeCards.Select(i => i.Id));
}
