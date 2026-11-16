import { describe, expect, it } from 'vitest'
import { resolveAttack, type AttackSetup } from './attack'
import { effectMods } from './effects'

const setup = (patch: Partial<AttackSetup> = {}): AttackSetup => ({
  pool: ['purple', 'purple'],
  empower: false,
  toHit: 0,
  physical: 0,
  pierce: 0,
  abilities: [],
  defense: 7,
  armor: 1,
  dodge: false,
  ...patch,
})
const roll = { total: 9, shield: 0, book: 0, star: 0, skull: false, dodgeShields: 0, dodgeSkull: false }

describe('effects in a duel', () => {
  it('counts Darkness on the attacker and Barrier, Nemesis and Wilt on the target', () => {
    const mods = effectMods(['darkness'], ['barrier', 'nemesis', 'wilt'])
    expect(mods).toMatchObject({ toHit: -1, armor: 1, physical: 2, irreducible: 2, noDodge: false })
    const outcome = resolveAttack(
      setup({
        toHit: mods.toHit,
        armor: 1 + mods.armor,
        physical: mods.physical,
        irreducible: mods.irreducible,
      }),
      roll,
    )
    // 9 − 1 = 8 against 7: difference 1, +2 Nemesis = 3 physical, −2 Armor, +2 Wilt
    expect(outcome).toMatchObject({ hit: true, difference: 1, physical: 3, armor: 2, irreducible: 2, final: 3 })
  })

  it('takes the Dodge away from a target in Darkness, and stops a Paralyzed attacker', () => {
    expect(effectMods([], ['darkness']).noDodge).toBe(true)
    expect(effectMods(['paralyze'], []).paralyzed).toBe(true)
  })

  it('gives a shield and a book to an attacker with Courage, to spend on its abilities', () => {
    const mods = effectMods(['courage'], [])
    const book = {
      cost: { shield: 0, book: 1, star: 0 },
      text: '+2 PHYSICAL DMG',
      boost: { kind: 'physical', amount: 2 },
    }
    const outcome = resolveAttack(setup({ symbols: mods.symbols, abilities: [book] as AttackSetup['abilities'] }), roll)
    expect(outcome.physical).toBe(4)
  })

  it('leaves the Damage of a Spell to the GM, with a note', () => {
    const mods = effectMods([], ['wilt', 'tether'], true)
    expect(mods.irreducible).toBe(0)
    expect(mods.notes.map((n) => n.key)).toEqual(['wiltSpell', 'tether'])
  })
})
