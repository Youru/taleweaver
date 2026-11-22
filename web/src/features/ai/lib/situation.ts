import type { Cell } from '@/features/games'
import { footprint, inReach, key, sight, type Board, type BoardFigure } from './grid'

/** What a figure Attacks with. */
export interface Weapon {
  melee: boolean
  /** Its Reach, for a Melee Attack. */
  reach: number
}

/** What the board says of an Attack: pre-ticked in the duel, the GM may untick it. */
export interface Situation {
  clear: boolean
  flank: boolean
  throughAlly: boolean
  hindering: boolean
  higher: boolean
  /** The target stands in Water: it may not DODGE. */
  inWater: boolean
}

const EPSILON = 1e-9
const same = (a: number, b: number) => Math.abs(a - b) < EPSILON

/** When the line from `from` by `step` is between `low` and `high`, as shares of the line. */
function span(from: number, step: number, low: number, high: number) {
  if (step) {
    const first = (low - from) / step
    const second = (high - from) / step
    return { enter: Math.min(first, second), exit: Math.max(first, second) }
  }
  const inside = from > low && from < high
  return inside ? { enter: -Infinity, exit: Infinity } : { enter: Infinity, exit: -Infinity }
}

/**
 * Whether the line between the centres of two squares goes through opposite sides, or opposite corners, of the
 * target's base (rulebook p. 50, example 4).
 */
export function crossesOpposite(a: Cell, b: Cell, target: Cell, size: number): boolean {
  const x = span(a.x + 0.5, b.x - a.x, target.x, target.x + size)
  const y = span(a.y + 0.5, b.y - a.y, target.y, target.y + size)
  const enter = Math.max(x.enter, y.enter)
  const exit = Math.min(x.exit, y.exit)
  if (exit - enter <= EPSILON || enter < 0 || exit > 1) return false
  // In and out by the same axes: a side answers the side opposite, a corner the corner opposite
  return same(x.enter, enter) === same(x.exit, exit) && same(y.enter, enter) === same(y.exit, exit)
}

/** Can make a Melee Attack against the target from where it stands. */
function threatens(board: Board, figure: BoardFigure, target: BoardFigure, weapon: Weapon | undefined): boolean {
  return (
    !!weapon?.melee &&
    !figure.effects?.includes('paralyze') &&
    inReach(board, figure, target, weapon.reach) &&
    sight(board, figure, target).clear
  )
}

/** `attack`: the weapon of this very Attack, when the AI instruction says it. */
export function situationOf(
  board: Board,
  attackerId: string,
  targetId: string,
  weaponOf: (id: string) => Weapon | undefined,
  attack?: Weapon,
): Situation | undefined {
  const attacker = board.figures.find((f) => f.id === attackerId)
  const target = board.figures.find((f) => f.id === targetId)
  if (!attacker || !target) return undefined

  const from = footprint(attacker.position, attacker.size)
  const opposite = (ally: BoardFigure) =>
    footprint(ally.position, ally.size).some((b) =>
      from.some((a) => crossesOpposite(a, b, target.position, target.size)),
    )
  const flank =
    attacker.side !== target.side &&
    threatens(board, attacker, target, attack ?? weaponOf(attacker.id)) &&
    board.figures.some(
      (ally) =>
        ally.id !== attacker.id &&
        ally.side === attacker.side &&
        threatens(board, ally, target, weaponOf(ally.id)) &&
        opposite(ally),
    )
  const view = sight(board, attacker, target)
  return {
    clear: view.clear,
    flank,
    throughAlly: !!view.ally,
    hindering: !!view.hindering,
    higher: !!view.higher,
    inWater: footprint(target.position, target.size).some((c) => board.terrain?.get(key(c)) === 'water'),
  }
}
