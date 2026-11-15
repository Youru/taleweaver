import type { Combatant } from '@/features/catalog'
import type { Cell, EncounterState } from '@/features/games'
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

export const defaultSheet: AdventurerSheet = {
  pool: ['purple', 'purple'],
  defense: null,
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
}

const numberOrNull = (value: number | string | undefined) => (typeof value === 'number' ? value : null)

/** Every figure still in the encounter: the combatants, then the adventurers. */
export function fighters(
  encounter: EncounterState,
  cards: Map<string, Combatant>,
  sheets: Record<string, AdventurerSheet>,
  language: Language,
): Fighter[] {
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
        health: numberOrNull(stats?.health) ?? undefined,
        damage: c.damage,
        position: c.position,
      }
    })
  const adventurers = encounter.adventurers.map((a): Fighter => {
    const sheet = { ...defaultSheet, ...sheets[a.name] }
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
      reminders: [],
      damage: 0,
      position: a.position,
    }
  })
  return [...combatants, ...adventurers]
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
