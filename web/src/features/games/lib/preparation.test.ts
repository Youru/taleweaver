import { describe, expect, it } from 'vitest'
import type { CatalogAdventurer } from '@/features/catalog'
import type { PartyPreparation } from '../api/preparation'
import { adventurerId, counted, kitsOf, notChosen } from './preparation'

const preparation = {
  choices: [{ adventurer: 'Rook' }],
  adventurers: [
    { name: 'Rook', id: 'um1:adventurer/rook', hidden: false, required: true },
    { name: 'Remi', id: 'um1:adventurer/remi', hidden: false, required: false },
  ],
  kits: [
    { id: 'rook', name: { en: 'R' }, adventurer: 'um1:adventurer/rook', items: [], disciplines: [] },
    { id: 'remi', name: { en: 'M' }, adventurer: 'remi', items: [], disciplines: [] },
    { id: 'any', name: { en: 'A' }, items: [], disciplines: [] },
  ],
} as Partial<PartyPreparation> as PartyPreparation

describe('preparation', () => {
  it("gives an adventurer their kits and everybody's", () => {
    expect(kitsOf(preparation, 'Rook').map((k) => k.id)).toEqual(['rook', 'any'])
    expect(kitsOf(preparation, 'Remi').map((k) => k.id)).toEqual(['remi', 'any'])
    expect(kitsOf(preparation, 'Stranger').map((k) => k.id)).toEqual(['any'])
  })

  it('offers who is not chosen yet', () => {
    expect(notChosen(preparation).map((a) => a.name)).toEqual(['Remi'])
  })

  it('counts the copies of a card', () => {
    expect(counted(['a', 'b', 'a'])).toEqual([
      { id: 'a', count: 2 },
      { id: 'b', count: 1 },
    ])
  })

  it('reads an entry written by name', () => {
    const cards = [
      { id: 'um1:adventurer/rook', name: { en: 'Rook' } },
    ] as Partial<CatalogAdventurer>[] as CatalogAdventurer[]
    expect(adventurerId(cards, 'rook')).toBe('um1:adventurer/rook')
    expect(adventurerId(cards, 'Nobody')).toBe('Nobody')
  })
})
