import type { CatalogAdventurer } from '@/features/catalog'
import type { AdventurerOffer, PartyPreparation, StarterKit } from '../api/preparation'

/** The kits this adventurer may take: theirs, and those of anybody. */
export function kitsOf(preparation: PartyPreparation, name: string): StarterKit[] {
  const offer = preparation.adventurers.find((a) => a.name === name)
  return preparation.kits.filter(
    (kit) =>
      !kit.adventurer ||
      (!!offer && (kit.adventurer === offer.id || kit.adventurer.toLowerCase() === offer.name.toLowerCase())),
  )
}

/** The adventurers offered who are not in the party yet. */
export function notChosen(preparation: PartyPreparation): AdventurerOffer[] {
  return preparation.adventurers.filter((a) => !preparation.choices.some((c) => c.adventurer === a.name))
}

/** "3 Throwing Knives" rather than three times the card. */
export function counted(ids: readonly string[]): { id: string; count: number }[] {
  const counts = new Map<string, number>()
  for (const id of ids) counts.set(id, (counts.get(id) ?? 0) + 1)
  return [...counts].map(([id, count]) => ({ id, count }))
}

/** The workshop's lists read a scenario's entry, written by id or by name. */
export function adventurerId(cards: readonly CatalogAdventurer[], entry: string): string {
  const lower = entry.toLowerCase()
  return cards.find((c) => c.id === entry || Object.values(c.name).some((n) => n?.toLowerCase() === lower))?.id ?? entry
}
