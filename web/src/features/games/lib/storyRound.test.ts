import { describe, expect, it } from 'vitest'
import type { CatalogItem } from '@/features/catalog'
import type { AdventurerState, CampaignState } from '../api/types'
import { activeMembers, disciplineBlock, disciplineCost, respecPrice, storeItems, xpLeft } from './storyRound'

const item = (id: string, tier: string | null, type: string, extra: Partial<CatalogItem> = {}): CatalogItem => ({
  id,
  name: { en: id },
  tier,
  type,
  tags: [],
  ...extra,
})

const catalog = [
  item('sword', 'mundane', 'weapon'),
  item('axe', 'common', 'weapon'),
  item('ring', null, 'accessory'),
  item('crown', 'mundane', 'relic', { unique: true }),
  item('idol', 'mundane', 'relic', { unique: true }),
  item('letter', 'mundane', 'relic', { story: true }),
  item('fang', 'monster', 'accessory'),
]

describe('storeItems', () => {
  it('sells every card of the lines, the drawn unique first, never story items nor monster loot', () => {
    const store = {
      items: [
        { tiers: ['mundane'], types: [] },
        { tiers: [], types: ['accessory'] },
      ],
    }

    expect(storeItems(store, catalog, ['crown']).map((i) => i.id)).toEqual(['crown', 'sword', 'ring'])
  })

  it('sells nothing without a store', () => {
    expect(storeItems(undefined, catalog)).toEqual([])
  })
})

describe('activeMembers', () => {
  const party = [
    { name: 'Nightingale', xp: 0 },
    { name: 'Rook', xp: 0, injured: true },
    { name: 'Zeke', xp: 0 },
    { name: 'Remi', xp: 0, unselectable: true },
  ]

  it('leaves out the Injured and the Unselectable', () => {
    expect(activeMembers({ party } as CampaignState)).toEqual(['Nightingale', 'Zeke'])
  })

  it('keeps to the Active Group when there is one', () => {
    expect(activeMembers({ party, activeGroup: ['Zeke'] } as CampaignState)).toEqual(['Zeke'])
  })
})

describe('xpLeft and disciplineCost', () => {
  const catalog = [
    { id: 'gore-shot', name: { en: 'Gore Shot' }, tree: 'cruor', level: 1, cost: 2 },
    { id: 'blade-works', name: { en: 'Blade Works' }, tree: 'martial', level: 1, cost: 2 },
    { id: 'blood-rage', name: { en: 'Blood Rage' }, tree: 'cruor', level: 2 },
  ]
  const zeke = { name: 'Zeke', xp: 6, disciplines: [{ id: 'gore-shot', xp: 2 }] }

  it('counts the XP the Disciplines cost', () => {
    expect(xpLeft(zeke)).toBe(4)
  })

  it('adds 1 XP per Discipline of the same level, and leaves an unknown cost to the GM', () => {
    expect(disciplineCost(catalog[1]!, zeke, catalog)).toBe(3)
    expect(disciplineCost(catalog[2]!, zeke, catalog)).toBeNull()
  })

  it('needs each lower level of the tree, and never the same card twice', () => {
    const rook = { name: 'Rook', xp: 6, disciplines: [{ id: 'blade-works', xp: 2 }] }

    expect(disciplineBlock(catalog[2]!, zeke, catalog)).toBeNull()
    expect(disciplineBlock(catalog[2]!, rook, catalog)).toEqual({ missingLevel: 1 })
    expect(disciplineBlock(catalog[0]!, zeke, catalog)).toEqual({ learnt: true })
  })
})

describe('respecPrice', () => {
  it('costs 25 Gold, +25 per Loot Level beyond Mundane of the Store', () => {
    expect(respecPrice(undefined)).toBe(25)
    expect(
      respecPrice({
        items: [
          { tiers: ['mundane'], types: [] },
          { tiers: ['uncommon'], types: [] },
        ],
      }),
    ).toBe(75)
    expect(respecPrice({ items: [], unique: { tier: 'common', count: 1 } })).toBe(50)
  })
})

describe('Familiars and Exalted Espers (p. 76)', () => {
  const cards = [
    { id: 'um1:discipline/familiar', name: { en: 'Familiar' }, tree: 'assemblage', level: 1, cost: 3 },
    { id: 'um1:discipline/tatva', name: { en: 'Tatva' }, tree: 'familiar', story: true },
    { id: 'um1:discipline/yuxa', name: { en: 'Yuxa' }, tree: 'familiar', story: true },
    { id: 'um1:discipline/minos', name: { en: 'Minos' }, tree: 'exalted', story: true },
  ]
  const with_ = (...ids: string[]) => ({ disciplines: ids.map((id) => ({ id, xp: 0 })) }) as unknown as AdventurerState

  it('a Familiar needs the Discipline, then only one is chosen; an Exalted Esper is never learnt', () => {
    expect(disciplineBlock(cards[1]!, with_(), cards)).toEqual({ familiar: 'needed' })
    expect(disciplineBlock(cards[1]!, with_('um1:discipline/familiar'), cards)).toBeNull()
    expect(disciplineBlock(cards[2]!, with_('um1:discipline/familiar', 'um1:discipline/tatva'), cards)).toEqual({
      familiar: 'chosen',
    })
    expect(disciplineBlock(cards[3]!, with_('um1:discipline/familiar'), cards)).toEqual({ exalted: true })
  })
})
