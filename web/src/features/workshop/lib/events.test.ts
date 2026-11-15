import { describe, expect, it } from 'vitest'
import type { DraftNode } from '../api/types'
import {
  activatableKeys,
  addEvent,
  addTokenEvent,
  atOf,
  eventsOf,
  eventsOfToken,
  removeEvent,
  spotOf,
  tokenRulesOf,
} from './events'

const node = (): DraftNode => ({
  id: 'fight',
  type: 'encounter',
  map: {
    tiles: [],
    tokens: [
      { id: 'o1', type: 'um1:token/objective', color: 'blue', x: 1, y: 1 },
      { id: 'w1', type: 'um1:token/terrain/obstructing', x: 2, y: 2 },
    ],
  },
  tokens: { 'loot:red': { events: ['old'] } },
  events: [{ id: 'old', trigger: { on: 'tokenActivated', token: 'loot:red' }, actions: [] }],
})
const category = (type: string) => (type.includes('/terrain/') ? 'terrain' : 'objective')

describe('tokens and events', () => {
  it('lists the tokens to describe: those of the map but the terrain, and those already described', () => {
    expect(activatableKeys(node(), category)).toEqual(['loot:red', 'objective:blue'])
  })

  it('adds an effect to a token: an event its rule names', () => {
    const { node: next, id } = addTokenEvent(node(), 'objective:blue')
    expect(id).toBe('ev-objective-blue-1')
    expect(tokenRulesOf(next)['objective:blue']?.events).toEqual([id])
    expect(eventsOfToken(next, 'objective:blue').map((e) => e.trigger)).toEqual([
      { on: 'tokenActivated', token: 'objective:blue' },
    ])
  })

  it('removes an event and its name from the token rules', () => {
    const next = removeEvent(node(), 'old')
    expect(eventsOf(next)).toEqual([])
    expect(tokenRulesOf(next)['loot:red']?.events).toEqual([])
  })

  it('adds an event of another trigger', () => {
    const { node: next } = addEvent(node(), 'roundStart')
    expect(eventsOf(next).at(-1)).toEqual({ id: 'ev-roundstart-1', trigger: { on: 'roundStart' }, actions: [] })
  })

  it('reads and writes where an action happens', () => {
    expect(spotOf(undefined)).toEqual({ kind: 'gm' })
    expect(spotOf({ on: 'token:totem:blue' })).toEqual({ kind: 'on', token: 'totem:blue' })
    expect(spotOf({ x: 3, y: 4 })).toEqual({ kind: 'cell', x: 3, y: 4 })
    expect(spotOf({ tile: 'um1:tile/um35', where: { en: 'any' } })).toEqual({ kind: 'other' })
    expect(atOf({ kind: 'adjacentTo', token: 'totem:green' })).toEqual({ adjacentTo: 'token:totem:green' })
  })
})
