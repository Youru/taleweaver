import type { BoardToken, CombatantState, EncounterState } from '@/features/games'
import type { BoardMap, FogArea, ScenarioNode } from '@/features/scenarios'
import { assetUrl } from '@/shared/api/assets'
import type { BoardPieces } from '../api/types'

/** Everything the table draws, in squares (x to the east, y to the south). */
export interface BoardLayout {
  width: number
  height: number
  tiles: TileShape[]
  zones: AreaShape[]
  elevation: AreaShape[]
  currents: CurrentShape[]
  tokens: TokenShape[]
  figures: FigureShape[]
  /** On the board but without a square: the GM places them (drag, or select then tap a square). */
  unplacedFigures: UnplacedFigure[]
  unplacedTokens: BoardToken[]
}

export interface TileShape {
  key: string
  ref: string
  label: string
  href?: string
  x: number
  y: number
  w: number
  h: number
  /** Degrees clockwise; 0 when unknown. */
  rotation: number
  /** Under the fog of war: drawn as fog, everything on it is hidden. */
  hidden?: FogArea
}

export interface AreaShape {
  key: string
  kind: string
  x: number
  y: number
  w: number
  h: number
  level?: number
}

export interface CurrentShape {
  key: string
  /** Centre of the arrow. */
  x: number
  y: number
  /** Degrees clockwise from east. */
  angle: number
  strength: number
}

export interface TokenShape {
  id: string
  key: string
  href?: string
  x: number
  y: number
  w: number
  h: number
  /** The long token images are drawn 2 wide × 1 high: a vertical one is turned a quarter. */
  turned: boolean
  /** Objectives, totems, exits, loot… are round tokens; terrain pieces are square. */
  round: boolean
  /** Catalogue type ("um1:token/terrain/obstructing-long"), when known. */
  type?: string
}

export type FigureKind = 'combatant' | 'adventurer'

export interface FigureShape {
  /** What the move command names: the combatant's instance, or "adventurer:<name>". */
  id: string
  kind: FigureKind
  /** Combatant instance, or adventurer name. */
  name: string
  card?: string
  label: string
  href?: string
  x: number
  y: number
  size: number
  acting: boolean
  aggro: boolean
}

export interface UnplacedFigure {
  id: string
  kind: FigureKind
  name: string
}

export const adventurerId = (name: string) => `adventurer:${name}`

const DIRECTIONS: Record<string, number> = { east: 0, south: 90, west: 180, north: 270 }

/** The maps on the table: the encounter's own, then the diagrams placed so far. */
export function mapsOnTable(node: ScenarioNode | undefined, encounter: EncounterState): BoardMap[] {
  const diagrams = (node?.diagrams ?? []).filter((d) => encounter.placedDiagrams.includes(d.id))
  return [node?.map, ...diagrams.map((d) => d.map)].filter((m): m is BoardMap => !!m)
}

const covers = (r: { x: number; y: number; w: number; h: number }, x: number, y: number) =>
  x >= r.x && x < r.x + r.w && y >= r.y && y < r.y + r.h

export function boardLayout(
  node: ScenarioNode | undefined,
  encounter: EncounterState,
  pieces: BoardPieces | undefined,
): BoardLayout {
  const maps = mapsOnTable(node, encounter)
  const areas = new Map(maps.flatMap((m) => m.areas ?? []).map((a) => [a.id, a]))
  const revealed = new Set(encounter.revealedAreas ?? [])

  const tiles = new Map<string, TileShape>()
  for (const tile of maps.flatMap((m) => m.tiles)) {
    const piece = pieces?.tiles[tile.ref]
    const key = `${tile.ref}@${tile.x},${tile.y}`
    const area = tile.area ? areas.get(tile.area) : undefined
    tiles.set(key, {
      key,
      ref: tile.ref,
      label: piece?.name ?? tile.ref.split('/').pop() ?? tile.ref,
      href: assetUrl(piece?.image),
      x: tile.x,
      y: tile.y,
      w: piece?.w ?? 6,
      h: piece?.h ?? 6,
      rotation: tile.rotation ?? 0,
      hidden: area && !revealed.has(area.id) ? area : undefined,
    })
  }
  const fog = [...tiles.values()].filter((t) => t.hidden)
  const underFog = (x: number, y: number) => fog.some((t) => covers(t, x, y))

  const zones = maps.flatMap((m) => m.zones).map((z) => ({ key: z.id, kind: z.kind ?? 'zone', ...z.rect }))
  const elevation = maps
    .flatMap((m) => m.elevation)
    .map((e, i) => ({ key: `elevation-${i}`, kind: 'elevation', level: e.level, ...e.rect }))
  const currents = maps
    .flatMap((m) => m.currents)
    .flatMap((current, i): CurrentShape[] => {
      const angle = DIRECTIONS[current.direction ?? '']
      const area = current.rect ?? [...tiles.values()].find((t) => t.ref === current.tile)
      if (angle === undefined || !area || underFog(area.x, area.y)) return []
      return [
        { key: `current-${i}`, x: area.x + area.w / 2, y: area.y + area.h / 2, angle, strength: current.strength },
      ]
    })

  const onBoard = encounter.tokens.filter((t) => !t.removed)
  const tokens = onBoard.flatMap((token): TokenShape[] => {
    if (!token.area || underFog(token.area.x, token.area.y)) return []
    const piece = token.type ? pieces?.tokens[token.type] : undefined
    const image = (token.color && piece?.colors?.[token.color]) || piece?.image
    return [
      {
        id: token.id,
        key: token.key,
        type: token.type ?? undefined,
        href: assetUrl(image),
        ...token.area,
        turned: token.area.h > token.area.w,
        round: !!piece && piece.category !== 'terrain',
      },
    ]
  })

  const alive = encounter.combatants.filter((c) => !c.defeated)
  const acting = encounter.activation?.figures[0]
  const figurePiece = (...families: (string | undefined)[]) =>
    families.map((f) => (f ? pieces?.figures[f] : undefined)).find((p) => !!p)

  const combatants = alive.flatMap((c): FigureShape[] => {
    if (!c.position || underFog(c.position.x, c.position.y)) return []
    const piece = figurePiece(c.figure, c.family)
    return [
      {
        id: c.instance,
        kind: 'combatant',
        name: c.instance,
        card: c.card,
        label: c.number > 0 ? String(c.number) : (c.instance[0] ?? '?').toUpperCase(),
        href: assetUrl(piece?.image),
        x: c.position.x,
        y: c.position.y,
        size: piece?.baseSize === '2x2' ? 2 : 1,
        acting: c.instance === acting,
        aggro: c.aggro,
      },
    ]
  })
  const adventurers = (encounter.adventurers ?? []).flatMap((a): FigureShape[] => {
    if (!a.position) return []
    const piece = figurePiece(a.name.toLowerCase())
    return [
      {
        id: adventurerId(a.name),
        kind: 'adventurer',
        name: a.name,
        label: (a.name[0] ?? '?').toUpperCase(),
        href: assetUrl(piece?.image),
        x: a.position.x,
        y: a.position.y,
        size: 1,
        acting: false,
        aggro: false,
      },
    ]
  })

  const unplacedFigures: UnplacedFigure[] = [
    ...(encounter.adventurers ?? [])
      .filter((a) => !a.position)
      .map((a) => ({ id: adventurerId(a.name), kind: 'adventurer' as const, name: a.name })),
    ...alive
      .filter((c: CombatantState) => !c.position)
      .map((c) => ({ id: c.instance, kind: 'combatant' as const, name: c.instance })),
  ]

  const figures = [...combatants, ...adventurers]
  const extents = [
    ...tiles.values(),
    ...zones,
    ...tokens,
    ...figures.map((f) => ({ x: f.x, y: f.y, w: f.size, h: f.size })),
  ]
  return {
    width: Math.max(1, ...extents.map((e) => e.x + e.w)),
    height: Math.max(1, ...extents.map((e) => e.y + e.h)),
    tiles: [...tiles.values()],
    zones: zones.filter((z) => !underFog(z.x, z.y)),
    elevation: elevation.filter((e) => !underFog(e.x, e.y)),
    currents,
    tokens,
    figures,
    unplacedFigures,
    unplacedTokens: onBoard.filter((t) => !t.area),
  }
}

/** What the GM selected on the table. */
export type Selection = { kind: 'figure'; id: string } | { kind: 'token'; id: string } | { kind: 'area'; id: string }
