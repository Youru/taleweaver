import type { CatalogItem, Combatant } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { pick, type Language } from '@/shared/lib/localized'
import { defaultSheet, type AdventurerSheet, type Equipment, type Reminder } from './fighters'

const printed = (dice: string[] | undefined) => !!dice?.length && !dice.includes('*')

/**
 * A Command card's numbers (rulebook p. 72): what is printed, and for "*" what its adventurer has. Their Defense is
 * the base one plus the Core and Armor only.
 */
export function commandSheet(
  card: Combatant | undefined,
  owner: AdventurerState | undefined,
  ownerSheet: AdventurerSheet | undefined,
  items: Map<string, CatalogItem>,
  language: Language,
): Equipment[string] {
  const base = ownerSheet ?? defaultSheet
  const stats = card?.stats
  let defense: number | null = null
  if (typeof stats?.defense === 'number') defense = stats.defense
  else if (stats?.defense === '*' && owner)
    defense = (owner.equipped ?? []).reduce((total, owned) => {
      const item = owned.item ? items.get(owned.item) : undefined
      const face = owned.flipped && item?.backSide?.stats ? item.backSide.stats : item?.stats
      const gained = item?.type === 'core' || item?.type === 'armor' ? face?.defense : undefined
      return total + (typeof gained === 'number' ? gained : 0)
    }, defaultSheet.defense ?? 7)
  // A card may print two Casting dice
  const casting = [stats?.castingDie ?? []].flat()
  const reminders: Reminder[] = [
    ...(card?.passives ?? []),
    ...(card?.abilities ?? []).map((a) => ({ name: a.name, text: a.text })),
  ].map((r) => ({ name: pick(r.name, language), text: pick(r.text, language) }))
  return {
    pool: printed(stats?.combatDice) ? stats!.combatDice! : base.pool,
    defense,
    armor: typeof stats?.armor === 'number' ? stats.armor : 0,
    conviction: printed(stats?.convictionDice) ? stats!.convictionDice! : base.conviction,
    casting: casting[0] && casting[0] !== '*' ? casting[0] : base.casting,
    pierce: 0,
    reminders,
    items: card ? [pick(card.name, language)] : [],
    variableDefense: [],
  }
}

/** The sheets, an adventurer in Conduit Form playing their Conduit card (p. 77). */
export function withConduitForms(
  equipment: Equipment,
  party: AdventurerState[] | undefined,
  cards: Map<string, Combatant>,
  items: Map<string, CatalogItem>,
  language: Language,
): Equipment {
  const sheets = { ...equipment }
  for (const member of party ?? []) {
    const key = member.name.toLowerCase()
    const card = member.conduitForm ? cards.get(member.conduitForm) : undefined
    if (card) sheets[key] = commandSheet(card, member, equipment[key], items, language)
  }
  return sheets
}
