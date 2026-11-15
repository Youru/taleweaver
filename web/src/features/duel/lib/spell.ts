import type { Dice } from '../api/types'
import { rollDistribution } from './dice'

/** Rulebook p. 53: the caster's Force against the target's Conviction Check. */
export interface SpellSetup {
  /** Casting die; none for a Spell-Like Ability (FORCE X), whose Force is printed. */
  casting: string | null
  /** The X of SPELL (X) or FORCE (X), plus the DIFFERENCE HIT when the ability says so. */
  force: number
  /** Black die added: +1 Force per book, the skull makes the Spell fail (3 IRREDUCIBLE DMG to the caster). */
  empower: boolean
  conviction: string[]
}

export interface SpellRoll {
  casting: number
  books: number
  skull: boolean
  conviction: number
}

export interface SpellOutcome {
  force: number
  /** Skull on the Empower die. */
  backfire: boolean
  /** The target's Conviction Check beat the Force. */
  resisted: boolean
  affected: boolean
}

export function resolveSpell(setup: SpellSetup, roll: SpellRoll): SpellOutcome {
  const force = setup.force + (setup.casting ? roll.casting : 0) + (setup.empower ? roll.books : 0)
  const backfire = setup.empower && roll.skull
  const resisted = roll.conviction >= force
  return { force, backfire, resisted, affected: !backfire && !resisted }
}

export interface SpellOdds {
  affected: number
  backfire: number
  /** Average Force. */
  force: number
}

export function spellOdds(setup: SpellSetup, dice: Dice): SpellOdds | undefined {
  const casts = rollDistribution([...(setup.casting ? [setup.casting] : []), ...(setup.empower ? ['black'] : [])], dice)
  const checks = rollDistribution(setup.conviction, dice)
  if (!casts || !checks) return undefined

  let affected = 0
  let backfire = 0
  let force = 0
  for (const cast of casts) {
    const roll = { casting: cast.total, books: cast.book, skull: cast.skull }
    const value = resolveSpell(setup, { ...roll, conviction: 0 }).force
    force += cast.p * value
    if (setup.empower && cast.skull) {
      backfire += cast.p
      continue
    }
    for (const check of checks) {
      if (check.total < value) affected += cast.p * check.p
    }
  }
  return { affected, backfire, force }
}
