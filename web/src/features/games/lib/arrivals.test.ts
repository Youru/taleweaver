import { describe, expect, it } from 'vitest'
import { arrivedCombatants } from './arrivals'

describe('arrivedCombatants', () => {
  it('keeps the spawns of the encounter in progress, not its setup nor earlier encounters', () => {
    const arrived = arrivedCombatants([
      { code: 'encounter' },
      { code: 'spawn', args: { instance: 'fragor-1' } },
      { code: 'encounter' },
      { code: 'place', args: { instance: 'cave-sickle-1' } },
      { code: 'tokenActivated', args: { token: 'totem:blue' } },
      { code: 'spawn', args: { instance: 'cave-sickle-2' } },
      { code: 'spawn', args: { instance: 'cave-sickle-3' } },
    ])
    expect([...arrived]).toEqual(['cave-sickle-2', 'cave-sickle-3'])
  })

  it('is empty without a log', () => {
    expect(arrivedCombatants(undefined).size).toBe(0)
  })
})
