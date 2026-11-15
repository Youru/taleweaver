import type { BoardToken, CombatantState, EncounterState } from '@/features/games'
import type { BoardMap, ScenarioNode } from '@/features/scenarios'
import { assetUrl } from '@/shared/api/assets'
import type { BoardPieces } from '../api/types'

/** Everything the map view draws, in squares (x to the east, y to the south). */
export interface BoardLayout {
  width: number
  height: number
  tiles: TileShape[]
  zones: AreaShape[]
  elevation: AreaShape[]
  currents: CurrentShape[]
  tokens: TokenShape[]
  figures: FigureShape[]
  /** On the board but without a square given by the scenario: the GM places them. */
  unplacedFigures: CombatantState[]
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
}

export interface FigureShape {
  instance: string
  card: string
  label: string
  href?: string
  x: number
  y: number
  size: number
  acting: boolean
  aggro: boolean
  damaged: boolean
}

const DIRECTIONS: Record<string, number> = { east: 0, south: 90, west: 180, north: 270 }

/** The maps on the table: the encounter's own, then the diagrams placed so far. */
export function mapsOnTable(node: ScenarioNode | undefined, encounter: EncounterState): BoardMap[] {
  const diagrams = (node?.diagrams ?? []).filter((d) => encounter.placedDiagrams.includes(d.id))
  return [node?.map, ...diagrams.map((d) => d.map)].filter((m): m is BoardMap => !!m)
}

export function boardLayout(
  node: ScenarioNode | undefined,
  encounter: EncounterState,
  pieces: BoardPieces | undefined,
): BoardLayout {
  const maps = mapsOnTable(node, encounter)
  const tiles = new Map<string, TileShape>()
  for (const tile of maps.flatMap((m) => m.tiles)) {
    const piece = pieces?.tiles[tile.ref]
    const key = `${tile.ref}@${tile.x},${tile.y}`
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
    })
  }

  const zones = maps.flatMap((m) => m.zones).map((z) => ({ key: z.id, kind: z.kind ?? 'zone', ...z.rect }))
  const elevation = maps
    .flatMap((m) => m.elevation)
    .map((e, i) => ({ key: `elevation-${i}`, kind: 'elevation', level: e.level, ...e.rect }))
  const currents = maps
    .flatMap((m) => m.currents)
    .flatMap((current, i): CurrentShape[] => {
      const angle = DIRECTIONS[current.direction ?? '']
      const area = current.rect ?? [...tiles.values()].find((t) => t.ref === current.tile)
      if (angle === undefined || !area) return []
      return [
        { key: `current-${i}`, x: area.x + area.w / 2, y: area.y + area.h / 2, angle, strength: current.strength },
      ]
    })

  const onBoard = encounter.tokens.filter((t) => !t.removed)
  const tokens = onBoard.flatMap((token): TokenShape[] => {
    if (!token.area) return []
    const piece = token.type ? pieces?.tokens[token.type] : undefined
    const image = (token.color && piece?.colors?.[token.color]) || piece?.image
    return [
      {
        id: token.id,
        key: token.key,
        href: assetUrl(image),
        ...token.area,
        turned: token.area.h > token.area.w,
        round: !!piece && piece.category !== 'terrain',
      },
    ]
  })

  const alive = encounter.combatants.filter((c) => !c.defeated)
  const acting = encounter.activation?.figures[0]
  const figures = alive.flatMap((c): FigureShape[] => {
    if (!c.position) return []
    const piece = (c.figure && pieces?.figures[c.figure]) || pieces?.figures[c.family]
    return [
      {
        instance: c.instance,
        card: c.card,
        label: c.number > 0 ? String(c.number) : (c.instance[0] ?? '?').toUpperCase(),
        href: assetUrl(piece?.image),
        x: c.position.x,
        y: c.position.y,
        size: piece?.baseSize === '2x2' ? 2 : 1,
        acting: c.instance === acting,
        aggro: c.aggro,
        damaged: c.damage > 0,
      },
    ]
  })

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
    zones,
    elevation,
    currents,
    tokens,
    figures,
    unplacedFigures: alive.filter((c) => !c.position),
    unplacedTokens: onBoard.filter((t) => !t.area),
  }
}
