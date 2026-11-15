import { describe, expect, it } from 'vitest'
import type { BoardPieces } from '@/features/table'
import type { DraftScenario } from '../api/types'
import { encounterSheet, readingOrder } from './printable'

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
      { group: 'terrain', items: ['2 × Obstructing'] },
      { group: 'totem', items: ['1 × Blue', '1 × Red'] },
      { group: 'figures', items: ['1 × Fragor', '1 × Blighted Terror'] },
    ])
  })

  it('gives the end conditions their reward and where they lead, and prints the hidden rules', () => {
    expect(sheet.ends).toEqual([
      { kind: 'win', text: 'All dead', reward: '10 gold', next: 'After' },
      { kind: 'win', text: 'Sealed', reward: '', next: undefined, later: true },
    ])
    expect(sheet.hiddenRules).toEqual([{ name: 'Cracks', text: 'The seal cracks.' }])
  })

  it('prints the hidden text of the tokens that have one', () => {
    expect(sheet.hidden).toEqual([
      {
        group: 'totem',
        entries: [
          { key: 'totem:blue', label: 'Blue Totem', activation: 'In line of sight.', text: ['A Fragor appears.'] },
        ],
      },
    ])
  })
})

it('reads the steps from the start, following the exits', () => {
  expect(readingOrder(scenario).map((n) => n.id)).toEqual(['intro', 'fight', 'after'])
})
