import type { Cell } from '@/features/games'

/**
 * The board as the AI helper sees it: the squares of the tiles in view and the figures on them.
 * Tiles carry no terrain yet, so walls, Obstructing Terrain and Line of Sight are ignored: every
 * answer is a suggestion the GM checks on the table.
 */
export interface Board {
  /** "x,y" of every square of a revealed tile. */
  squares: Set<string>
  figures: BoardFigure[]
}

export type Side = 'combatant' | 'adventurer'

export interface BoardFigure {
  /** Combatant instance, or "adventurer:<name>". */
  id: string
  name: string
  side: Side
  /** Top-left square. */
  position: Cell
  /** 1, or 2 for a 2×2 base. */
  size: number
  /** Damage taken (combatants only: adventurers' sheets are not tracked). */
  damage: number
  /** Place on the Initiative Track: ties go to the figure closer to its front (rulebook p. 62). */
  initiative: number
}

export const key = (c: Cell) => `${c.x},${c.y}`

/** The squares a figure covers when its top-left square is `at`. */
export function footprint(at: Cell, size: number): Cell[] {
  const cells: Cell[] = []
  for (let dx = 0; dx < size; dx++) for (let dy = 0; dy < size; dy++) cells.push({ x: at.x + dx, y: at.y + dy })
  return cells
}

/** Range between two figures: counted diagonally as well as orthogonally, base to base (rulebook p. 43). */
export function range(a: Cell, aSize: number, b: Cell, bSize: number): number {
  const gapX = Math.max(0, b.x - (a.x + aSize - 1), a.x - (b.x + bSize - 1))
  const gapY = Math.max(0, b.y - (a.y + aSize - 1), a.y - (b.y + bSize - 1))
  return Math.max(gapX, gapY)
}

export const between = (a: BoardFigure, b: BoardFigure) => range(a.position, a.size, b.position, b.size)

/** A square the mover may end on, and what it costs to get there. */
export interface Reach {
  at: Cell
  cost: number
}

/**
 * Where a figure can end its movement (rulebook p. 41): orthogonal steps only, never through an
 * opponent, through an ally but never stopping on it, and leaving an ally's square costs 2.
 */
export function reachable(board: Board, mover: BoardFigure, movement: number): Reach[] {
  const owner = new Map<string, BoardFigure>()
  for (const f of board.figures)
    if (f.id !== mover.id) for (const c of footprint(f.position, f.size)) owner.set(key(c), f)

  const fits = (at: Cell) => footprint(at, mover.size).every((c) => board.squares.has(key(c)))
  const blocked = (at: Cell) =>
    footprint(at, mover.size).some((c) => {
      const f = owner.get(key(c))
      return !!f && f.side !== mover.side
    })
  const occupied = (at: Cell) => footprint(at, mover.size).some((c) => owner.has(key(c)))

  const best = new Map<string, number>([[key(mover.position), 0]])
  const queue: Reach[] = [{ at: mover.position, cost: 0 }]
  while (queue.length) {
    queue.sort((a, b) => a.cost - b.cost)
    const { at, cost } = queue.shift()!
    if (cost > (best.get(key(at)) ?? Infinity)) continue
    const leaving = occupied(at) ? 2 : 1
    for (const [dx, dy] of [
      [1, 0],
      [-1, 0],
      [0, 1],
      [0, -1],
    ] as const) {
      const next = { x: at.x + dx, y: at.y + dy }
      const total = cost + leaving
      if (total > movement || !fits(next) || blocked(next)) continue
      if (total >= (best.get(key(next)) ?? Infinity)) continue
      best.set(key(next), total)
      queue.push({ at: next, cost: total })
    }
  }
  return [...best.entries()]
    .map(([k, cost]) => {
      const [x, y] = k.split(',').map(Number) as [number, number]
      return { at: { x, y }, cost }
    })
    .filter((r) => r.cost === 0 || !occupied(r.at))
}
