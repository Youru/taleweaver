import { describe, expect, it } from 'vitest'
import type { DraftNode } from '../api/types'
import { addHiddenRule, hiddenRulesOf, removeHiddenRule, updateHiddenRule } from './hiddenRules'

const node: DraftNode = { id: 'fight', type: 'encounter' }

describe('hidden rules', () => {
  it('adds numbered rules, and lettered parts of a rule', () => {
    const one = addHiddenRule(addHiddenRule(node, 'fr'), 'fr')
    const parts = addHiddenRule(addHiddenRule(one, 'fr', 'hidden-rule-1'), 'fr', 'hidden-rule-1')

    expect(hiddenRulesOf(parts).map((r) => [r.id, r.partOf])).toEqual([
      ['hidden-rule-1', undefined],
      ['hidden-rule-2', undefined],
      ['hidden-rule-1-a', 'hidden-rule-1'],
      ['hidden-rule-1-b', 'hidden-rule-1'],
    ])
  })

  it('updates a rule and drops the fields emptied', () => {
    const one = addHiddenRule(node, 'en', undefined)
    const next = updateHiddenRule(one, 'hidden-rule-1', { text: { en: 'Reveal the crypt.' }, actions: [] })

    expect(hiddenRulesOf(next)[0]).toEqual({ id: 'hidden-rule-1', name: { en: '' }, text: { en: 'Reveal the crypt.' } })
  })

  it('removes a rule, its parts become rules of their own, and the list disappears when empty', () => {
    const parts = addHiddenRule(addHiddenRule(node, 'fr'), 'fr', 'hidden-rule-1')

    const without = removeHiddenRule(parts, 'hidden-rule-1')

    expect(hiddenRulesOf(without)).toEqual([{ id: 'hidden-rule-1-a', name: { fr: '' }, text: { fr: '' } }])
    expect('hiddenRules' in removeHiddenRule(without, 'hidden-rule-1-a')).toBe(false)
  })
})
