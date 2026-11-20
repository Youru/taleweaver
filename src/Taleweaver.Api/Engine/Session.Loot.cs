using System.Globalization;
using System.Text.RegularExpressions;
using Taleweaver.Api.Content;

namespace Taleweaver.Api.Engine;

/// <summary>
/// The Combatant Loot deck (rulebook p. 20 and 57): built from the encounter's Loot LVL, one card drawn per
/// opponent Defeated, drawn cards kept out until the party Restores, Gold pooled and added at the end.
/// </summary>
internal sealed partial class Session
{
    private const string GoldCard = "gold:";
    private const string ItemCard = "item";
    private const string MonsterCard = "monster";
    private const string UniqueCard = "unique";

    /// <summary>The deck of the encounter's Loot LVL, in catalogue order: a draw takes a random card, as from a shuffled deck.</summary>
    private void BuildLootDeck(Node node)
    {
        var deck = Catalog.Packs
            .SelectMany(p => p.Rules?.LootDecks ?? [])
            .LastOrDefault(d => d.LootLevel == node.LootLevel);
        Encounter.LootDeck = deck is null
            ? []
            : deck.Cards.SelectMany(c => Enumerable.Repeat(CardKey(c), c.Count)).ToList();
    }

    private static string CardKey(LootCard card) => card.Kind switch
    {
        "gold" => GoldCard + (card.Value ?? 0).ToString(CultureInfo.InvariantCulture),
        _ => card.Kind,
    };

    /// <summary>An opponent was Defeated: one card, more with AVARICE (X), its Monster Loot only when GILDED (ch. 11).</summary>
    private void DropLoot(CombatantState figure)
    {
        var card = Catalog.FindCombatant(figure.Card);
        var tags = card?.Tags ?? [];
        if (tags.Contains("GILDED"))
        {
            if (!GainMonsterLoot(figure, card))
            {
                Log("lootNone", args: Args(("instance", figure.Instance)));
            }

            return;
        }

        var extra = tags.Select(t => Regex.Match(t, @"^AVARICE\s+(\d+)$")).Where(m => m.Success)
            .Sum(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        for (var i = 0; i < 1 + extra; i++)
        {
            DrawLoot(figure, card);
        }
    }

    private void DrawLoot(CombatantState figure, Combatant? card)
    {
        var encounter = Encounter;
        while (true)
        {
            if (encounter.LootDeck.Count == 0)
            {
                Log("lootDeckEmpty", args: Args(("instance", figure.Instance)));
                return;
            }

            var index = NextRandom(encounter.LootDeck.Count);
            var drawn = encounter.LootDeck[index];
            encounter.LootDeck.RemoveAt(index);

            // Monster Loot that cannot be drawn: shuffled back and redrawn, or discarded when it was the last card.
            if (drawn == MonsterCard && !CanGainMonsterLoot(card))
            {
                if (encounter.LootDeck.Count == 0)
                {
                    encounter.LootDrawn.Add(drawn);
                    Log("lootMonsterDiscarded", args: Args(("instance", figure.Instance)));
                    return;
                }

                encounter.LootDeck.Add(drawn);
                continue;
            }

            encounter.LootDrawn.Add(drawn);
            ApplyLoot(drawn, figure, card);
            return;
        }
    }

    private void ApplyLoot(string drawn, CombatantState figure, Combatant? card)
    {
        var encounter = Encounter;
        var lootLevel = EncounterNode.LootLevel;
        if (drawn.StartsWith(GoldCard, StringComparison.Ordinal))
        {
            var gold = int.Parse(drawn[GoldCard.Length..], CultureInfo.InvariantCulture);
            encounter.LootGold += gold;
            Log("lootGold", args: Args(("instance", figure.Instance), ("amount", Number(gold)), ("total", Number(encounter.LootGold))));
            return;
        }

        switch (drawn)
        {
            case ItemCard:
                // The Adventurer rolls 2 WHITE dice for the Item type (rulebook p. 20): physical dice, the GM draws the card.
                State.Campaign.Items.Add($"1 {lootLevel} item (Item Loot)");
                Log("lootItem", args: Args(("instance", figure.Instance), ("lootLevel", lootLevel)));
                break;
            case MonsterCard:
                GainMonsterLoot(figure, card);
                break;
            case UniqueCard:
                GainUniqueItem(figure, lootLevel);
                break;
        }
    }

    /// <summary>The Monster Loot cards of this combatant not yet in the party's hands.</summary>
    private List<string> MonsterLootLeft(Combatant? card) =>
        (card?.MonsterLoot ?? []).Where(id => !State.Campaign.Items.Contains(id)).ToList();

    private bool CanGainMonsterLoot(Combatant? card) => MonsterLootLeft(card).Count > 0;

    /// <summary>"Shuffle them and Pull a card from the bottom": one of the cards left, at random.</summary>
    private bool GainMonsterLoot(CombatantState figure, Combatant? card)
    {
        var left = MonsterLootLeft(card);
        if (left.Count == 0)
        {
            return false;
        }

        var item = left[NextRandom(left.Count)];
        State.Campaign.Items.Add(item);
        Log("lootMonster", args: Args(("instance", figure.Instance), ("item", item)));
        return true;
    }

    /// <summary>A random Unique Item of the encounter's Loot LVL, among those the party does not hold.</summary>
    private void GainUniqueItem(CombatantState figure, string? lootLevel)
    {
        var left = Catalog.Packs.SelectMany(p => p.Items)
            .Where(i => i.Unique && i.Tier == lootLevel && !State.Campaign.Items.Contains(i.Id))
            .Select(i => i.Id)
            .Distinct()
            .ToList();
        if (left.Count == 0)
        {
            Log("lootUniqueNone", args: Args(("instance", figure.Instance), ("lootLevel", lootLevel)));
            return;
        }

        var item = left[NextRandom(left.Count)];
        State.Campaign.Items.Add(item);
        Log("lootUnique", args: Args(("instance", figure.Instance), ("item", item)));
    }

    /// <summary>Restore Adventurers: the drawn Combatant Loot cards go back into the deck.</summary>
    private void ReturnLootCards()
    {
        if (State.Encounter is not { } encounter || encounter.LootDrawn.Count == 0)
        {
            return;
        }

        encounter.LootDeck.AddRange(encounter.LootDrawn);
        encounter.LootDrawn.Clear();
        Log("lootDeckShuffled", args: Args(("cards", Number(encounter.LootDeck.Count))));
    }

    /// <summary>Gold drawn from the deck is earned at the end of the encounter, even if the party is Defeated.</summary>
    private void BankLootGold()
    {
        var encounter = Encounter;
        if (encounter.LootGold <= 0)
        {
            return;
        }

        State.Campaign.Gold += encounter.LootGold;
        Log("gainGold", args: Args(("amount", Number(encounter.LootGold)), ("gold", Number(State.Campaign.Gold)), ("source", "loot")));
        encounter.LootGold = 0;
    }
}
