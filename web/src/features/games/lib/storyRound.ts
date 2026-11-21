import type { CatalogItem } from '@/features/catalog'
import type { Store } from '@/features/scenarios'
import type { CampaignState } from '../api/types'

/**
 * The cards for sale in a Story Round's Store (rulebook p. 13): every non-unique card matching one of
 * its lines ("all Mundane Items", "all Accessories"), never a Story Item nor Monster Loot, plus the
 * Unique Items drawn for this Story Round that the party does not hold yet.
 */
export function storeItems(store: Store | undefined, items: CatalogItem[], uniques: string[] = []): CatalogItem[] {
  if (!store) return []
  const matches = (item: CatalogItem) =>
    store.items.some(
      (line) =>
        (!line.tiers?.length || (!!item.tier && line.tiers.includes(item.tier))) &&
        (!line.types?.length || line.types.includes(item.type)),
    )
  const ordinary = items.filter((i) => !i.unique && !i.story && i.tier !== 'monster' && matches(i))
  const drawn = items.filter((i) => uniques.includes(i.id))
  return [...drawn, ...ordinary]
}

/** The members who play the encounters: the Active Group, without the Injured and the Unselectable. */
export function activeMembers(campaign: CampaignState): string[] {
  return campaign.party
    .filter((a) => !a.injured && !a.unselectable && (!campaign.activeGroup || campaign.activeGroup.includes(a.name)))
    .map((a) => a.name)
}
