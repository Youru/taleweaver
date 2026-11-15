import type { Dice } from '../api/types'
import type { SymbolAbility } from './abilities'
import { rollDistribution, type Symbols } from './dice'

/** Everything known before the dice are rolled (rulebook p. 41, "Steps for Making an Attack"). */
export interface AttackSetup {
  /** Combat dice, by colour. A "black" die in it means the Attack is always Empowered. */
  pool: string[]
  /** Spend 1 SP to add the black die (step 3). */
  empower: boolean
  /** Flat bonus TO THE ATK ROLL: Flanking +1, through an ally or Hindering terrain −1… */
  toHit: number
  /** Flat +X PHYSICAL DMG (BACKSTAB +3…). */
  physical: number
  /** ARMOR PIERCING from items and abilities. */
  pierce: number
  /** The attacker's symbol abilities (spent for the most Damage, as Intelligent Combatants must). */
  abilities: SymbolAbility[]
  defense: number
  armor: number
  /** The target DODGEs: it rolls the black die, +1 Defense per shield, nothing on the skull (step 4). */
  dodge: boolean
}

/** What the table rolled. */
export interface AttackRoll extends Symbols {
  /** Sum of the numbers on the combat dice. */
  total: number
  /** Skull on the Empower die: the Attack misses whatever the numbers. */
  skull: boolean
  dodgeShields: number
  dodgeSkull: boolean
}

export interface Spent {
  ability: SymbolAbility
  times: number
}

export interface AttackOutcome {
  hit: boolean
  /** Missed because of the skull on the Empower die. */
  skull: boolean
  /** Roll plus bonuses TO THE ATK ROLL. */
  attack: number
  /** Defense, after a Dodge. */
  defense: number
  /** By how much the Attack hit (also the DIFFERENCE HIT of Follow Up Forces). */
  difference: number
  spent: Spent[]
  /** PHYSICAL DMG before Armor. */
  physical: number
  /** Armor left after ARMOR PIERCING. */
  armor: number
  magic: number
  /** Damage tokens for the target. */
  final: number
}

export function hasBlackDie(setup: Pick<AttackSetup, 'pool'>): boolean {
  return setup.pool.includes('black')
}

/** The dice actually rolled: the pool, plus the black die when Empowered (only once). */
export function rolledDice(setup: Pick<AttackSetup, 'pool' | 'empower'>): string[] {
  return setup.empower && !hasBlackDie(setup) ? [...setup.pool, 'black'] : setup.pool
}

/** Steps 5 and 6: hit or miss, then the Damage, spending the symbols for the most Final Damage. */
export function resolveAttack(setup: AttackSetup, roll: AttackRoll): AttackOutcome {
  const defense = setup.defense + (setup.dodge && !roll.dodgeSkull ? roll.dodgeShields : 0)
  const miss: AttackOutcome = {
    hit: false,
    skull: roll.skull,
    attack: roll.total + setup.toHit,
    defense,
    difference: 0,
    spent: [],
    physical: 0,
    armor: setup.armor,
    magic: 0,
    final: 0,
  }
  if (roll.skull) return miss

  const usable = setup.abilities.filter((a) => a.boost)
  let best: AttackOutcome = miss
  let bestUse: number[] = usable.map(() => 0)
  const use = usable.map(() => 0)

  const evaluate = () => {
    const sum = (kind: string) =>
      usable.reduce((total, a, i) => total + (a.boost?.kind === kind ? a.boost.amount * (use[i] ?? 0) : 0), 0)
    const attack = roll.total + setup.toHit + sum('toHit')
    if (attack < defense) return
    const difference = attack - defense
    const physical = difference + setup.physical + sum('physical')
    const armor = Math.max(0, setup.armor - setup.pierce - sum('pierce'))
    const magic = sum('magic')
    const final = Math.max(0, physical - armor) + magic
    // Most Final Damage; on a tie, the hit with the larger difference (it feeds Follow Up Forces).
    if (!best.hit || final > best.final || (final === best.final && difference > best.difference)) {
      best = { hit: true, skull: false, attack, defense, difference, spent: [], physical, armor, magic, final }
      bestUse = [...use]
    }
  }

  const search = (index: number, left: Symbols) => {
    if (index === usable.length) {
      evaluate()
      return
    }
    const cost = usable[index]!.cost
    for (let times = 0; ; times++) {
      use[index] = times
      search(index + 1, left)
      if (left.shield < cost.shield || left.book < cost.book || left.star < cost.star) break
      left = { shield: left.shield - cost.shield, book: left.book - cost.book, star: left.star - cost.star }
    }
    use[index] = 0
  }
  search(0, { shield: roll.shield, book: roll.book, star: roll.star })

  return {
    ...best,
    spent: usable.map((ability, i) => ({ ability, times: bestUse[i] ?? 0 })).filter((s) => s.times > 0),
  }
}

export interface AttackOdds {
  hit: number
  /** At least 1 Final Damage. */
  wound: number
  expected: number
  /** Chance of dealing at least the damage the target can still take. */
  defeat?: number
  /** Lowest total on the combat dice that hits before symbols and Dodge. */
  needed: number
  /** P(Final Damage = i). */
  damage: number[]
}

/** The odds, from every way the dice can land. Undefined when a die is unknown. */
export function attackOdds(setup: AttackSetup, dice: Dice, remaining?: number): AttackOdds | undefined {
  const rolls = rollDistribution(rolledDice(setup), dice)
  const dodges = setup.dodge ? rollDistribution(['black'], dice) : [{ shield: 0, skull: false, p: 1 }]
  if (!rolls || !dodges) return undefined

  const damage: number[] = []
  let hit = 0
  for (const roll of rolls) {
    for (const dodge of dodges) {
      const outcome = resolveAttack(setup, { ...roll, dodgeShields: dodge.shield, dodgeSkull: dodge.skull })
      const p = roll.p * dodge.p
      if (outcome.hit) hit += p
      damage[outcome.final] = (damage[outcome.final] ?? 0) + p
    }
  }
  const filled = Array.from(damage, (p) => p ?? 0)
  return {
    hit,
    wound: 1 - (filled[0] ?? 0),
    expected: filled.reduce((total, p, i) => total + p * i, 0),
    defeat:
      remaining === undefined ? undefined : filled.reduce((total, p, i) => (i >= remaining ? total + p : total), 0),
    needed: setup.defense - setup.toHit,
    damage: filled,
  }
}
