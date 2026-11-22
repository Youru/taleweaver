import { describe, expect, it } from 'vitest'
import type { EncounterState } from '../api/types'
import { adventurerOf, playingNow, withAuthor } from './play'

const turn = (card: string, more: Partial<EncounterState> = {}) =>
  ({ round: 1, currentTurn: 0, initiativeTrack: [card], allies: [], ...more }) as unknown as EncounterState

describe('who did it', () => {
  it('is proposed from the turn, a second card included', () => {
    expect(playingNow(turn('adventurer:Rook#2'))).toEqual(['adventurer:Rook'])
    expect(playingNow(turn('hb:initiative/imp'))).toEqual([])
    expect(adventurerOf('imp-1')).toBeUndefined()
  })

  it('goes on an opponent hurt, not on an adventurer hurt', () => {
    expect(withAuthor({ type: 'damage', instance: 'imp-1', amount: 2 }, 'adventurer:Rook')).toMatchObject({
      author: 'adventurer:Rook',
    })
    expect(withAuthor({ type: 'damage', instance: 'adventurer:Iris', amount: 2 }, 'adventurer:Rook')).not.toHaveProperty(
      'author',
    )
  })

  it('never replaces who was already said, nobody included', () => {
    const said = { type: 'defeat', instance: 'imp-1', author: null } as const
    expect(withAuthor(said, 'adventurer:Rook')).toEqual(said)
  })
})
