import { describe, expect, it } from 'vitest'
import type { Reference } from '../api/queries'
import { ruleOfToken } from './reference'

const reference: Reference = {
  effects: [{ id: 'poison', name: { en: 'Poison', fr: 'Empoisonné' }, text: { en: 'Roll a die.' } }],
  terrainTypes: [{ id: 'obstructing', name: { en: 'Obstructing' } }],
  abilityTokens: [],
  combatantTokens: [{ id: 'plank', name: { en: 'Plank' } }],
  tags: [],
}

describe('reference', () => {
  it('knows the rule of a token of the board', () => {
    expect(ruleOfToken(reference, 'um1:token/terrain/obstructing-long')?.id).toBe('obstructing')
    expect(ruleOfToken(reference, 'um1:token/plank')?.id).toBe('plank')
    expect(ruleOfToken(reference, 'um1:token/totem')).toBeUndefined()
  })
})
