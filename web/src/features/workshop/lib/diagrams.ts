import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftNode } from '../api/types'
import { mapOf, setupOf, type DraftMap, type DraftSetup } from './map'

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
