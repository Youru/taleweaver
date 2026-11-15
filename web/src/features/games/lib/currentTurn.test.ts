import { describe, expect, it } from 'vitest'
import type { EncounterState, LogEntry } from '../api/types'
import { currentTurn } from './currentTurn'

function encounter(overrides: Partial<EncounterState> = {}): EncounterState {
  return {
    node: 'fight',
    round: 1,
    urgency: 0,
    ended: false,
    initiativeTrack: ['adventurer:Rook', 'um1:initiative/fragor'],
    currentTurn: 1,
    combatants: [],
    tokens: [],
    placedDiagrams: [],
    revealedAreas: [],
    adventurers: [],
    activeEndConditions: [],
    ...overrides,
  }
}

const log: LogEntry[] = [
  { code: 'roundStart', args: { round: '1' } },
  { code: 'turn', args: { card: 'adventurer:Rook', adventurer: 'Rook' } },
  { code: 'turn', args: { card: 'um1:initiative/fragor' } },
  { code: 'figureTurn', args: { instance: 'fragor-1' } },
  { code: 'answer', args: { answer: 'yes' } },
  { code: 'aiInstruction', text: { en: 'Move 4.' }, args: { instance: 'fragor-1' } },
  { code: 'figureTurnEnd', args: { instance: 'fragor-1' } },
  { code: 'figureTurn', args: { instance: 'fragor-2' } },
]

describe('currentTurn', () => {
  it('is empty before the first round', () => {
    expect(currentTurn(encounter({ round: 0 }), [])).toEqual({ kind: 'before', entries: [] })
  })

  it("keeps the whole group's instructions until the next turn, without answers and bookkeeping", () => {
    const turn = currentTurn(
      encounter({ activation: { initiativeCard: 'um1:initiative/fragor', figures: ['fragor-2'], step: 0 } }),
      log,
    )

    expect(turn.kind).toBe('combatants')
    expect(turn.acting).toBe('fragor-2')
    expect(turn.entries.map((e) => e.code)).toEqual(['figureTurn', 'aiInstruction', 'figureTurn'])
  })

  it('names the adventurer whose turn it is', () => {
    const turn = currentTurn(encounter({ currentTurn: 0 }), log.slice(0, 2))

    expect(turn).toEqual({ kind: 'adventurer', who: 'Rook', entries: [] })
  })

  it('says when the group has finished', () => {
    expect(currentTurn(encounter(), log).kind).toBe('idle')
  })
})
