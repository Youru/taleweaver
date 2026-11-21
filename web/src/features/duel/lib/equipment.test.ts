import { describe, expect, it } from 'vitest'
import type { CatalogItem } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { equipmentSheet, partyEquipment, passivePierce } from './equipment'

const card = (id: string, type: string, extra: Partial<CatalogItem> = {}): CatalogItem => ({
  id,
  name: { en: id },
  type,
  tags: [],
  ...extra,
})
const catalog = new Map<string, CatalogItem>(
  [
    card('katar', 'weapon', {
      stats: { dice: ['white'], hands: 1, attack: 'melee' },
      text: { en: '* PASSIVE\n+2 ARMOR PIERCING\n*COMBO : LIGHT\nBACKSTAB' },
    }),
    card('chain-pick', 'weapon', {
      stats: { dice: ['black', 'green', 'green'], hands: 2, attack: 'melee' },
      backSide: {
        name: { en: 'back' },
        tags: [],
        text: { en: 'EXHAUST\nEMPOWER' },
        stats: { dice: ['black', 'red', 'green'], hands: 2, attack: 'ranged', range: 4 },
      },
    }),
    card('buckler', 'weapon', { stats: { dice: ['purple'], hands: 1, attack: 'either', armor: 1 } }),
    card('cuirass', 'armor', { stats: { armor: 2, health: 2 } }),
    card('charm', 'core', { stats: { defense: 4 } }),
    card('oblation', 'armor', { stats: { armor: 0, defense: 'X', movement: 1 } }),
    card('ring', 'relic'),
  ].map((c) => [c.id, c]),
)
const adventurer = (equipped: { item: string; flipped?: boolean }[]): AdventurerState =>
  ({
    name: 'Nightingale',
    equipped: equipped.map((e, i) => ({ id: `item-${i}`, ...e })),
    pack: [],
  }) as unknown as AdventurerState

describe('equipmentSheet', () => {
  it('adds the dice of both weapons and every Armor and Defense bonus', () => {
    const sheet = equipmentSheet(
      adventurer([{ item: 'katar' }, { item: 'buckler' }, { item: 'cuirass' }, { item: 'charm' }]),
      catalog,
      'en',
    )
    expect(sheet?.pool).toEqual(['white', 'purple'])
    expect(sheet?.armor).toBe(3)
    expect(sheet?.defense).toBe(11)
    expect(sheet?.pierce).toBe(2)
    expect(sheet?.items).toEqual(['katar', 'buckler', 'cuirass', 'charm'])
  })

  it('reads the side face up', () => {
    expect(equipmentSheet(adventurer([{ item: 'chain-pick', flipped: true }]), catalog, 'en')?.pool).toEqual([
      'black',
      'red',
      'green',
    ])
  })

  it('rolls 2 purple dice without a weapon and leaves a Defense "X" to the GM', () => {
    const sheet = equipmentSheet(adventurer([{ item: 'oblation' }, { item: 'ring' }]), catalog, 'en')
    expect(sheet?.pool).toEqual(['purple', 'purple'])
    expect(sheet?.defense).toBe(7)
    expect(sheet?.variableDefense).toEqual(['oblation'])
  })

  it('gives nothing when no card equipped is known', () => {
    expect(equipmentSheet(adventurer([]), catalog, 'en')).toBeUndefined()
    expect(partyEquipment([adventurer([{ item: 'unknown' }])], catalog, 'en')).toEqual({})
    expect(Object.keys(partyEquipment([adventurer([{ item: 'katar' }])], catalog, 'en'))).toEqual(['nightingale'])
  })
})

describe('passivePierce', () => {
  it('counts ARMOR PIERCING only in the PASSIVE block', () => {
    expect(passivePierce('* PASSIVE\n• +1 ARMOR PIERCING\nEXHAUST\nDODGE')).toBe(1)
    expect(passivePierce('* PASSIVE\n• Weapons have REACH 2.\n*COMBO : LIGHT\n+2 ARMOR PIERCING')).toBe(0)
    expect(passivePierce(undefined)).toBe(0)
  })
})
