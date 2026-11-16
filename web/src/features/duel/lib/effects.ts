import type { Symbols } from './dice'

/**
 * What the Effects on the attacker and on the target change in an Attack or a Spell (rulebook,
 * Table 3 p. 95; packs/um1/rules.json). The rest (Poison, Condemn, Haste at the start of the turn,
 * Disease…) is played out of the duel.
 */
export interface EffectMods {
  /** TO THE ATK ROLL: Darkness on the attacker, −1. */
  toHit: number
  /** Barrier on the target: +1 Armor. */
  armor: number
  /** Nemesis on the target: +2 Damage of a type already dealt (here, PHYSICAL on an Attack). */
  physical: number
  /** Wilt on the target: +2 IRREDUCIBLE DMG (when it has no Resistances to ignore). */
  irreducible: number
  /** Courage on the attacker: a [Melee/Ranged] ATK gains a shield and a book. */
  symbols: Symbols
  /** Darkness on the target: it may not DODGE. */
  noDodge: boolean
  /** Paralyze on the attacker: it may not make Actions (nor Attack, nor cast). */
  paralyzed: boolean
  /** What was applied, and what the GM still has to do: i18n keys under `duel.effect.` with the effect named. */
  notes: { key: string; effect: string }[]
}

const has = (effects: string[] | undefined, id: string) => (effects ?? []).includes(id)

export function effectMods(attacker: string[] | undefined, target: string[] | undefined, spell = false): EffectMods {
  const mods: EffectMods = {
    toHit: 0,
    armor: 0,
    physical: 0,
    irreducible: 0,
    symbols: { shield: 0, book: 0, star: 0 },
    noDodge: false,
    paralyzed: has(attacker, 'paralyze'),
    notes: [],
  }
  const note = (key: string, effect: string) => mods.notes.push({ key, effect })
  if (mods.paralyzed) note('paralyzed', 'Paralyze')
  if (!spell) {
    if (has(attacker, 'darkness')) {
      mods.toHit -= 1
      note('darknessAttacker', 'Darkness')
    }
    if (has(attacker, 'courage')) {
      mods.symbols = { shield: 1, book: 1, star: 0 }
      note('courage', 'Courage')
    }
    if (has(target, 'darkness')) {
      mods.noDodge = true
      note('darknessTarget', 'Darkness')
    }
    if (has(target, 'barrier')) {
      mods.armor += 1
      note('barrier', 'Barrier')
    }
    if (has(target, 'nemesis')) {
      mods.physical += 2
      note('nemesis', 'Nemesis')
    }
    if (has(target, 'wilt')) {
      mods.irreducible += 2
      note('wilt', 'Wilt')
    }
  } else {
    if (has(target, 'nemesis')) note('nemesisSpell', 'Nemesis')
    if (has(target, 'wilt')) note('wiltSpell', 'Wilt')
  }
  if (has(target, 'tether')) note('tether', 'Tether')
  return mods
}
