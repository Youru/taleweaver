import { describe, expect, it } from 'vitest'
import { symbolAbilities } from './abilities'

describe('symbolAbilities', () => {
  it('reads costs and counted effects, and keeps the others as reminders', () => {
    const abilities = symbolAbilities(
      [
        { cost: [{ symbol: 'book' }, { symbol: 'shield' }], effect: { en: '+1 PHYSICAL DMG' } },
        { cost: [{ symbol: 'shield×2' }], effect: { en: '+1 ARMOR PIERCING' } },
        { cost: [{ symbol: 'star' }], effect: { en: 'FU: HEAL 2' } },
        { cost: [{ symbol: '*shield' }, { symbol: '*book' }], effect: { en: "(Esper's symbols)" } },
      ],
      'fr',
    )

    expect(abilities).toEqual([
      { cost: { shield: 1, book: 1, star: 0 }, boost: { kind: 'physical', amount: 1 }, text: '+1 PHYSICAL DMG' },
      { cost: { shield: 2, book: 0, star: 0 }, boost: { kind: 'pierce', amount: 1 }, text: '+1 ARMOR PIERCING' },
      { cost: { shield: 0, book: 0, star: 1 }, boost: undefined, text: 'FU: HEAL 2' },
    ])
  })
})
