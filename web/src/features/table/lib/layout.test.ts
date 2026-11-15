import { describe, expect, it } from 'vitest'
import type { EncounterState } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import type { BoardPieces } from '../api/types'
import { boardLayout } from './layout'

const pieces: BoardPieces = {
  families: {},
  tiles: {
    'um1:tile/um47': { name: 'UM47', w: 6, h: 6, image: 'assets/act1/tiles/um47.jpg' },
    'um1:tile/um15': { name: 'UM15', w: 6, h: 6, image: 'assets/act1/tiles/um15.jpg' },
  },
  tokens: {
    'um1:token/totem': {
      name: { en: 'Totem' },
      category: 'totem',
      image: 'assets/act1/tokens/totem-back.jpg',
      colors: { blue: 'assets/act1/tokens/totem-blue.jpg' },
    },
    'um1:token/terrain/obstructing-long': {
      name: { en: 'Obstructing 1X2' },
      category: 'terrain',
      image: 'assets/act1/tokens/obstructing-1x2.jpg',
    },
  },
  figures: {
    fragor: { image: 'assets/act1/standees/fragor-lowres.png', baseSize: '1x1' },
    'blighted-guardian': { image: 'assets/act1/standees/male-blighted-guardian-lowres.png', baseSize: '2x2' },
  },
}

const emptyMap = { tiles: [], tokens: [], zones: [], currents: [], elevation: [], areas: [] }

const node: ScenarioNode = {
  id: 'lair',
  type: 'encounter',
  tokens: { 'totem:blue': { events: [] } },
  endConditions: [],
  map: {
    ...emptyMap,
    tiles: [{ ref: 'um1:tile/um47', x: 0, y: 0, rotation: 270 }],
    zones: [{ id: 'start', kind: 'startingZone', rect: { x: 5, y: 1, w: 1, h: 4 } }],
    currents: [{ tile: 'um1:tile/um47', direction: 'north', strength: 1 }],
  },
  diagrams: [{ id: 'diagram-a', map: { ...emptyMap, tiles: [{ ref: 'um1:tile/um15', x: 6, y: 0, rotation: null }] } }],
}

function encounter(overrides: Partial<EncounterState> = {}): EncounterState {
  return {
    node: 'lair',
    round: 1,
    urgency: 0,
    ended: false,
    initiativeTrack: [],
    currentTurn: 0,
    combatants: [],
    tokens: [],
    placedDiagrams: [],
    revealedAreas: [],
    adventurers: [],
    activeEndConditions: [],
    ...overrides,
  }
}

const combatant = {
  card: 'um1:combatant/fragor-1-3-common',
  family: 'fragor',
  initiativeCards: [],
  damage: 0,
  effects: [],
  aggro: false,
  defeated: false,
}

describe('boardLayout', () => {
  it('draws the encounter tiles with their image and rotation, sized to the board', () => {
    const layout = boardLayout(node, encounter(), pieces)

    expect(layout.tiles).toEqual([
      expect.objectContaining({ label: 'UM47', href: '/api/assets/act1/tiles/um47.jpg', rotation: 270, w: 6 }),
    ])
    expect([layout.width, layout.height]).toEqual([6, 6])
  })

  it('adds the tiles of the diagrams placed so far', () => {
    const layout = boardLayout(node, encounter({ placedDiagrams: ['diagram-a'] }), pieces)

    expect(layout.tiles.map((t) => t.label)).toEqual(['UM47', 'UM15'])
    expect(layout.tiles[1]?.rotation).toBe(0)
    expect(layout.width).toBe(12)
  })

  it('points a current of a tile from the centre of that tile', () => {
    const layout = boardLayout(node, encounter(), pieces)

    expect(layout.currents).toEqual([{ key: 'current-0', x: 3, y: 3, angle: 270, strength: 1 }])
    expect(layout.zones[0]).toMatchObject({ kind: 'startingZone', x: 5, w: 1, h: 4 })
  })

  it('uses the colour image of a token, turns a vertical long token, and hides removed ones', () => {
    const layout = boardLayout(
      node,
      encounter({
        tokens: [
          {
            id: 'totem-blue',
            key: 'totem:blue',
            type: 'um1:token/totem',
            color: 'blue',
            area: { x: 1, y: 3, w: 1, h: 1 },
            removed: false,
          },
          {
            id: 'obs',
            key: 'obstructing-long',
            type: 'um1:token/terrain/obstructing-long',
            area: { x: 2, y: 2, w: 1, h: 2 },
            removed: false,
          },
          {
            id: 'gone',
            key: 'totem:red',
            type: 'um1:token/totem',
            color: 'red',
            area: { x: 4, y: 4, w: 1, h: 1 },
            removed: true,
          },
          { id: 'placed-9', key: 'exit:blue', type: 'um1:token/exit', color: 'blue', removed: false },
        ],
      }),
      pieces,
    )

    expect(layout.tokens.map((t) => [t.id, t.href, t.turned, t.round])).toEqual([
      ['totem-blue', '/api/assets/act1/tokens/totem-blue.jpg', false, true],
      ['obs', '/api/assets/act1/tokens/obstructing-1x2.jpg', true, false],
    ])
    expect(layout.unplacedTokens.map((t) => t.key)).toEqual(['exit:blue'])
  })

  it('places the living figures with their number, the figure they are played with, and lists the others', () => {
    const layout = boardLayout(
      node,
      encounter({
        combatants: [
          { ...combatant, instance: 'fragor-1', number: 1, position: { x: 2, y: 1 } },
          { ...combatant, instance: 'fragor-2', number: 2, position: { x: 3, y: 1 }, defeated: true },
          { ...combatant, instance: 'fragor-3', number: 3 },
          {
            ...combatant,
            instance: 'terror',
            number: 0,
            family: 'blighted-terror',
            figure: 'blighted-guardian',
            position: { x: 4, y: 4 },
          },
        ],
        activation: { initiativeCard: 'x', figures: ['fragor-1'], step: 0 },
      }),
      pieces,
    )

    expect(layout.figures.map((f) => [f.id, f.label, f.size, f.acting])).toEqual([
      ['fragor-1', '1', 1, true],
      ['terror', 'T', 2, false],
    ])
    expect(layout.figures[1]?.href).toBe('/api/assets/act1/standees/male-blighted-guardian-lowres.png')
    expect(layout.unplacedFigures.map((c) => c.id)).toEqual(['fragor-3'])
  })

  it('draws the adventurers, and lists those without a square first', () => {
    const layout = boardLayout(
      node,
      encounter({ adventurers: [{ name: 'Nightingale', position: { x: 5, y: 1 } }, { name: 'Rook' }] }),
      {
        ...pieces,
        figures: { ...pieces.figures, nightingale: { image: 'assets/act1/standees/nightingale-lowres.png' } },
      },
    )

    expect(layout.figures).toEqual([
      expect.objectContaining({
        id: 'adventurer:Nightingale',
        kind: 'adventurer',
        label: 'N',
        href: '/api/assets/act1/standees/nightingale-lowres.png',
      }),
    ])
    expect(layout.unplacedFigures).toEqual([{ id: 'adventurer:Rook', kind: 'adventurer', name: 'Rook' }])
  })

  it('hides the tiles of an area under the fog, and what stands on them, until it is revealed', () => {
    const foggy: ScenarioNode = {
      ...node,
      map: {
        ...emptyMap,
        tiles: [
          { ref: 'um1:tile/um47', x: 0, y: 0, rotation: 0 },
          { ref: 'um1:tile/um15', x: 6, y: 0, rotation: 0, area: 'crypt' },
        ],
        areas: [{ id: 'crypt', revealedAtStart: false, name: { en: 'The Crypt' } }],
      },
    }
    const state = encounter({
      combatants: [
        { ...combatant, instance: 'fragor-1', number: 1, position: { x: 2, y: 1 } },
        { ...combatant, instance: 'fragor-2', number: 2, position: { x: 8, y: 1 } },
      ],
    })

    const hidden = boardLayout(foggy, state, pieces)
    const revealed = boardLayout(foggy, { ...state, revealedAreas: ['crypt'] }, pieces)

    expect(hidden.tiles.map((t) => t.hidden?.id)).toEqual([undefined, 'crypt'])
    expect(hidden.figures.map((f) => f.id)).toEqual(['fragor-1'])
    expect(hidden.unplacedFigures).toEqual([])
    expect(revealed.tiles.every((t) => !t.hidden)).toBe(true)
    expect(revealed.figures.map((f) => f.id)).toEqual(['fragor-1', 'fragor-2'])
  })
})
