import { describe, expect, it } from 'vitest'
import type { CatalogItem } from '@/features/catalog'
import type { AdventurerState, CampaignState } from '@/features/games'
import type { TokenPiece } from '@/features/table'
import {
  allItems,
  discardedOnUse,
  equippedCounts,
  limitWarnings,
  salePrice,
  searchItems,
  suggestedToken,
} from './inventory'

const item = (id: string, name: string, type: string, extra: Partial<CatalogItem> = {}): CatalogItem => ({
  id: `um1:item/${id}`,
  name: { en: name },
  type,
  tier: 'common',
  tags: [],
  ...extra,
})

const plank = item('ubiquitous-plank', 'Ubiquitous Plank', 'consumable', {
  tier: 'mundane',
  text: {
    en: 'X DISCARD\nYou may do 1 of the following:\n• At any time on your turn, Place a Plank token on an\nadjacent space.',
  },
})
const sword = item('long-sword', 'Long Sword', 'weapon', { tags: ['SWORD', 'DOUBLE'] })
const dagger = item('thief-dagger', 'Thief Dagger', 'weapon', { tier: 'mundane', tags: ['KNIFE'] })
const axe = item('hand-axe', 'Hand Axe', 'weapon', { tier: 'mundane' })
const idol = item('idol', 'Éclat d’idole', 'relic', { unique: true, tier: 'rare' })
const catalog = [plank, sword, dagger, axe, idol]
const byId = new Map(catalog.map((i) => [i.id, i]))

describe('searchItems', () => {
  it('finds by every word, ignoring accents and case, names starting with the query first', () => {
    expect(searchItems(catalog, { query: 'ECLAT' }, 'en').map((i) => i.id)).toEqual([idol.id])
    expect(searchItems(catalog, { query: 'sword' }, 'en')[0]).toBe(sword)
    expect(searchItems(catalog, { query: 'knife' }, 'en')).toEqual([dagger])
    expect(searchItems(catalog, { query: 'h' }, 'en').map((i) => i.id)).toEqual([axe.id, dagger.id])
  })

  it('keeps the type and tier asked', () => {
    expect(searchItems(catalog, { query: '', type: 'weapon', tier: 'mundane' }, 'en')).toEqual([axe, dagger])
  })
})

describe('limits', () => {
  const owned = (id: string, n: number) => ({ id: `item-${n}`, item: id, flipped: false })
  const rook: AdventurerState = {
    name: 'Rook',
    xp: 0,
    equipped: [owned(sword.id, 1), owned(dagger.id, 2), owned(axe.id, 3), owned(idol.id, 4)],
    pack: [owned(idol.id, 5), owned(plank.id, 6), owned(plank.id, 7), owned(plank.id, 8)],
  }

  it('warns of too many weapons, a full Pack and the same Relic twice', () => {
    expect(limitWarnings(rook, byId, 'en')).toEqual([
      { kind: 'over', type: 'weapon', count: 3, max: 2 },
      { kind: 'pack', count: 4, max: 3 },
      { kind: 'relic', name: 'Éclat d’idole', count: 2 },
    ])
  })

  it('counts the equipped items by type', () => {
    expect(equippedCounts(rook, byId)).toEqual([
      { type: 'weapon', count: 3, max: 2 },
      { type: 'relic', count: 1, max: 3 },
    ])
  })
})

describe('allItems', () => {
  it('lists the items to hand out, then each adventurer, then the stash', () => {
    const campaign: CampaignState = {
      gold: 0,
      items: [],
      revealedHiddenCards: [],
      unassigned: [{ id: 'item-1', tiers: ['rare'], flipped: false }],
      stash: [{ id: 'item-3', item: plank.id, flipped: false }],
      party: [{ name: 'Rook', xp: 0, pack: [{ id: 'item-2', item: sword.id, flipped: false }] }],
    }
    expect(allItems(campaign).map((l) => [l.owned.id, l.place])).toEqual([
      ['item-1', { owner: 'unassigned' }],
      ['item-2', { owner: 'Rook', slot: 'pack' }],
      ['item-3', { owner: 'stash' }],
    ])
  })
})

describe('cards', () => {
  it('sells by tier, the unique version for double, story items not at all', () => {
    expect(salePrice(dagger)).toBe(5)
    expect(salePrice(idol)).toBe(40)
    expect(salePrice({ ...sword, story: true })).toBeUndefined()
  })

  it('suggests the token a card places on a space, the smallest of that name', () => {
    const tokens: Record<string, TokenPiece> = {
      'um1:token/combatant/plank-large': { name: { en: 'Large Plank' }, category: 'combatant', size: '2x1' },
      'um1:token/combatant/plank': { name: { en: 'Small Plank' }, category: 'combatant', size: '1x1' },
      'um1:token/energy': { name: { en: 'Energy' }, category: 'ability' },
    }
    expect(suggestedToken(plank, tokens)).toBe('um1:token/combatant/plank')
    expect(
      suggestedToken(item('x', 'Battery', 'relic', { text: { en: 'Place an Energy token on this card.' } }), tokens),
    ).toBeUndefined()
  })

  it('discards Consumables and X DISCARD cards when used', () => {
    expect(discardedOnUse(plank)).toBe(true)
    expect(discardedOnUse(sword)).toBe(false)
  })
})
