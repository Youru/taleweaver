import { describe, expect, it } from 'vitest'
import { resolveSpell, spellOdds } from './spell'
import { testDice } from './testDice'

describe('resolveSpell', () => {
  it("Gore Shot (p. 54): 7 rolled + SPELL 6 = Force 13 beats the Animate's Conviction of 7", () => {
    const outcome = resolveSpell(
      { casting: 'white', force: 6, empower: false, conviction: ['white', 'teal'] },
      { casting: 7, books: 0, skull: false, conviction: 7 },
    )

    expect(outcome).toEqual({ force: 13, backfire: false, resisted: false, affected: true })
  })

  it('a Spell-Like Ability has its printed Force: FORCE 7 + DIFFERENCE HIT 4 (p. 55)', () => {
    const outcome = resolveSpell(
      { casting: null, force: 11, empower: false, conviction: [] },
      { casting: 0, books: 0, skull: false, conviction: 11 },
    )

    expect(outcome.force).toBe(11)
    expect(outcome.resisted).toBe(true)
  })

  it('backfires on the Empower skull, and adds one Force per book otherwise', () => {
    const setup = { casting: 'purple', force: 6, empower: true, conviction: ['purple', 'purple'] }

    expect(resolveSpell(setup, { casting: 3, books: 0, skull: true, conviction: 0 }).backfire).toBe(true)
    expect(resolveSpell(setup, { casting: 3, books: 2, skull: false, conviction: 0 }).force).toBe(11)
  })
})

describe('spellOdds', () => {
  it('weighs the Force against the Conviction Check', () => {
    const odds = spellOdds({ casting: null, force: 3, empower: false, conviction: ['purple'] }, testDice)

    expect(odds?.affected).toBeCloseTo(2 / 6)
    expect(odds?.backfire).toBe(0)
  })

  it('backfires one time in six when Empowered', () => {
    expect(
      spellOdds({ casting: 'purple', force: 6, empower: true, conviction: ['purple', 'purple'] }, testDice)?.backfire,
    ).toBeCloseTo(1 / 6)
  })
})
