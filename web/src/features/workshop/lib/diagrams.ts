import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftNode } from '../api/types'
import { mapOf, setupOf, TILE, tokenRect, type DraftMap, type DraftSetup } from './map'

/**
 * A diagram of an encounter (§3.7.1 of the data model): the tiles, tokens and figures a hidden text
 * lays during the fight ("Place according to Diagram 3"). It is printed on a page of its own, after
 * the steps, with a green header, as the books do.
 */
export interface DraftDiagram {
  id: string
  label?: LocalizedText
  /** The diagrams already on the table when this one is laid: printed faded under it. */
  builtOn?: string[]
  map?: Partial<DraftMap>
  setup?: Partial<DraftSetup>
  specialRules?: { id: string; name: LocalizedText; text: LocalizedText }[]
  [field: string]: unknown
}

export const diagramsOf = (node: DraftNode): DraftDiagram[] =>
  Array.isArray(node.diagrams) ? (node.diagrams as DraftDiagram[]) : []

/** Its board, with the lists the editor and the print expect. */
export function diagramBoard(diagram: DraftDiagram): { map: DraftMap; setup: DraftSetup } {
  const shape = { id: diagram.id, type: 'encounter', map: diagram.map, setup: diagram.setup } as DraftNode
  return { map: mapOf(shape), setup: setupOf(shape) }
}

/** The number of "Diagram 12" (or of its id), to print them in order; diagrams without one come last. */
export function diagramNumber(diagram: DraftDiagram): number {
  const words = [...Object.values(diagram.label ?? {}), diagram.id]
  for (const word of words) {
    const n = /(\d+)/.exec(String(word))?.[1]
    if (n) return Number(n)
  }
  return Number.MAX_SAFE_INTEGER
}

/** The maps of the diagrams it builds on, in the order given. */
export function underOf(node: DraftNode, diagram: DraftDiagram): DraftMap[] {
  const all = diagramsOf(node)
  return (diagram.builtOn ?? []).flatMap((id) => {
    const under = all.find((d) => d.id === id && d.id !== diagram.id)
    return under ? [diagramBoard(under).map] : []
  })
}

/** Every diagram of these encounters, in number order (Diagram 1, 2…), as the booklet prints them. */
export function bookletDiagrams(nodes: DraftNode[]): { node: DraftNode; diagram: DraftDiagram }[] {
  return nodes
    .filter((n) => n.type === 'encounter')
    .flatMap((node) => diagramsOf(node).map((diagram) => ({ node, diagram })))
    .sort((a, b) => diagramNumber(a.diagram) - diagramNumber(b.diagram))
}

/** A new diagram: the next free number. */
export function addDiagram(node: DraftNode, label: (n: number) => LocalizedText): DraftNode {
  const used = new Set(diagramsOf(node).map((d) => d.id))
  let n = 1
  while (used.has(`diagram-${n}`)) n++
  const diagram: DraftDiagram = { id: `diagram-${n}`, label: label(n), map: { tiles: [], tokens: [] } }
  return { ...node, diagrams: [...diagramsOf(node), diagram] }
}

export function updateDiagram(node: DraftNode, id: string, change: (d: DraftDiagram) => DraftDiagram): DraftNode {
  return { ...node, diagrams: diagramsOf(node).map((d) => (d.id === id ? change(d) : d)) }
}

/** Removes a diagram, and the mentions of it in the others' builtOn. */
export function removeDiagram(node: DraftNode, id: string): DraftNode {
  const diagrams = diagramsOf(node)
    .filter((d) => d.id !== id)
    .map((d) => {
      if (!d.builtOn?.includes(id)) return d
      const next: DraftDiagram = { ...d, builtOn: d.builtOn.filter((u) => u !== id) }
      if (!next.builtOn?.length) delete next.builtOn
      return next
    })
  if (diagrams.length) return { ...node, diagrams }
  const rest = { ...node }
  delete rest.diagrams
  return rest
}

/**
 * The diagram as a node the map editor can work on (its board as the node's map and setup), and the
 * diagram back from what the editor gives.
 */
export const diagramAsNode = (node: DraftNode, diagram: DraftDiagram): DraftNode =>
  ({ id: `${node.id}#${diagram.id}`, type: 'encounter', map: diagram.map, setup: diagram.setup }) as DraftNode

export function withDiagramBoard(diagram: DraftDiagram, edited: DraftNode): DraftDiagram {
  const next: DraftDiagram = { ...diagram, map: edited.map as Partial<DraftMap> }
  const setup = edited.setup as Partial<DraftSetup> | undefined
  if (setup && (setup.combatants?.length || Object.keys(setup).some((k) => k !== 'combatants'))) next.setup = setup
  else delete next.setup
  return next
}

type Box = { x: number; y: number; w: number; h: number }
const overlaps = (a: Box, b: Box) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h

/**
 * What a diagram's page shows of the board already on the table, as the books do: only the tiles its
 * new pieces lie on or touch (and the terrain laid off the tiles there, a passage), with what is on
 * them. The rest of the encounter is left out: a diagram is a small addition, not the whole board.
 */
export function diagramContext(board: DraftMap, diagram: { map: DraftMap; setup: DraftSetup }): DraftMap {
  // The new pieces, each grown by one square along x, then along y: what they touch by an edge
  // (a tile met only at a corner is left out)
  const near: Box[] = [
    ...diagram.map.tiles.map((t) => ({ x: t.x, y: t.y, w: TILE, h: TILE })),
    ...diagram.map.tokens.map(tokenRect),
    ...diagram.setup.combatants.flatMap((c) => (c.at ? [{ x: c.at.x, y: c.at.y, w: 1, h: 1 }] : [])),
  ].flatMap((r) => [
    { x: r.x - 1, y: r.y, w: r.w + 2, h: r.h },
    { x: r.x, y: r.y - 1, w: r.w, h: r.h + 2 },
  ])
  const touched = (r: Box) => near.some((n) => overlaps(n, r))
  const tileBox = (t: { x: number; y: number }) => ({ x: t.x, y: t.y, w: TILE, h: TILE })
  const tiles = board.tiles.filter((t) => touched(tileBox(t)))
  const kept = tiles.map(tileBox)
  const onBoard = (r: Box) => board.tiles.some((t) => overlaps(tileBox(t), r))
  return {
    ...board,
    tiles,
    // On a tile kept, or off the tiles and touched (a passage the diagram builds from)
    tokens: board.tokens.filter((k) => {
      const r = tokenRect(k)
      return kept.some((t) => overlaps(t, r)) || (!onBoard(r) && touched(r))
    }),
    zones: board.zones.filter((z) => kept.some((t) => overlaps(t, z.rect))),
  }
}
