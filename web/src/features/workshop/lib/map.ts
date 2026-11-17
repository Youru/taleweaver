import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftNode } from '../api/types'

/**
 * An encounter's board as the map editor changes it: tiles, tokens, zones, fog areas, and the
 * combatants of the setup. Coordinates in squares, x to the east and y to the south.
 * Every field the editor does not know is kept as it is.
 */
export interface Rect {
  x: number
  y: number
  w: number
  h: number
}

export interface DraftTile {
  ref: string
  x: number
  y: number
  rotation?: number | null
  area?: string
  [field: string]: unknown
}

export interface DraftToken {
  id: string
  type: string
  color?: string
  /** The picture shown, when not its type's or its colour's: one of the type's variants. */
  image?: string
  x: number
  y: number
  w?: number
  h?: number
  area?: string
  [field: string]: unknown
}

export interface DraftZone {
  id: string
  kind?: string
  rect: Rect
  [field: string]: unknown
}

export interface DraftArea {
  id: string
  revealedAtStart?: boolean
  name?: LocalizedText
  [field: string]: unknown
}

export interface DraftMap {
  tiles: DraftTile[]
  tokens: DraftToken[]
  zones: DraftZone[]
  areas: DraftArea[]
  [field: string]: unknown
}

export interface DraftPlacement {
  family?: string
  ref?: string
  instance?: string
  at?: { x: number; y: number } | null
  [field: string]: unknown
}

export interface DraftSetup {
  combatants: DraftPlacement[]
  adventurersStart?: string
  [field: string]: unknown
}

/** What can be selected and moved on the map editor. */
export type MapItem =
  | { kind: 'tile'; index: number }
  | { kind: 'token'; id: string }
  | { kind: 'zone'; id: string }
  | { kind: 'combatant'; index: number }

export const TILE = 6
export const START_ZONE = 'startingZone'

export function mapOf(node: DraftNode): DraftMap {
  const map = (node.map ?? {}) as Partial<DraftMap>
  return { ...map, tiles: map.tiles ?? [], tokens: map.tokens ?? [], zones: map.zones ?? [], areas: map.areas ?? [] }
}

export function setupOf(node: DraftNode): DraftSetup {
  const setup = (node.setup ?? {}) as Partial<DraftSetup>
  return { ...setup, combatants: setup.combatants ?? [] }
}

/** The node with its map and setup changed together (a zone removed may be the adventurers' start). */
export function withBoard(
  node: DraftNode,
  change: (board: { map: DraftMap; setup: DraftSetup }) => { map: DraftMap; setup: DraftSetup },
): DraftNode {
  const { map, setup } = change({ map: mapOf(node), setup: setupOf(node) })
  return { ...node, map, setup }
}

/**
 * "um1:token/objective" + "blue" → "objective:blue": the key the token rules and events use. Like the
 * engine, only the last part of the type counts ("um1:token/terrain/obstructing" → "obstructing").
 */
export function tokenKey(type: string, color?: string): string {
  const short = type.slice(type.lastIndexOf('/') + 1)
  return color ? `${short}:${color}` : short
}

/** "2x1" → 2 wide, 1 high. */
export function sizeOf(size: string | undefined): { w: number; h: number } {
  const match = /^(\d+)x(\d+)$/.exec(size ?? '')
  return match ? { w: Number(match[1]), h: Number(match[2]) } : { w: 1, h: 1 }
}

export function tokenRect(token: DraftToken): Rect {
  return { x: token.x, y: token.y, w: token.w ?? 1, h: token.h ?? 1 }
}

function fresh(used: Iterable<string>, prefix: string): string {
  const taken = new Set(used)
  if (!taken.has(prefix) && prefix === 'start') return prefix
  let n = 1
  while (taken.has(`${prefix}-${n}`)) n++
  return `${prefix}-${n}`
}

// Tiles

/**
 * Lays a tile; each one is in the box once, front or back: nothing changes when it, or its other side
 * (`pairedSide`), is already on the board.
 */
export function addTile(map: DraftMap, ref: string, at: { x: number; y: number }, pairedSide?: string): DraftMap {
  if (map.tiles.some((t) => t.ref === ref || (!!pairedSide && t.ref === pairedSide))) return map
  return { ...map, tiles: [...map.tiles, { ref, x: at.x, y: at.y, rotation: 0 }] }
}

export function rotateTile(map: DraftMap, index: number, by = 90): DraftMap {
  return {
    ...map,
    tiles: map.tiles.map((t, i) =>
      i === index ? { ...t, rotation: ((((t.rotation ?? 0) + by) % 360) + 360) % 360 } : t,
    ),
  }
}

export function setTileArea(map: DraftMap, index: number, area: string | undefined): DraftMap {
  return { ...map, tiles: map.tiles.map((t, i) => (i === index ? withField(t, 'area', area) : t)) }
}

/** Tiles laid over one another: their indexes (a mistake the author should see). */
export function overlappingTiles(map: DraftMap): Set<number> {
  const overlap = new Set<number>()
  map.tiles.forEach((a, i) =>
    map.tiles.forEach((b, j) => {
      if (i < j && Math.abs(a.x - b.x) < TILE && Math.abs(a.y - b.y) < TILE) {
        overlap.add(i)
        overlap.add(j)
      }
    }),
  )
  return overlap
}

// Tokens

export function addToken(
  map: DraftMap,
  piece: { type: string; color?: string; size?: string; image?: string },
  at: { x: number; y: number },
): { map: DraftMap; id: string } {
  const short = piece.type.replace(/^[^:]+:token\//, '').replace(/\//g, '-')
  const id = fresh(
    map.tokens.map((t) => t.id),
    piece.color ? `${short}-${piece.color}` : short,
  )
  const { w, h } = sizeOf(piece.size)
  const token: DraftToken = { id, type: piece.type, x: at.x, y: at.y }
  if (piece.color) token.color = piece.color
  if (piece.image) token.image = piece.image
  if (w !== 1 || h !== 1) Object.assign(token, { w, h })
  return { map: { ...map, tokens: [...map.tokens, token] }, id }
}

/** A long token turned a quarter: 2×1 becomes 1×2. */
export function turnToken(map: DraftMap, id: string): DraftMap {
  return {
    ...map,
    tokens: map.tokens.map((t) => (t.id === id ? { ...t, w: t.h ?? 1, h: t.w ?? 1 } : t)),
  }
}

export function patchToken(map: DraftMap, id: string, fields: Partial<DraftToken>): DraftMap {
  return {
    ...map,
    tokens: map.tokens.map((t) =>
      t.id === id ? Object.entries(fields).reduce<DraftToken>((token, [k, v]) => withField(token, k, v), t) : t,
    ),
  }
}

// Zones

export function addZone(map: DraftMap, kind: string, rect: Rect): { map: DraftMap; id: string } {
  const id = fresh(
    map.zones.map((z) => z.id),
    kind === START_ZONE ? 'start' : 'zone',
  )
  return { map: { ...map, zones: [...map.zones, { id, kind, rect: normalize(rect) }] }, id }
}

export function patchZone(map: DraftMap, id: string, fields: Partial<DraftZone>): DraftMap {
  return { ...map, zones: map.zones.map((z) => (z.id === id ? { ...z, ...fields } : z)) }
}

/** A rectangle from two opposite squares, whichever way the author dragged. */
export function spanOf(a: { x: number; y: number }, b: { x: number; y: number }): Rect {
  return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), w: Math.abs(a.x - b.x) + 1, h: Math.abs(a.y - b.y) + 1 }
}

function normalize(rect: Rect): Rect {
  return { x: rect.x, y: rect.y, w: Math.max(1, rect.w), h: Math.max(1, rect.h) }
}

// Fog areas

export function addArea(map: DraftMap, name: LocalizedText): { map: DraftMap; id: string } {
  const id = fresh(
    map.areas.map((a) => a.id),
    'area',
  )
  return { map: { ...map, areas: [...map.areas, { id, name, revealedAtStart: false }] }, id }
}

export function patchArea(map: DraftMap, id: string, fields: Partial<DraftArea>): DraftMap {
  return { ...map, areas: map.areas.map((a) => (a.id === id ? { ...a, ...fields } : a)) }
}

/** Removes a fog area: its tiles and tokens are no longer hidden. */
export function removeArea(map: DraftMap, id: string): DraftMap {
  return {
    ...map,
    areas: map.areas.filter((a) => a.id !== id),
    tiles: map.tiles.map((t) => (t.area === id ? withField(t, 'area', undefined) : t)),
    tokens: map.tokens.map((t) => (t.area === id ? withField(t, 'area', undefined) : t)),
  }
}

/** The fog area a square lies in (that of the tile under it). */
export function areaAt(map: DraftMap, x: number, y: number): string | undefined {
  return map.tiles.find((t) => x >= t.x && x < t.x + TILE && y >= t.y && y < t.y + TILE)?.area
}

// Setup

export function addCombatant(setup: DraftSetup, family: string, at: { x: number; y: number }): DraftSetup {
  return { ...setup, combatants: [...setup.combatants, { family, at: { x: at.x, y: at.y } }] }
}

// Every item

export function rectOf(board: { map: DraftMap; setup: DraftSetup }, item: MapItem, size = 1): Rect | undefined {
  switch (item.kind) {
    case 'tile': {
      const t = board.map.tiles[item.index]
      return t && { x: t.x, y: t.y, w: TILE, h: TILE }
    }
    case 'token': {
      const t = board.map.tokens.find((x) => x.id === item.id)
      return t && tokenRect(t)
    }
    case 'zone':
      return board.map.zones.find((z) => z.id === item.id)?.rect
    case 'combatant': {
      const at = board.setup.combatants[item.index]?.at
      return at ? { x: at.x, y: at.y, w: size, h: size } : undefined
    }
  }
}

/** Moves an item so that its top-left square is `to`. A token takes the fog area of its new square. */
export function moveItem(
  board: { map: DraftMap; setup: DraftSetup },
  item: MapItem,
  to: { x: number; y: number },
): { map: DraftMap; setup: DraftSetup } {
  const { map, setup } = board
  switch (item.kind) {
    case 'tile':
      return {
        setup,
        map: { ...map, tiles: map.tiles.map((t, i) => (i === item.index ? { ...t, x: to.x, y: to.y } : t)) },
      }
    case 'token':
      return {
        setup,
        map: {
          ...map,
          tokens: map.tokens.map((t) =>
            t.id === item.id ? withField({ ...t, x: to.x, y: to.y }, 'area', areaAt(map, to.x, to.y)) : t,
          ),
        },
      }
    case 'zone':
      return {
        setup,
        map: {
          ...map,
          zones: map.zones.map((z) => (z.id === item.id ? { ...z, rect: { ...z.rect, x: to.x, y: to.y } } : z)),
        },
      }
    case 'combatant':
      return {
        map,
        setup: {
          ...setup,
          combatants: setup.combatants.map((c, i) => (i === item.index ? { ...c, at: { x: to.x, y: to.y } } : c)),
        },
      }
  }
}

export function removeItem(
  board: { map: DraftMap; setup: DraftSetup },
  item: MapItem,
): { map: DraftMap; setup: DraftSetup } {
  const { map, setup } = board
  switch (item.kind) {
    case 'tile':
      return { setup, map: { ...map, tiles: map.tiles.filter((_, i) => i !== item.index) } }
    case 'token':
      return { setup, map: { ...map, tokens: map.tokens.filter((t) => t.id !== item.id) } }
    case 'zone':
      return {
        map: { ...map, zones: map.zones.filter((z) => z.id !== item.id) },
        setup: setup.adventurersStart === item.id ? withField(setup, 'adventurersStart', undefined) : setup,
      }
    case 'combatant':
      return { map, setup: { ...setup, combatants: setup.combatants.filter((_, i) => i !== item.index) } }
  }
}

/** The squares the board covers (at least one tile's worth), to size the grid. */
/**
 * The squares the pieces cover: `width` and `height` count from square 0 (the right and bottom edges);
 * `x` and `y` are the left and top edges, beyond 0 when a board starts further (an encounter whose
 * diagrams later add tiles north or west of it).
 */
export function extentOf(board: { map: DraftMap; setup: DraftSetup }): {
  x: number
  y: number
  width: number
  height: number
} {
  const rects: Rect[] = [
    ...board.map.tiles.map((t) => ({ x: t.x, y: t.y, w: TILE, h: TILE })),
    ...board.map.tokens.map(tokenRect),
    ...board.map.zones.map((z) => z.rect),
    ...board.setup.combatants.flatMap((c) => (c.at ? [{ x: c.at.x, y: c.at.y, w: 1, h: 1 }] : [])),
  ]
  return {
    x: rects.length ? Math.min(...rects.map((r) => r.x)) : 0,
    y: rects.length ? Math.min(...rects.map((r) => r.y)) : 0,
    width: Math.max(TILE, ...rects.map((r) => r.x + r.w)),
    height: Math.max(TILE, ...rects.map((r) => r.y + r.h)),
  }
}

export const sameItem = (a: MapItem | null, b: MapItem | null): boolean =>
  !!a && !!b && a.kind === b.kind && ('id' in a ? a.id === (b as typeof a).id : a.index === (b as typeof a).index)

/** Sets a field, or removes it when undefined (the file stays free of empty keys). */
function withField<T extends object>(value: T, key: string, field: unknown): T {
  const copy = { ...value } as Record<string, unknown>
  if (field === undefined) delete copy[key]
  else copy[key] = field
  return copy as T
}
