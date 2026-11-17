import { describe, expect, it } from 'vitest'
import type { DraftNode } from '../api/types'
import {
  addDiagram,
  bookletDiagrams,
  diagramAsNode,
  diagramContext,
  diagramNumber,
  diagramsOf,
  removeDiagram,
  underOf,
  withDiagramBoard,
} from './diagrams'

const tile = (ref: string, x: number) => ({ ref, x, y: 0 })
const encounter = (id: string, diagrams?: unknown[]): DraftNode => ({
  id,
  type: 'encounter',
  map: { tiles: [tile('um1:tile/um1', 0)], tokens: [] },
  ...(diagrams ? { diagrams } : {}),
})

describe('diagrams', () => {
  it('adds them under the next free number and removes them with their mentions', () => {
    const label = (n: number) => ({ fr: `Schéma ${n}` })
    let node = addDiagram(encounter('fight'), label)
    node = addDiagram(node, label)
    expect(diagramsOf(node).map((d) => [d.id, d.label])).toEqual([
      ['diagram-1', { fr: 'Schéma 1' }],
      ['diagram-2', { fr: 'Schéma 2' }],
    ])
    node = {
      ...node,
      diagrams: diagramsOf(node).map((d) => (d.id === 'diagram-2' ? { ...d, builtOn: ['diagram-1'] } : d)),
    }
    const left = removeDiagram(node, 'diagram-1')
    expect(diagramsOf(left)).toEqual([{ id: 'diagram-2', label: { fr: 'Schéma 2' }, map: { tiles: [], tokens: [] } }])
    expect(removeDiagram(left, 'diagram-2')).not.toHaveProperty('diagrams')
  })

  it('prints them in number order after the steps, each over the diagrams it builds on', () => {
    const crack = encounter('crack', [{ id: 'diagram-4' }, { id: 'diagram-1', label: { en: 'Diagram 1' } }])
    const darkway = encounter('darkway', [
      { id: 'diagram-2', map: { tiles: [tile('um1:tile/um5', 6)] } },
      { id: 'diagram-12', builtOn: ['diagram-2', 'diagram-99'] },
      { id: 'extra' },
    ])
    const order = bookletDiagrams([crack, { id: 'intro', type: 'scene' }, darkway]).map(
      (d) => `${d.node.id}/${d.diagram.id}`,
    )
    expect(order).toEqual([
      'crack/diagram-1',
      'darkway/diagram-2',
      'crack/diagram-4',
      'darkway/diagram-12',
      'darkway/extra',
    ])
    expect(diagramNumber({ id: 'x', label: { fr: 'Schéma 7' } })).toBe(7)
    const d12 = diagramsOf(darkway)[1]!
    expect(underOf(darkway, d12).map((m) => m.tiles.map((t) => t.ref))).toEqual([['um1:tile/um5']])
  })

  it('is edited by the map editor as a node, and keeps its other fields', () => {
    const diagram = { id: 'diagram-3', label: { en: 'Diagram 3' }, specialRules: [], map: { tiles: [] } }
    const node = diagramAsNode(encounter('fight'), diagram)
    expect(node.id).toBe('fight#diagram-3')
    const edited = { ...node, map: { tiles: [tile('um1:tile/um3', 0)], tokens: [] }, setup: { combatants: [] } }
    expect(withDiagramBoard(diagram, edited)).toEqual({
      id: 'diagram-3',
      label: { en: 'Diagram 3' },
      specialRules: [],
      map: { tiles: [tile('um1:tile/um3', 0)], tokens: [] },
    })
    const withFigure = { ...edited, setup: { combatants: [{ family: 'fragor', at: { x: 1, y: 1 } }] } }
    expect(withDiagramBoard(diagram, withFigure).setup).toEqual(withFigure.setup)
  })

  it('keeps of the board only the tiles its new pieces lie on or touch by an edge', () => {
    const board = {
      tiles: [
        tile('um1:tile/um21', 0),
        tile('um1:tile/um19', 6),
        { ref: 'um1:tile/um13', x: 0, y: 6 },
        { ref: 'um1:tile/um51', x: 12, y: 6 },
      ],
      tokens: [
        { id: 'totem', type: 'um1:token/totem', x: 1, y: 1 },
        { id: 'far', type: 'um1:token/totem', x: 13, y: 7 },
        { id: 'passage', type: 'be1:token/terrain/passage', x: 12, y: 2 },
      ],
      zones: [{ id: 'start', kind: 'startingZone', rect: { x: 0, y: 6, w: 1, h: 1 } }],
      areas: [],
    }
    // A tile laid at (6, 6): under UM19, east of UM13, corner to corner with UM21, west of UM51
    const added = {
      map: { tiles: [{ ref: 'um1:tile/um5', x: 6, y: 6 }], tokens: [], zones: [], areas: [] },
      setup: { combatants: [] },
    }
    const shown = diagramContext(board, added)
    expect(shown.tiles.map((t) => t.ref)).toEqual(['um1:tile/um19', 'um1:tile/um13', 'um1:tile/um51'])
    expect(shown.tokens.map((k) => k.id)).toEqual(['far'])
    expect(shown.zones.map((z) => z.id)).toEqual(['start'])
    // A figure next to the passage laid off the tiles brings it, and only it
    const figure = {
      map: { tiles: [], tokens: [], zones: [], areas: [] },
      setup: { combatants: [{ family: 'fragor', at: { x: 13, y: 2 } }] },
    }
    expect(diagramContext(board, figure)).toMatchObject({ tiles: [], tokens: [{ id: 'passage' }], zones: [] })
  })
})
