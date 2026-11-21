import type { CatalogItem, ItemStats } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { pick, type Language, type LocalizedText } from '@/shared/lib/localized'
import { defaultSheet, type Equipment } from './fighters'

/**
 * What an adventurer's equipped cards give the duel: the sheet, the ARMOR PIERCING of their Passives
 * (Katar: +2), the cards to read (reminders), their names, and those whose Defense is "X" (not counted).
 */
export type EquipmentSheet = Equipment[string]

/** The side face up: the back's icons when the card is flipped and has its own. */
function sideOf(item: CatalogItem, flipped: boolean): { stats?: ItemStats | null; text?: LocalizedText | null } {
  return flipped && item.backSide
    ? { stats: item.backSide.stats, text: item.backSide.text }
    : { stats: item.stats, text: item.text }
}

/** "+2 ARMOR PIERCING" in the PASSIVE block of the card (always on while equipped). */
export function passivePierce(text: string | undefined): number {
  if (!text) return 0
  const passive = /PASSIVE\s*\n([\s\S]*?)(?=\n\s*[*✦>]?\s*(?:EXHAUST|COMBO|FLIP|PER ENCOUNTER|DISCARD|X DISCARD)\b|$)/.exec(
    text,
  )
  const match = passive?.[1] ? /^\s*[•]?\s*\+(\d+) ARMOR PIERCING\s*\.?\s*$/m.exec(passive[1]) : null
  return match?.[1] ? Number(match[1]) : 0
}

/**
 * The adventurer's sheet from what they have equipped (rulebook p. 25, 30): the combat dice of the
 * weapons (2 purple when no weapon has any), Defense 7 plus the Cores, Accessories and Armor, Armor 0
 * plus every bonus. Undefined when nothing equipped is known: the GM's own entries stay in use.
 */
export function equipmentSheet(
  adventurer: AdventurerState | undefined,
  catalog: Map<string, CatalogItem>,
  language: Language,
): EquipmentSheet | undefined {
  const equipped = (adventurer?.equipped ?? []).flatMap((owned) => {
    const item = owned.item ? catalog.get(owned.item) : undefined
    return item ? [{ item, ...sideOf(item, owned.flipped ?? false) }] : []
  })
  if (!equipped.length) return undefined

  const dice = equipped.flatMap((e) => e.stats?.dice ?? [])
  // A black die on two weapons is still one Empower die.
  const pool = dice.filter((d, i) => d !== 'black' || dice.indexOf('black') === i)
  const sum = (key: 'armor' | 'health') => equipped.reduce((total, e) => total + (e.stats?.[key] ?? 0), 0)
  const defense = equipped.reduce((total, e) => {
    const value = e.stats?.defense
    return total + (typeof value === 'number' ? value : 0)
  }, defaultSheet.defense ?? 7)
  return {
    ...defaultSheet,
    pool: pool.length ? pool : defaultSheet.pool,
    defense,
    armor: defaultSheet.armor + sum('armor'),
    pierce: equipped.reduce((total, e) => total + passivePierce(e.text?.en), 0),
    reminders: equipped.map((e) => ({
      name: pick(e.item.name, language),
      text: pick(e.text, language),
    })),
    items: equipped.map((e) => pick(e.item.name, language)),
    variableDefense: equipped.filter((e) => e.stats?.defense === 'X').map((e) => pick(e.item.name, language)),
  }
}

/** The sheets of the whole party, by lower-case name, for fighters(). */
export function partyEquipment(
  party: AdventurerState[] | undefined,
  catalog: Map<string, CatalogItem>,
  language: Language,
): Equipment {
  const sheets: Equipment = {}
  for (const adventurer of party ?? []) {
    const sheet = equipmentSheet(adventurer, catalog, language)
    if (sheet) sheets[adventurer.name.toLowerCase()] = sheet
  }
  return sheets
}
