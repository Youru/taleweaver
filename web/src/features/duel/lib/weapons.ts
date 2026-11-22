import type { Weapon } from '@/features/ai'
import type { CatalogItem, Combatant } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'

/**
 * What a combatant Attacks with, as its AI steps or Abilities print it. Both types on a card: Melee, as it may
 * then give Flanking; the instruction being played says which this Attack is.
 */
export function cardWeapon(card: Combatant | undefined): Weapon | undefined {
  const lines = [
    ...(card?.aiSteps ?? []).flatMap((step) => step.instructions.map((i) => i.text.en ?? '')),
    ...(card?.abilities ?? []).map((ability) => (ability.tags ?? []).join(' ')),
  ]
  const melee = lines.filter((line) => /\[Melee\]\s*(\d+\s*)?ATK/i.test(line))
  if (!melee.length) return undefined
  const reaches = melee.map((line) => Number(/\bREACH (\d+)/i.exec(line)?.[1] ?? 1))
  return { melee: true, reach: Math.max(...reaches) }
}

/**
 * What an adventurer Attacks with: Melee Weapons only (a Ranged one neither Gains nor gives Flanking, p. 50), and
 * the lowest of their Reach (p. 43). None known: nothing is pre-ticked.
 */
export function equippedWeapon(
  adventurer: AdventurerState | undefined,
  catalog: Map<string, CatalogItem>,
): Weapon | undefined {
  const weapons = (adventurer?.equipped ?? []).flatMap((owned) => {
    const item = owned.item ? catalog.get(owned.item) : undefined
    if (!item) return []
    const back = owned.flipped ? item.backSide : undefined
    const attack = (back?.stats ?? item.stats)?.attack
    return attack ? [{ attack, text: (back ?? item).text?.en ?? '' }] : []
  })
  // Melee/Ranged takes the type and Reach of the Weapon it is paired with
  const own = weapons.filter((w) => w.attack !== 'either')
  if (!own.length || own.some((w) => w.attack === 'ranged')) return undefined
  // A line of its own on the card: "• REACH 2"
  const reaches = own.map((w) => Number(/^\W*REACH (\d+)\W*$/m.exec(w.text)?.[1] ?? 1))
  return { melee: true, reach: Math.min(...reaches) }
}
