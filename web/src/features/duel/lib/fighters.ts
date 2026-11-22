import type { Combatant } from '@/features/catalog'
import type { Cell, EncounterState } from '@/features/games'
import type { BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import { pick, type Language } from '@/shared/lib/localized'
import { symbolAbilities, type SymbolAbility } from './abilities'

/** What the GM keeps about an adventurer: Taleweaver does not track their sheet, so these come from the table. */
export interface AdventurerSheet {
  /** Combat dice of the equipped weapons (2 purple when unarmed). */
  pool: string[]
  defense: number | null
  armor: number
  conviction: string[]
  casting: string
}

/** Every Act 1 adventurer card prints Defense 7, Conviction 2 purple, Casting purple; weapons and armor come from items. */
export const defaultSheet: AdventurerSheet = {
  pool: ['purple', 'purple'],
  defense: 7,
  armor: 0,
  conviction: ['purple', 'purple'],
  casting: 'purple',
}

export interface Reminder {
  name: string
  text: string
}

/** A figure that can attack or be attacked, with what the duel needs from its card or sheet. */
export interface Fighter {
  /** Combatant instance, or "adventurer:<name>". */
  id: string
  name: string
  kind: 'combatant' | 'adventurer'
  /** Dice by colour; "*" when the card leaves it to another card. */
  pool: string[]
  defense: number | null
  armor: number | null
  conviction: string[]
  casting: string | null
  abilities: SymbolAbility[]
  /** Passives and symbol abilities the GM plays out (Follow Up…). */
  reminders: Reminder[]
  health?: number
  damage: number
  position?: Cell
  /** The figure's standee (URL), for its portrait. */
  portrait?: string
  /** The combatant card (URL), to read it during the duel. */
  card?: string
  /** Effects on the figure (catalogue ids: "darkness", "barrier"…), as the GM reported them. */
  effects: string[]
  /** ARMOR PIERCING its equipped cards always give. */
  pierce?: number
  /** An adventurer's equipped cards, when the dice, Defense and Armor come from them. */
  equipment?: string[]
  /** Equipped cards whose Defense is "X": to add by hand. */
  variableDefense?: string[]
}

/** What an adventurer's equipped cards give, by lower-case name (see equipment.ts). */
export type Equipment = Record<
  string,
  AdventurerSheet & { pierce: number; reminders: Reminder[]; items: string[]; variableDefense: string[] }
>

const numberOrNull = (value: number | string | undefined) => (typeof value === 'number' ? value : null)

/** Every figure still in the encounter: the combatants, then the adventurers. */
export function fighters(
  encounter: EncounterState,
  cards: Map<string, Combatant>,
  sheets: Record<string, AdventurerSheet>,
  language: Language,
  pieces?: BoardPieces,
  equipment: Equipment = {},
): Fighter[] {
  const figure = (...names: (string | undefined)[]) =>
    assetUrl(names.map((n) => (n ? pieces?.figures[n]?.image : undefined)).find((i) => !!i))
  const combatants = encounter.combatants
    .filter((c) => !c.defeated)
    .map((c): Fighter => {
      const card = cards.get(c.card)
      const stats = card?.stats
      const abilities = symbolAbilities(card?.symbolAbilities, language)
      return {
        id: c.instance,
        name: card ? `${c.instance} · ${pick(card.name, language)}` : c.instance,
        kind: 'combatant',
        pool: stats?.combatDice ?? [],
        defense: numberOrNull(stats?.defense),
        armor: numberOrNull(stats?.armor),
        conviction: stats?.convictionDice ?? [],
        casting: stats?.castingDie ?? null,
        abilities,
        reminders: [
          ...(card?.passives ?? []).map((p) => ({ name: pick(p.name, language), text: pick(p.text, language) })),
          ...abilities.filter((a) => !a.boost).map((a) => ({ name: symbolsLabel(a), text: a.text })),
        ],
        health: c.health ?? numberOrNull(stats?.health) ?? undefined,
        damage: c.damage,
        position: c.position,
        portrait: figure(c.figure, c.family),
        card: assetUrl(card?.image),
        effects: c.effects,
      }
    })
  const adventurers = encounter.adventurers.map((a): Fighter => {
    // The equipped cards when the party's items are tracked; otherwise what the GM entered last.
    const gear = equipment[a.name.toLowerCase()]
    const sheet = gear ?? { ...defaultSheet, ...sheets[a.name] }
    return {
      id: `adventurer:${a.name}`,
      name: a.name,
      kind: 'adventurer',
      pool: sheet.pool,
      defense: sheet.defense,
      armor: sheet.armor,
      conviction: sheet.conviction,
      casting: sheet.casting,
      abilities: [],
      reminders: gear?.reminders ?? [],
      damage: 0,
      position: a.position,
      portrait: figure(a.name.toLowerCase()),
      effects: a.effects ?? [],
      pierce: gear?.pierce,
      equipment: gear?.items,
      variableDefense: gear?.variableDefense,
    }
  })
  const allies = (encounter.allies ?? [])
    .filter((a) => !a.defeated)
    .map((a): Fighter => {
      const id = `ally:${a.id}`
      const name = pick(a.name, language) || a.id
      const card = cards.get(a.card)
      // Their numbers are the card's and their summoner's (scaling.ts), by figure id
      const gear = equipment[id]
      const sheet = gear ?? { ...defaultSheet, ...sheets[name] }
      return {
        id,
        name,
        kind: 'adventurer',
        pool: sheet.pool,
        defense: sheet.defense,
        armor: sheet.armor,
        conviction: sheet.conviction,
        casting: sheet.casting,
        abilities: symbolAbilities(card?.symbolAbilities, language),
        reminders: gear?.reminders ?? [],
        health: a.health ?? undefined,
        damage: a.damage,
        position: a.position ?? undefined,
        portrait: figure(a.figure ?? undefined, a.family),
        card: assetUrl(card?.image),
        effects: a.effects,
        pierce: gear?.pierce,
      }
    })
  return [...combatants, ...adventurers, ...allies]
}

function symbolsLabel(ability: SymbolAbility): string {
  const { shield, book, star } = ability.cost
  return [...Array<string>(shield).fill('🛡'), ...Array<string>(book).fill('📖'), ...Array<string>(star).fill('★')].join(
    '',
  )
}

/** Range is counted diagonally as well as orthogonally (rulebook p. 43). */
export function distance(a: Cell | undefined, b: Cell | undefined): number | undefined {
  return a && b ? Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y)) : undefined
}
