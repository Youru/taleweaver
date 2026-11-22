import { describe, expect, it } from 'vitest'
import type { CatalogItem, Combatant } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { defaultSheet } from './fighters'
import { commandSheet, withConduitForms } from './scaling'

const items = new Map(
  (
    [
      { id: 'core', type: 'core', stats: { defense: 2 } },
      { id: 'sword', type: 'weapon', stats: { defense: 1 } },
    ] as CatalogItem[]
  ).map((i) => [i.id, i]),
)
const owner = {
  name: 'Rook',
  xp: 0,
  equipped: [
    { id: '1', item: 'core', flipped: false },
    { id: '2', item: 'sword', flipped: false },
  ],
} as AdventurerState
const ownerSheet = { ...defaultSheet, pool: ['teal', 'red'], defense: 10, armor: 3 }
const esper = {
  id: 'esper',
  family: 'esper',
  name: { en: 'Esper' },
  kind: 'command',
  stats: { defense: '*', armor: 4, combatDice: ['*', '*'], convictionDice: ['*', '*'], castingDie: '*' },
} as Combatant

describe('commandSheet', () => {
  it("reads the Scaling Icons on the owner, the Core's and Armor's Defense only", () => {
    expect(commandSheet(esper, owner, ownerSheet, items, 'en')).toMatchObject({
      pool: ['teal', 'red'],
      defense: 9,
      armor: 4,
      casting: 'purple',
    })
  })

  it('keeps what is printed', () => {
    const card = { ...esper, stats: { defense: 12, armor: 2, combatDice: ['green', 'red'] } } as Combatant
    expect(commandSheet(card, undefined, undefined, items, 'en')).toMatchObject({
      pool: ['green', 'red'],
      defense: 12,
      armor: 2,
    })
  })

  it('leaves the Defense to the GM without an owner', () => {
    expect(commandSheet(esper, undefined, undefined, items, 'en').defense).toBeNull()
  })

  it('puts a Conduit in the place of the adventurer', () => {
    const sheets = withConduitForms({}, [{ ...owner, conduitForm: 'esper' }], new Map([['esper', esper]]), items, 'en')
    expect(sheets.rook).toMatchObject({ armor: 4, pierce: 0 })
  })
})
