import { describe, expect, it } from 'vitest'
import type { DraftScenario } from '../api/types'
import { nextKitId, partyOf, withParty, withStart } from './party'

const scenario = {
  id: 'homebrew:scenario/x',
  title: { en: 'X' },
  packs: [],
  start: 'a',
  nodes: [],
  party: { size: 4, unknownField: 1, allowedAdventurers: ['Rook'] },
} as DraftScenario

describe('party', () => {
  it('keeps what it does not edit', () => {
    expect(partyOf(withParty(scenario, { size: 3 }))).toMatchObject({ size: 3, unknownField: 1 })
  })

  it('does not write what says nothing', () => {
    expect(partyOf(withParty(scenario, { allowedAdventurers: [] }))).not.toHaveProperty('allowedAdventurers')
    const shop = withStart(scenario, { items: { tiers: ['mundane'] } })
    expect(partyOf(withStart(shop, { items: null }))).not.toHaveProperty('start')
  })

  it('numbers the kits apart', () => {
    expect(nextKitId([{ id: 'kit-2', name: {} }])).toBe('kit-3')
  })
})
