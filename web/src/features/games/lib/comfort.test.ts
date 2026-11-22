import { describe, expect, it } from 'vitest'
import type { ScenarioNode } from '@/features/scenarios'
import type { EncounterState, GameState } from '../api/types'
import {
  actionOfLine,
  consumablesAtRisk,
  encountersAhead,
  materialOf,
  proposals,
  remindersFor,
  speeches,
  urgencyComing,
  waitingFor,
} from './comfort'

const encounter = (more: object = {}) =>
  ({
    node: 'fight',
    round: 1,
    urgency: 0,
    ended: false,
    initiativeTrack: ['adventurer:Rook', 'x'],
    playedCards: ['adventurer:Rook', 'x'],
    currentTurn: 1,
    combatants: [],
    tokens: [],
    adventurers: [{ name: 'Rook' }],
    activeEndConditions: ['win'],
    ...more,
  }) as unknown as EncounterState
const node = (more: object = {}) =>
  ({ id: 'fight', type: 'encounter', endConditions: [], tokens: {}, ...more }) as unknown as ScenarioNode
const rule = (id: string, en: string) => ({ id, name: { en: id }, text: { en } })

describe('the journal gone back to (M2)', () => {
  it('finds the action that wrote a line', () => {
    expect(actionOfLine([2, 5, 5, 9], 1)).toBeUndefined()
    expect(actionOfLine([2, 5, 5, 9], 2)).toBe(0)
    expect(actionOfLine([2, 5, 5, 9], 6)).toBe(2)
  })
  it('stops at what cannot be gone back on', () => {
    expect(actionOfLine([-1, -1, 7], 3)).toBeUndefined()
  })
})

describe('Urgency announced (M11)', () => {
  it('is announced on the last card of a quiet round only', () => {
    expect(urgencyComing(encounter())).toBe(true)
    expect(urgencyComing(encounter({ playedCards: ['x'] }))).toBe(false)
    expect(urgencyComing(encounter({ roundLog: { defeated: 1 } }))).toBe(false)
    expect(urgencyComing(encounter({ roundLog: { waived: true } }))).toBe(false)
    expect(urgencyComing(encounter(), { removeUrgency: true })).toBe(false)
  })
})

describe('reminders (M10)', () => {
  const read = node({
    specialRules: [rule('start', 'At the start of each round, flip it.'), rule('end', 'At the end of each Round, burn.')],
  })
  it('shows each rule at its moment', () => {
    const ids = (e: EncounterState) => remindersFor(e, read).map((r) => r.id)
    expect(ids(encounter({ round: 0, playedCards: [] }))).toEqual(['start', 'end'])
    expect(ids(encounter({ playedCards: ['adventurer:Rook'], currentTurn: 0 }))).toEqual(['start'])
    expect(ids(encounter())).toEqual(['end'])
  })
})

describe('proposals (M12)', () => {
  const win = (en: string) => node({ endConditions: [{ id: 'win', kind: 'win', text: { en } }] })
  const imp = (defeated: boolean) => ({ instance: 'imp-1', family: 'imp', defeated })
  it('proposes the family defeated', () => {
    expect(proposals(encounter({ combatants: [imp(true)] }), win('Defeat the Imp.')).map((p) => p.id)).toEqual(['win'])
    expect(proposals(encounter({ combatants: [imp(false)] }), win('Defeat the Imp.'))).toEqual([])
  })
  it('waits for those under the fog', () => {
    const fog = encounter({ combatants: [imp(true)], waiting: [{ area: 'a' }] })
    expect(proposals(fog, win('Defeat all opponents.'))).toEqual([])
  })
})

describe('before and around an encounter', () => {
  it('lists the Consumables that Explore discards (M18)', () => {
    const campaign = {
      stash: [{ id: 'a', item: 'potion' }],
      unassigned: [{ id: 'b', type: 'consumable' }],
      party: [{ name: 'Rook', pack: [{ id: 'c', item: 'sword' }], equipped: [{ id: 'd', item: 'potion' }] }],
    } as unknown as GameState['campaign']
    const type = (id: string) => (id === 'potion' ? 'consumable' : 'weapon')
    expect(consumablesAtRisk(campaign, type).map((c) => c.owned.id)).toEqual(['a', 'b'])
  })

  it('finds the next encounters, through the scenes (M5)', () => {
    const nodes = [
      node({ id: 'a', type: 'scene', next: 'b' }),
      node({ id: 'b', type: 'scene', mechanics: [{ do: 'goToNode', node: 'c' }], next: '@end' }),
      node({ id: 'c', endConditions: [{ id: 'win', next: 'd' }] }),
      node({ id: 'd' }),
    ]
    expect(encountersAhead(nodes, 'a').map((n) => n.id)).toEqual(['c'])
    expect(encountersAhead(nodes, 'c').map((n) => n.id)).toEqual(['d'])
  })

  it('counts the pieces to take out (M8)', () => {
    const board = node({ map: { tiles: [{ ref: 'um1:tile/4a' }, { ref: 'um1:tile/4a' }] } })
    const lists = materialOf(board, encounter({ tokens: [{ key: 'totem:red', removed: false }] }))
    expect(lists.tiles).toEqual([{ id: 'um1:tile/4a', count: 2 }])
    expect(lists.tokens).toEqual([{ id: 'totem:red', count: 1 }])
  })
})

describe('who is waited for (M15)', () => {
  it('follows the question, else the turn', () => {
    const at = (more: object) =>
      waitingFor({ encounter: encounter({ currentTurn: 0 }), ...more } as unknown as GameState)
    expect(at({})).toBe('Rook')
    expect(at({ prompt: { kind: 'choice', decider: 'Iris' } })).toBe('Iris')
    expect(at({ prompt: { kind: 'continue' } })).toBeNull()
  })
})

describe('reading aloud (M19)', () => {
  it('gives a speech to who says it, and to nobody in doubt', () => {
    expect(speeches('“Run!” said Iris.', ['Rook'])).toEqual([
      { text: '“Run!”', speech: true, who: 'Iris' },
      { text: ' said Iris.', speech: false },
    ])
    expect(speeches('"Why?" Rook and Iris wonder.', ['Rook', 'Iris'])[0]).toEqual({ text: '"Why?"', speech: true })
  })
})
