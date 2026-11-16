import type { Cell } from '@/features/games'

/**
 * The board as the AI helper sees it: the squares of the tiles in view, their Terrain (printed on the
 * tiles, surveyed by eye, then the Terrain tokens laid on them) and the figures. Elevation is not
 * known: every answer stays a suggestion the GM checks on the table.
 */
export interface Board {
  /** "x,y" of every square of a revealed tile. */
  squares: Set<string>
  figures: BoardFigure[]
  /** Terrain of the board (printed, then tokens), by "x,y". */
  terrain?: Map<string, Terrain>
}

/** The Terrain types that change movement or Line of Sight (rulebook p. 21). */
export type Terrain = 'obstructing' | 'hindering' | 'water' | 'muck' | 'dangerous'

/** The Terrain of a token, from its catalogue type ("um1:token/terrain/obstructing-long" → obstructing). */
export function terrainOf(type: string | undefined): Terrain | undefined {
  if (!type?.includes(':token/terrain/')) return undefined
  return (['obstructing', 'hindering', 'water', 'muck', 'dangerous'] as const).find((t) => type.includes(t))
}

const PRINTED: Record<string, Terrain> = {
  h: 'hindering',
  w: 'water',
  m: 'muck',
  o: 'obstructing',
  d: 'dangerous',
}

/** Where a square of a 6×6 tile lands once the tile is turned clockwise by `rotation` degrees. */
export function rotate(x: number, y: number, rotation: number, size = 6): Cell {
  const last = size - 1
  switch ((((Math.round(rotation / 90) % 4) + 4) % 4) as 0 | 1 | 2 | 3) {
    case 1:
      return { x: last - y, y: x }
    case 2:
      return { x: last - x, y: last - y }
    case 3:
      return { x: y, y: last - x }
    default:
      return { x, y }
  }
}

/**
 * The Terrain printed on a laid tile, by "x,y" of the board: `rows` is the survey of the side, tile
 * not rotated (". h w m o d s", one letter a square). Special squares carry no Terrain here.
 */
export function printedTerrain(tile: { x: number; y: number; rotation: number; printed?: string[] }) {
  const terrain = new Map<string, Terrain>()
  const rows = tile.printed ?? []
  rows.forEach((row, y) =>
    [...row].forEach((letter, x) => {
      const type = PRINTED[letter]
      if (!type) return
      const at = rotate(x, y, tile.rotation, rows.length)
      terrain.set(key({ x: tile.x + at.x, y: tile.y + at.y }), type)
    }),
  )
  return terrain
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
  /** Effects on the figure, as the GM reported them (catalogue ids: "darkness", "barrier"…). */
  effects?: string[]
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

  const terrain = (c: Cell) => board.terrain?.get(key(c))
  // Never into Obstructing Terrain; never into Dangerous Terrain either (ending there, or leaving it, defeats).
  const fits = (at: Cell) =>
    footprint(at, mover.size).every((c) => {
      const t = terrain(c)
      return board.squares.has(key(c)) && t !== 'obstructing' && t !== 'dangerous'
    })
  const blocked = (at: Cell) =>
    footprint(at, mover.size).some((c) => {
      const f = owner.get(key(c))
      return !!f && f.side !== mover.side
    })
  const occupied = (at: Cell) => footprint(at, mover.size).some((c) => owner.has(key(c)))
  /** Leaving Hindering or Water Terrain, or an ally's space, costs 2 (these do not stack). */
  const slow = (at: Cell) => footprint(at, mover.size).some((c) => terrain(c) === 'hindering' || terrain(c) === 'water')

  const best = new Map<string, number>([[key(mover.position), 0]])
  const queue: Reach[] = [{ at: mover.position, cost: 0 }]
  while (queue.length) {
    queue.sort((a, b) => a.cost - b.cost)
    const { at, cost } = queue.shift()!
    if (cost > (best.get(key(at)) ?? Infinity)) continue
    const leaving = occupied(at) || slow(at) ? 2 : 1
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

/** Line of Sight between two figures, and the penalty to the Attack roll it carries. */
export interface Sight {
  clear: boolean
  /** -1 through (or into) Hindering Terrain, -1 through allies; never more than -2 (rulebook p. 43). */
  penalty: number
}

/**
 * The squares a line from the centre of `a` to the centre of `b` crosses, in order, `b` excluded.
 * Where it passes exactly through a corner, both squares beside it are given: the players choose
 * which one counts (rulebook p. 42).
 */
export function lineCells(a: Cell, b: Cell): Cell[][] {
  const dx = b.x - a.x
  const dy = b.y - a.y
  const stepX = Math.sign(dx)
  const stepY = Math.sign(dy)
  const deltaX = dx ? 1 / Math.abs(dx) : Infinity
  const deltaY = dy ? 1 / Math.abs(dy) : Infinity
  let nextX = dx ? 0.5 / Math.abs(dx) : Infinity
  let nextY = dy ? 0.5 / Math.abs(dy) : Infinity
  let x = a.x
  let y = a.y
  const steps: Cell[][] = []
  while (x !== b.x || y !== b.y) {
    if (Math.abs(nextX - nextY) < 1e-9) {
      steps.push([
        { x: x + stepX, y },
        { x, y: y + stepY },
      ])
      x += stepX
      y += stepY
      nextX += deltaX
      nextY += deltaY
    } else if (nextX < nextY) {
      x += stepX
      nextX += deltaX
    } else {
      y += stepY
      nextY += deltaY
    }
    if (x !== b.x || y !== b.y) steps.push([{ x, y }])
  }
  return steps
}

/**
 * Line of Sight from `viewer` to `target` (rulebook p. 43): blocked by Obstructing Terrain, by the
 * edges of the tiles and by opponents; allies and Hindering Terrain only give -1 to the Attack roll.
 * With `soi` (Spells), only Obstructing Terrain and the tiles' edges count. For large bases, any
 * pair of squares will do.
 */
export function sight(board: Board, viewer: BoardFigure, target: BoardFigure, soi = false): Sight {
  const owner = new Map<string, BoardFigure>()
  for (const f of board.figures)
    if (f.id !== viewer.id && f.id !== target.id) for (const c of footprint(f.position, f.size)) owner.set(key(c), f)
  const terrain = (c: Cell) => board.terrain?.get(key(c))
  const own = new Set(footprint(viewer.position, viewer.size).map(key))

  /** What a square does to the line: blocks it, or the penalties it adds. */
  const effect = (c: Cell) => {
    if (own.has(key(c))) return { blocks: false, hindering: false, ally: false }
    const f = owner.get(key(c))
    const blocks = !board.squares.has(key(c)) || terrain(c) === 'obstructing' || (!soi && !!f && f.side !== viewer.side)
    return {
      blocks,
      hindering: !soi && terrain(c) === 'hindering',
      ally: !soi && !!f && f.side === viewer.side,
    }
  }

  let bestSight: Sight | undefined
  for (const from of footprint(viewer.position, viewer.size))
    for (const to of footprint(target.position, target.size)) {
      let hindering = !soi && terrain(to) === 'hindering'
      let ally = false
      let clear = true
      for (const choices of lineCells(from, to)) {
        // The players pick the square that suits them best.
        const options = choices.map(effect).filter((e) => !e.blocks)
        if (!options.length) {
          clear = false
          break
        }
        const chosen = options.sort(
          (p, q) => Number(p.hindering) + Number(p.ally) - Number(q.hindering) - Number(q.ally),
        )[0]!
        hindering ||= chosen.hindering
        ally ||= chosen.ally
      }
      if (!clear) continue
      const penalty = 0 - Number(hindering) - Number(ally)
      if (!bestSight || penalty > bestSight.penalty) bestSight = { clear: true, penalty }
    }
  return bestSight ?? { clear: false, penalty: 0 }
}
