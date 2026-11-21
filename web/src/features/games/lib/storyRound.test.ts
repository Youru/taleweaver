import { describe, expect, it } from 'vitest'
import type { CatalogItem } from '@/features/catalog'
import type { CampaignState } from '../api/types'
import { activeMembers, storeItems } from './storyRound'

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
    const store = { items: [{ tiers: ['mundane'], types: [] }, { tiers: [], types: ['accessory'] }] }

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
