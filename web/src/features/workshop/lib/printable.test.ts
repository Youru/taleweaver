import { describe, expect, it } from 'vitest'
import type { BoardPieces } from '@/features/table'
import type { DraftScenario } from '../api/types'
import { encounterSheet, readingOrder, setupColumns, terrainFrame, terrainName } from './printable'

const pieces: BoardPieces = {
  tiles: { 'um1:tile/um1': { name: 'UM1', w: 6, h: 6 } },
  tokens: {
    'um1:token/totem': { name: { en: 'Totem' }, category: 'totem', colors: { blue: 'b.png', red: 'r.png' } },
    'um1:token/terrain/obstructing': { name: { en: 'Obstructing' }, category: 'terrain' },
  },
  figures: {},
  families: {
    fragor: { name: { en: 'Fragor' }, kind: 'command' },
    'blighted-terror': { name: { en: 'Blighted Terror' }, kind: 'intelligent' },
  },
}

const scenario: DraftScenario = {
  id: 'homebrew:scenario/test',
  title: { en: 'Test' },
  packs: ['um1'],
  start: 'intro',
  nodes: [
    {
      id: 'fight',
      type: 'encounter',
      name: { en: 'Ambush' },
      endConditions: [
        { id: 'win', kind: 'win', text: { en: 'All dead' }, reward: { en: '10 gold' }, next: 'after' },
        { id: 'late', kind: 'win', active: false, text: { en: 'Sealed' } },
      ],
      hiddenRules: [{ id: 'cracks', name: { en: 'Cracks' }, text: { en: 'The seal cracks.' } }],
      map: {
        tiles: [{ ref: 'um1:tile/um1', x: 0, y: 0 }],
        tokens: [
          { id: 't1', type: 'um1:token/totem', color: 'blue', x: 1, y: 1 },
          { id: 't2', type: 'um1:token/totem', color: 'red', x: 2, y: 1 },
          { id: 'o1', type: 'um1:token/terrain/obstructing', x: 3, y: 3 },
          { id: 'o2', type: 'um1:token/terrain/obstructing', x: 4, y: 3 },
        ],
      },
      setup: {
        combatants: [{ family: 'fragor', at: { x: 5, y: 5 } }, { ref: 'um1:combatant/blighted-terror-common' }],
      },
      tokens: { 'totem:blue': { activation: { en: 'In line of sight.' }, events: ['ev'] } },
      events: [
        { id: 'ev', trigger: { on: 'tokenActivated', token: 'totem:blue' }, hiddenText: { en: 'A Fragor appears.' } },
      ],
    },
    { id: 'intro', type: 'scene', title: { en: 'Start' }, next: 'fight' },
    { id: 'after', type: 'scene', title: { en: 'After' } },
  ],
}

describe('the printed encounter', () => {
  const sheet = encounterSheet(scenario, scenario.nodes[0]!, pieces, 'en', (c) => c)

  it('lists the setup as the books do', () => {
    expect(sheet.setup).toEqual([
      { group: 'tiles', items: ['UM1'] },
      { group: 'terrain', items: ['2 × Obstructing (1x1)'] },
      { group: 'totem', items: ['1 × Blue', '1 × Red'] },
      { group: 'figures', items: ['1 × Fragor', '1 × Blighted Terror'] },
    ])
  })

  it('gives the end conditions their reward and where they lead, the later wins as alternate, and prints the hidden rules', () => {
    expect(sheet.ends).toEqual([
      { kind: 'win', text: 'All dead', reward: '10 gold', next: 'After' },
      { kind: 'win', alternate: true, text: 'Sealed', reward: '', next: undefined, later: true },
    ])
    expect(sheet.hiddenRules).toEqual([{ name: 'Cracks', text: 'The seal cracks.' }])
  })

  it('prints the hidden text of the tokens that have one, with the picture of their colour', () => {
    expect(sheet.hidden).toEqual([
      {
        group: 'totem',
        entries: [
          {
            key: 'totem:blue',
            label: 'Blue Totem',
            image: 'b.png',
            activation: 'In line of sight.',
            text: ['A Fragor appears.'],
          },
        ],
      },
    ])
  })
})

it('reads the start first, then the steps in the order of the file', () => {
  expect(readingOrder(scenario).map((n) => n.id)).toEqual(['intro', 'fight', 'after'])
})

it('names the terrain as the books do, without the size of its name', () => {
  expect(terrainName('Obstructing 1X2')).toBe('Obstructing')
  expect(terrainName('Terrain Light Water 2X2')).toBe('Water')
  expect(terrainName('Light Water 1X1')).toBe('Water')
  expect(terrainName('Cave Entrance')).toBe('Cave Entrance')
})

it('lists the terrain by kind, the largest first, and the tiles in number order', () => {
  const node = {
    id: 'big',
    type: 'encounter',
    map: {
      tiles: [
        { ref: 'um1:tile/um31', x: 0, y: 0 },
        { ref: 'um1:tile/um5', x: 6, y: 0 },
      ],
      tokens: [
        { id: 'a', type: 'obstructing', x: 0, y: 0 },
        { id: 'b', type: 'obstructing-long', x: 1, y: 0 },
        { id: 'c', type: 'water', x: 2, y: 0 },
        { id: 'd', type: 'obstructing-long', x: 3, y: 0 },
      ],
    },
  }
  const board: BoardPieces = {
    ...pieces,
    tiles: { 'um1:tile/um31': { name: 'UM31', w: 6, h: 6 }, 'um1:tile/um5': { name: 'UM5', w: 6, h: 6 } },
    tokens: {
      obstructing: { name: { en: 'Obstructing 1X1' }, category: 'terrain', size: '1x1' },
      'obstructing-long': { name: { en: 'Obstructing 1X2' }, category: 'terrain', size: '2x1' },
      water: { name: { en: 'Light Water 1X1' }, category: 'terrain', size: '1x1' },
    },
  }
  const setup = encounterSheet({ ...scenario, nodes: [node] }, node, board, 'en', (c) => c).setup

  expect(setup).toEqual([
    { group: 'tiles', items: ['UM5, UM31'] },
    { group: 'terrain', items: ['2 × Obstructing (2x1)', '1 × Obstructing (1x1)', '1 × Water (1x1)'] },
  ])
})

it('spreads the setup over three columns, a long group going on in the next one', () => {
  const terrain = Array.from({ length: 11 }, (_, i) => `${i + 1} × Terrain`)
  const columns = setupColumns([
    { group: 'tiles', items: ['UM31, UM33, UM35, UM37, UM39, UM41, UM43, UM51, UM53, UM55'] },
    { group: 'terrain', items: terrain },
  ])

  expect(columns).toHaveLength(3)
  expect(columns[0]).toEqual([{ group: 'tiles', continued: false, items: [expect.any(String)] }])
  expect(columns[1]![0]).toMatchObject({ group: 'terrain', continued: false })
  expect(columns[2]![0]).toMatchObject({ group: 'terrain', continued: true })
  expect(columns.flat().flatMap((c) => (c.group === 'terrain' ? c.items : []))).toEqual(terrain)
})

it('keeps the groups whole when they fit three columns', () => {
  const columns = setupColumns([
    { group: 'totem', items: ['1 × Green', '1 × Blue', '1 × Purple', '1 × Red', '1 × Yellow', '1 × Orange'] },
    { group: 'objective', items: ['1 × Red', '1 × Blue'] },
    { group: 'loot', items: ['1 × Yellow', '1 × Red', '1 × Green'] },
  ])

  expect(columns.map((c) => c.map((cell) => cell.group))).toEqual([['totem'], ['objective'], ['loot']])
})

it('frames each terrain in the colour of its kind, as the books draw it', () => {
  expect(terrainFrame('um1:token/terrain/obstructing-long')).toBe(terrainFrame('be1:token/terrain/obstructing-3x1'))
  expect(terrainFrame('um1:token/terrain/dangerous')).not.toBe(terrainFrame('be1:token/terrain/bug-creep'))
  expect(terrainFrame('um1:token/terrain/water-long-dark')).not.toBe(terrainFrame('um1:token/terrain/water'))
  expect(terrainFrame('um1:token/terrain/water-large-light')).toBe(terrainFrame('um1:token/terrain/water'))
  expect(terrainFrame('um1:token/totem')).toBeUndefined()
})
