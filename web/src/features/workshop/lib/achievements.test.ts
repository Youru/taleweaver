import { describe, expect, it } from 'vitest'
import { addAchievement, withXp, xpOf } from './achievements'

describe('achievements', () => {
  it('numbers a new one apart from the others', () => {
    const list = addAchievement(addAchievement([], 'en'), 'en')
    expect(list.map((item) => item.id)).toEqual(['achievement-1', 'achievement-2'])
  })

  it('changes the XP without losing the other rewards', () => {
    const item = { id: 'a', name: { en: 'A' }, actions: [{ do: 'gainXp', amount: 1 }, { do: 'restore' }] }
    expect(xpOf(withXp(item, 3))).toBe(3)
    expect(withXp(item, 0).actions).toEqual([{ do: 'restore' }])
  })
})
