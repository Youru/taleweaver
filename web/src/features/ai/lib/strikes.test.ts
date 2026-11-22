import { describe, expect, it } from 'vitest'
import type { LogEntry } from '@/features/games'
import { planTurn } from './instructions'
import { isOpen, stepKey, strikesOfTurn } from './strikes'

const turn: LogEntry = { code: 'turn' }
const told = (text: string): LogEntry => ({ code: 'aiInstruction', text: { en: text }, args: { instance: 'imp-1' } })
const done = (step: string, target = 'adventurer:Rook'): LogEntry => ({
  code: 'attackMissed',
  args: { attacker: 'imp-1', target, step },
})

describe('the Attacks the AI asked for', () => {
  it('are closed by their line in the journal', () => {
    const log = [turn, told('Make a [Melee] ATK. Make a [Melee] Follow Up ATK against the same target.')]
    const open = strikesOfTurn(log)
    expect(open[0]!.id).toBe('1:0')
    expect(isOpen(open[0]!)).toBe(true)

    const after = strikesOfTurn([...log, done('1:0:0')])
    expect(isOpen(after[0]!)).toBe(false)
    for (const other of after.slice(1)) expect(isOpen(other)).toBe(true)
  })

  it('count the Attacks of "Make 2 ATKS"', () => {
    const log = [turn, told('Make 2 [Melee] ATKS.')]
    const once = strikesOfTurn([...log, done('1:0:0')])[0]!
    expect(isOpen(once)).toBe(true)
    expect(stepKey(once, 'adventurer:Rook')).toBe('1:0:1')
    expect(isOpen(strikesOfTurn([...log, done('1:0:0'), done('1:0:1')])[0]!)).toBe(false)
  })

  it('go by target when it is one against each', () => {
    const log = [turn, told('Make a separate [Melee] ATK against each opponent within REACH 2.')]
    const strike = strikesOfTurn([...log, done('1:0:adventurer:Rook')])[0]!
    expect(isOpen(strike, 'adventurer:Rook')).toBe(false)
    expect(isOpen(strike, 'adventurer:Zeke')).toBe(true)
    expect(stepKey(strike, 'adventurer:Zeke')).toBe('1:0:adventurer:Zeke')
  })

  it('keep a wording the helper does not read', () => {
    expect(strikesOfTurn([turn, told('Perform an ATK.')])).toHaveLength(1)
    expect(strikesOfTurn([turn, told('Discard an Effect.')])).toHaveLength(0)
  })

  it('are only those of the turn under way', () => {
    expect(strikesOfTurn([turn, told('Make a [Melee] ATK.'), turn])).toHaveLength(0)
  })

  it('make "the same target" the one really attacked', () => {
    const squares = new Set(['0,0', '1,0', '2,0'])
    const at = (id: string, side: string, x: number, initiative: number) => ({
      id,
      name: id,
      side,
      position: { x, y: 0 },
      size: 1,
      damage: 0,
      initiative,
    })
    const acting = at('imp-1', 'combatant', 1, 0)
    const board = { squares, figures: [acting, at('rook', 'adventurer', 0, 1), at('zeke', 'adventurer', 2, 2)] }
    const texts = ['Make a [Melee] ATK.', 'Make a [Melee] ATK against the same target.']
    const target = (struck?: string[][]) => planTurn(texts, { board, acting, struck })[1]![0]

    expect(target()).toMatchObject({ target: { id: 'rook' } })
    expect(target([['zeke']])).toMatchObject({ target: { id: 'zeke' } })
  })
})
