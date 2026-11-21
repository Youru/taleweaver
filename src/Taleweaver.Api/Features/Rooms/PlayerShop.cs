using Taleweaver.Api.Content;
using Taleweaver.Api.Engine;

namespace Taleweaver.Api.Features.Rooms;

/// <summary>A card of the Store and its price (the card's cost): the phone never says what a card costs.</summary>
public sealed record ShopItem(string Item, int Price);

/// <summary>
/// The Store a phone sees (room-spec §5.1, Histoire): during the Shop phase of a Story Round, out of an
/// encounter, every non-unique card matching the node's Store, and the Unique Items drawn for it that are still
/// there. The same list as the GM's (web: storeItems), computed here since the phone never gets the scenario.
/// </summary>
public static class PlayerShop
{
    public static IReadOnlyList<ShopItem>? Of(GameState state, Scenario? scenario, Catalog? catalog)
    {
        var campaign = state.Campaign;
        if (catalog is null || state.Encounter is not null || campaign.StoryRound is not { Phase: StoryRoundPhases.Shop } round
            || scenario?.Nodes.FirstOrDefault(n => n.Id == round.Node)?.Store is not { } store)
        {
            return null;
        }

        var cards = catalog.Packs.SelectMany(p => p.Items).GroupBy(i => i.Id).Select(g => g.Last()).ToList();
        var uniques = campaign.StoreUniques.GetValueOrDefault(round.Node, []).Where(id => !campaign.Items.Contains(id)).ToHashSet();
        var drawn = cards.Where(i => uniques.Contains(i.Id));
        var ordinary = cards.Where(i => !i.Unique && !i.Story && i.Tier != "monster" && store.Items.Any(line =>
            (line.Tiers.Count == 0 || (i.Tier is { } tier && line.Tiers.Contains(tier))) && (line.Types.Count == 0 || line.Types.Contains(i.Type))));
        // A card whose cost the catalogue does not know stays with the GM (they read it on the card)
        return drawn.Concat(ordinary).Where(i => i.Cost is not null).Select(i => new ShopItem(i.Id, i.Cost!.Value)).ToList();
    }
}
