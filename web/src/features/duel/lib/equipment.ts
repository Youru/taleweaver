import type { CatalogItem, ItemStats, SymbolAbilityCard } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { pick, type Language, type LocalizedText } from '@/shared/lib/localized'
import { dieColours } from './dice'
import { symbolAbilities } from './abilities'
import { defaultSheet, type Equipment } from './fighters'

/**
 * What an adventurer's equipped cards give the duel: the sheet, the ARMOR PIERCING of their Passives
 * (Katar: +2), the cards to read (reminders), their names, and those whose Defense is "X" (not counted).
 */
export type EquipmentSheet = Equipment[string]

/** The side face up: the back's icons when the card is flipped and has its own. */
function sideOf(
  item: CatalogItem,
  flipped: boolean,
): { stats?: ItemStats | null; text?: LocalizedText | null; tags: string[]; symbols?: SymbolAbilityCard[] | null } {
  return flipped && item.backSide
    ? {
        stats: item.backSide.stats,
        text: item.backSide.text,
        tags: item.backSide.tags.length ? item.backSide.tags : item.tags,
        symbols: item.backSide.symbolAbilities,
      }
    : { stats: item.stats, text: item.text, tags: item.tags, symbols: item.symbolAbilities }
}

/** A COMBO block of a card (rulebook p. 93): its condition Tag and what it gives. */
export interface Combo {
  tag: string
  text: string
  /** FINESSE : COLOUR: the weapon's printed die becomes that one. */
  finesse?: string
  pierce?: number
}

/** "COMBO : LIGHT\nFINESSE : ORANGE" → { tag: 'LIGHT', finesse: 'orange' }. */
export function combosOf(text: string | undefined): Combo[] {
  if (!text) return []
  const combos: Combo[] = []
  const blocks =
    /COMBO\s*:\s*([A-Z][A-Z -]*?)\s*\n([\s\S]*?)(?=\n\s*[*✦>•]?\s*(?:PASSIVE|EXHAUST|COMBO|FLIP|PER ENCOUNTER|DISCARD|X DISCARD|STATUS)\b|$)/g
  for (const [, tag = '', body = ''] of text.matchAll(blocks)) {
    const colour = /FINESSE\s*:\s*([A-Z]+)/i.exec(body)?.[1]?.toLowerCase()
    const finesse = colour && (dieColours as readonly string[]).includes(colour) ? colour : undefined
    const pierce = /\+(\d+) ARMOR PIERCING/.exec(body)?.[1]
    combos.push({ tag: tag.trim(), text: body.trim(), finesse, pierce: pierce ? Number(pierce) : undefined })
  }
  return combos
}

/** A COMBO's Tag met by another card: one of its Tags, or MELEE / RANGED for a weapon that attacks so. */
function meets(other: { tags: string[]; stats?: ItemStats | null }, tag: string): boolean {
  if (other.tags.includes(tag)) return true
  const attack = other.stats?.dice?.length ? other.stats.attack : undefined
  return (tag === 'MELEE' || tag === 'RANGED') && (attack === 'either' || attack === tag.toLowerCase())
}

/** HEAVY (rulebook p. 93): -1 Movement and -1 Agility per equipped card that has it, face up. */
export function heavyCount(equipped: { tags: string[] }[]): number {
  return equipped.filter((e) => e.tags.includes('HEAVY')).length
}

/** "+2 ARMOR PIERCING" in the PASSIVE block of the card (always on while equipped). */
export function passivePierce(text: string | undefined): number {
  if (!text) return 0
  const passive =
    /PASSIVE\s*\n([\s\S]*?)(?=\n\s*[*✦>]?\s*(?:EXHAUST|COMBO|FLIP|PER ENCOUNTER|DISCARD|X DISCARD)\b|$)/.exec(text)
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

  // A COMBO counts when another equipped card has its Tag
  const combos = equipped.map((e, i) =>
    combosOf(e.text?.en).map((combo) => ({
      ...combo,
      on: equipped.some((other, j) => j !== i && meets(other, combo.tag)),
    })),
  )
  const dice = equipped.flatMap((e, i) => {
    const printed = e.stats?.dice ?? []
    const finesse = combos[i]?.find((c) => c.on && c.finesse)?.finesse
    const swapped = printed.findIndex((d) => d !== 'black')
    return finesse && swapped >= 0 ? printed.map((d, k) => (k === swapped ? finesse : d)) : printed
  })
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
    pierce:
      equipped.reduce((total, e) => total + passivePierce(e.text?.en), 0) +
      combos.flat().reduce((total, c) => total + (c.on ? (c.pierce ?? 0) : 0), 0),
    // Only weapons attack: their band at the bottom
    abilities: equipped.flatMap((e) =>
      e.stats?.dice?.length ? symbolAbilities(e.symbols ?? undefined, language) : [],
    ),
    heavy: heavyCount(equipped),
    combos: combos.flatMap((list, i) =>
      list.map((c) => ({ name: pick(equipped[i]!.item.name, language), tag: c.tag, on: c.on })),
    ),
    reminders: equipped.map((e, i) => {
      const states = (combos[i] ?? []).map((c) => `COMBO : ${c.tag} ${c.on ? '✓' : '✗'}`)
      return {
        name: [pick(e.item.name, language), ...states].join(' · '),
        text: pick(e.text, language),
      }
    }),
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
