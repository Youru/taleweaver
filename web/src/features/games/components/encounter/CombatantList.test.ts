import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import { i18n } from '@/shared/i18n'
import type { CombatantState, EncounterState } from '../../api/types'
import CombatantList from './CombatantList.vue'

const combatant = (instance: string, extra: Partial<CombatantState> = {}): CombatantState => ({
  instance,
  card: 'um1:combatant/fragor-1-3-common',
  family: 'fragor',
  number: 1,
  initiativeCards: [],
  damage: 0,
  effects: [],
  aggro: false,
  defeated: false,
  position: { x: 0, y: 0 },
  ...extra,
})

function list(combatants: CombatantState[], shown?: Set<string>, acting?: string) {
  const encounter = {
    combatants,
    activation: acting ? { initiativeCard: 'x', figures: [acting], step: 0 } : undefined,
  } as EncounterState
  return mount(CombatantList, {
    props: { encounter, cards: new Map(), selected: undefined, shown },
    global: { plugins: [createPinia(), i18n, PrimeVue] },
  })
}

describe('CombatantList', () => {
  it('leaves out what is not on the table yet, so as not to tell what is coming', () => {
    const wrapper = list(
      [combatant('fragor-1'), combatant('gevaudan-1', { position: undefined }), combatant('loa-1')],
      new Set(['fragor-1']),
    )
    const names = wrapper.findAll('strong').map((n) => n.text())
    expect(names).toEqual(['fragor-1'])
  })

  it('still lists the acting figure, marked as to place', () => {
    const wrapper = list([combatant('gevaudan-1', { position: undefined })], new Set(), 'gevaudan-1')
    expect(wrapper.findAll('strong').map((n) => n.text())).toEqual(['gevaudan-1'])
    expect(wrapper.text()).toContain('à placer')
  })
})
