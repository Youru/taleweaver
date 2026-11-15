import { describe, expect, it } from 'vitest'
import type { SymbolAbility } from './abilities'
import { attackOdds, resolveAttack, type AttackRoll, type AttackSetup } from './attack'
import { readFaces } from './dice'
import { testDice } from './testDice'

const ability = (
  cost: Partial<SymbolAbility['cost']>,
  kind: 'toHit' | 'physical' | 'magic' | 'pierce',
  amount: number,
): SymbolAbility => ({
  cost: { shield: 0, book: 0, star: 0, ...cost },
  boost: { kind, amount },
  text: `+${amount}`,
})

function setup(overrides: Partial<AttackSetup> = {}): AttackSetup {
  return {
    pool: ['white', 'purple'],
    empower: false,
    toHit: 0,
    physical: 0,
    pierce: 0,
    abilities: [],
    defense: 9,
    armor: 0,
    dodge: false,
    ...overrides,
  }
}
function roll(overrides: Partial<AttackRoll> = {}): AttackRoll {
  return { total: 0, shield: 0, book: 0, star: 0, skull: false, dodgeShields: 0, dodgeSkull: false, ...overrides }
}

describe('resolveAttack', () => {
  it("works out Nightingale's Attack on the Animate (rulebook p. 45): 7 Final Damage", () => {
    const outcome = resolveAttack(
      setup({
        defense: 9,
        armor: 2,
        pierce: 2,
        abilities: [
          ability({ shield: 2 }, 'physical', 2),
          ability({ shield: 2 }, 'physical', 2),
          ability({ star: 1 }, 'physical', 1),
        ],
      }),
      roll({ total: 13, book: 2, shield: 3, star: 1 }),
    )

    expect(outcome).toMatchObject({ hit: true, difference: 4, physical: 7, armor: 0, final: 7 })
  })

  it('spends the symbols for the most Damage, as an Intelligent Combatant must (p. 65): 7, then 6 after Armor', () => {
    const animate = setup({
      abilities: [ability({ book: 1, shield: 1 }, 'physical', 1), ability({ star: 1 }, 'physical', 2)],
      defense: 7,
    })

    expect(resolveAttack(animate, roll({ total: 10, book: 2, shield: 2, star: 1 })).final).toBe(7)
    expect(resolveAttack({ ...animate, armor: 2 }, roll({ total: 12, book: 7, shield: 3 })).final).toBe(6)
  })

  it('misses on the skull of the Empower die, whatever the numbers', () => {
    expect(resolveAttack(setup({ empower: true }), roll({ total: 20, skull: true })).hit).toBe(false)
  })

  it('adds the shields of a Dodge to the Defense, unless the skull is rolled', () => {
    const dodging = setup({ dodge: true })

    expect(resolveAttack(dodging, roll({ total: 10, dodgeShields: 2 })).hit).toBe(false)
    expect(resolveAttack(dodging, roll({ total: 10, dodgeShields: 0, dodgeSkull: true })).difference).toBe(1)
  })

  it('spends symbols TO THE ATK ROLL when they turn a miss into a hit', () => {
    const outcome = resolveAttack(setup({ abilities: [ability({ book: 1 }, 'toHit', 1)] }), roll({ total: 8, book: 1 }))

    expect(outcome).toMatchObject({ hit: true, difference: 0, final: 0 })
    expect(outcome.spent).toHaveLength(1)
  })

  it('does not reduce MAGIC DMG with Armor', () => {
    const outcome = resolveAttack(
      setup({ armor: 5, abilities: [ability({ book: 1 }, 'magic', 3)] }),
      roll({ total: 10, book: 1 }),
    )

    expect(outcome.final).toBe(3)
  })
})

describe('attackOdds', () => {
  it('counts the faces: a purple die hits Defense 4 half of the time', () => {
    const odds = attackOdds(setup({ pool: ['purple'], defense: 4 }), testDice)

    expect(odds?.hit).toBeCloseTo(0.5)
    expect(odds?.needed).toBe(4)
    expect(odds?.expected).toBeCloseTo((1 + 2 + 3) / 6)
  })

  it('gives the chance to defeat the target', () => {
    const odds = attackOdds(setup({ pool: ['purple'], defense: 4 }), testDice, 3)

    expect(odds?.defeat).toBeCloseTo(1 / 6)
  })

  it('lowers the odds when the target Dodges, and with the Empower skull', () => {
    const base = attackOdds(setup(), testDice)!.hit

    expect(attackOdds(setup({ dodge: true }), testDice)!.hit).toBeLessThan(base)
    expect(attackOdds(setup({ empower: true }), testDice)!.hit).toBeLessThan(base)
  })

  it('cannot tell without every die', () => {
    expect(attackOdds(setup({ pool: ['*', '*'] }), testDice)).toBeUndefined()
  })
})

describe('readFaces', () => {
  it('adds up the faces picked on each die, symbols included', () => {
    // white 7 (star, 2 shields, book) + purple 5 (shield, book) + black (skull)
    expect(readFaces(['white', 'purple', 'black'], [5, 3, 5], testDice)).toEqual({
      total: 12,
      shield: 3,
      book: 2,
      star: 1,
      skull: true,
    })
  })

  it('waits until every die has its face', () => {
    expect(readFaces(['white', 'purple'], [5, null], testDice)).toBeUndefined()
  })
})
