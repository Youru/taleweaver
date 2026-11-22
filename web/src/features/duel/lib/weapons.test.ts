import { describe, expect, it } from 'vitest'
import type { CatalogItem, Combatant } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { cardWeapon, equippedWeapon } from './weapons'

const card = (...texts: string[]) =>
  ({ aiSteps: [{ instructions: texts.map((en) => ({ text: { en } })) }] }) as unknown as Combatant

describe('what a figure Attacks with', () => {
  it('reads a card', () => {
    expect(cardWeapon(card('Make a [Melee] ATK.'))).toEqual({ melee: true, reach: 1 })
    expect(cardWeapon(card('Make a [Melee] ATK within REACH 2.'))).toEqual({ melee: true, reach: 2 })
    expect(cardWeapon(card('Make a [Ranged] 4 ATK.'))).toBeUndefined()
  })

  it('reads the Weapons equipped', () => {
    const item = (id: string, attack: string, en = '') => [id, { id, stats: { attack }, text: { en } }] as const
    const catalog = new Map([
      item('sword', 'melee'),
      item('spear', 'melee', '* PASSIVE\n• REACH 2'),
      item('bow', 'ranged'),
      item('knife', 'either'),
    ]) as unknown as Map<string, CatalogItem>
    const weapon = (...ids: string[]) =>
      equippedWeapon({ equipped: ids.map((id) => ({ id, item: id })) } as unknown as AdventurerState, catalog)

    expect(weapon('sword', 'knife')).toEqual({ melee: true, reach: 1 })
    expect(weapon('spear')).toEqual({ melee: true, reach: 2 })
    expect(weapon('spear', 'sword')).toEqual({ melee: true, reach: 1 })
    expect(weapon('bow', 'knife')).toBeUndefined()
    expect(weapon()).toBeUndefined()
  })
})
