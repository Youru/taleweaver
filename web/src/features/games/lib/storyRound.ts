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
  if (card.story) return 0
  if (card.cost == null) return null
  const sameLevel = learnt(adventurer, catalog).filter((d) => !d.story && d.level === card.level).length
  return card.cost + sameLevel
}

/** The catalogue cards an adventurer has learnt (those the catalogue knows). */
function learnt(adventurer: AdventurerState, catalog: CatalogDiscipline[]): CatalogDiscipline[] {
  return (adventurer.disciplines ?? []).flatMap((d) => catalog.filter((c) => c.id === d.id))
}

/**
 * Why the adventurer cannot learn this card yet (p. 24, 32): already learnt, or the lowest level of its tree they
 * still lack (one Discipline of each lower level is needed). Null when they can.
 */
export function disciplineBlock(
  card: CatalogDiscipline,
  adventurer: AdventurerState,
  catalog: CatalogDiscipline[],
): { learnt: true } | { missingLevel: number } | null {
  if ((adventurer.disciplines ?? []).some((d) => d.id === card.id)) return { learnt: true }
  if (card.story || !card.level) return null
  const known = new Set(learnt(adventurer, catalog).filter((d) => !d.story && d.tree === card.tree).map((d) => d.level))
  for (let level = 1; level < card.level; level++) if (!known.has(level)) return { missingLevel: level }
  return null
}

/**
 * Gold to Re-spec (p. 87): 25, +25 per Loot Level beyond Mundane of the Story Round's Items for sale
 * (the highest tier of its Store's lines and Unique Items).
 */
export function respecPrice(store: Store | undefined): number {
  const order = ['mundane', 'common', 'uncommon', 'rare']
  const tiers = [...(store?.items ?? []).flatMap((line) => line.tiers ?? []), store?.unique?.tier]
  return 25 * (1 + Math.max(0, ...tiers.map((tier) => order.indexOf(tier ?? ''))))
}
