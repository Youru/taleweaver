import type { CatalogDiscipline, CatalogItem } from '@/features/catalog'
import type { Store } from '@/features/scenarios'
import type { AdventurerState, CampaignState } from '../api/types'

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

/** XP an adventurer has left: every XP gained, minus what their Disciplines cost. */
export function xpLeft(adventurer: AdventurerState): number {
  return adventurer.xp - (adventurer.disciplines ?? []).reduce((sum, d) => sum + d.xp, 0)
}

/**
 * The XP a Discipline costs this adventurer (p. 33): its printed cost, +1 for every Discipline of the same level
 * they already have. Null when the cost is not in the catalogue: the GM reads it on the card.
 */
export function disciplineCost(
  card: CatalogDiscipline,
  adventurer: AdventurerState,
  catalog: CatalogDiscipline[],
): number | null {
  if (card.cost == null) return null
  const levelOf = (id: string) => catalog.find((d) => d.id === id)?.level
  const sameLevel = (adventurer.disciplines ?? []).filter((d) => levelOf(d.id) === card.level).length
  return card.cost + sameLevel
}
