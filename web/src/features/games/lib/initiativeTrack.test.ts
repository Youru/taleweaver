import { describe, expect, it } from 'vitest'
import type { InitiativeCard } from '@/features/catalog'
import type { CombatantState, EncounterState } from '../api/types'
import { initiativeTrack } from './initiativeTrack'

const card = (id: string, label: string, more: Partial<InitiativeCard> = {}): InitiativeCard => ({
  id,
  label,
  adventurer: false,
  hidden: false,
  lowres: false,
  image: `assets/act1/initiative/${id.split('/')[1]}.jpg`,
  ...more,
})

const cards: InitiativeCard[] = [
  card('um1:initiative/cave-sickle-1-3', 'Cave Sickle 1-3', { group: [1, 3] }),
  card('um1:initiative/agares', 'Agares'),
  card('um1:initiative/rook', 'Rook', { adventurer: true }),
  card('um1:initiative/damocles-red', 'Damocles', { adventurer: true, hidden: true, border: 'red', lowres: true }),
  card('um1:initiative/damocles', 'Damocles', { adventurer: true, hidden: true }),
]

const sickle = (number: number, more: Partial<CombatantState> = {}): CombatantState => ({
  instance: `cave-sickle-${number}`,
  card: 'um1:combatant/cave-sickle-1-3-common',
  family: 'cave-sickle',
  number,
  initiativeCards: ['um1:initiative/cave-sickle-1-3'],
  damage: 0,
  effects: [],
  aggro: false,
  defeated: false,
  ...more,
})

function encounter(overrides: Partial<EncounterState> = {}): EncounterState {
  return {
    node: 'fight',
    round: 1,
    urgency: 0,
    ended: false,
    initiativeTrack: [
      'adventurer:Rook',
      'um1:initiative/cave-sickle-1-3',
      'um1:initiative/agares',
      'adventurer:Damocles',
    ],
    currentTurn: 1,
    combatants: [
      sickle(1),
      sickle(2, { defeated: true }),
      sickle(3),
      {
        ...sickle(1),
        instance: 'agares-1',
        family: 'agares',
        initiativeCards: ['um1:initiative/agares'],
      },
    ],
    tokens: [],
    placedDiagrams: [],
    revealedAreas: [],
    adventurers: [],
    activeEndConditions: [],
    activation: { initiativeCard: 'um1:initiative/cave-sickle-1-3', figures: ['cave-sickle-3'], step: 0 },
    ...overrides,
  }
}

describe('initiativeTrack', () => {
  it('tells who has played, who plays and who comes next, front first', () => {
    const track = initiativeTrack(encounter(), cards)

    expect(track.map((c) => [c.order, c.label, c.state])).toEqual([
      [1, 'Rook', 'played'],
      [2, 'Cave Sickle 1-3', 'current'],
      [3, 'Agares', 'next'],
      [4, 'Damocles', 'later'],
    ])
  })

  it("shows a group's figures still on the board, the acting one among them", () => {
    const [, group, single] = initiativeTrack(encounter(), cards)

    expect(group!.figures.map((f) => [f.number, f.state])).toEqual([
      [1, 'played'],
      [3, 'acting'],
    ])
    expect(single!.figures).toEqual([])
  })

  it("finds an adventurer's card by their name, the plain print first", () => {
    const track = initiativeTrack(encounter(), cards)

    expect(track[0]).toMatchObject({ side: 'adventurer', image: 'assets/act1/initiative/rook.jpg' })
    expect(track[3]!.image).toBe('assets/act1/initiative/damocles.jpg')
    expect(track[1]!.side).toBe('opponent')
  })

  it('names a card the catalogue does not know by its id, without a picture', () => {
    const [first] = initiativeTrack(encounter({ initiativeTrack: ['hb:initiative/brute'], currentTurn: 0 }), [])

    expect(first).toMatchObject({ label: 'brute', image: undefined, side: 'opponent', state: 'current' })
  })

  it('follows the cards moved: one played sent to the back stays played, the next is the first not played', () => {
    const track = initiativeTrack(
      encounter({
        initiativeTrack: [
          'um1:initiative/agares',
          'um1:initiative/cave-sickle-1-3',
          'adventurer:Damocles',
          'adventurer:Rook',
        ],
        currentTurn: 1,
        playedCards: ['adventurer:Rook', 'um1:initiative/cave-sickle-1-3'],
      }),
      cards,
    )

    expect(track.map((c) => [c.label, c.state])).toEqual([
      ['Agares', 'next'],
      ['Cave Sickle 1-3', 'current'],
      ['Damocles', 'later'],
      ['Rook', 'played'],
    ])
  })

  it('has no turn yet before the first round', () => {
    expect(initiativeTrack(encounter({ round: 0 }), cards).every((c) => c.state === 'later')).toBe(true)
  })
})
