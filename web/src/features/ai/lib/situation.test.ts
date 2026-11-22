import { describe, expect, it } from 'vitest'
import type { Board, BoardFigure, Terrain } from './grid'
import { crossesOpposite, situationOf, type Weapon } from './situation'

const squares = new Set(Array.from({ length: 100 }, (_, i) => `${i % 10},${Math.floor(i / 10)}`))
const figure = (id: string, side: string, x: number, y: number, size = 1, effects: string[] = []): BoardFigure => ({
  id,
  name: id,
  side,
  position: { x, y },
  size,
  damage: 0,
  initiative: 0,
  effects,
})
const board = (figures: BoardFigure[], terrain?: Record<string, Terrain>): Board => ({
  squares,
  figures,
  terrain: new Map(Object.entries(terrain ?? {})),
})
const melee = () => ({ melee: true, reach: 1 })
const target = figure('zeke', 'adventurer', 4, 4)
const at = (x: number, y: number, effects: string[] = []) => figure('a', 'combatant', x, y, 1, effects)
const partner = (x: number, y: number, effects: string[] = []) => figure('b', 'combatant', x, y, 1, effects)
const flanks = (figures: BoardFigure[], weapon: (id: string) => Weapon | undefined = melee, attack?: Weapon) =>
  situationOf(board(figures), 'a', 'zeke', weapon, attack)?.flank

describe('flanking (rulebook p. 50)', () => {
  it('is from opposite sides or opposite corners', () => {
    expect(flanks([target, at(3, 4), partner(5, 4)])).toBe(true)
    expect(flanks([target, at(4, 3), partner(4, 5)])).toBe(true)
    expect(flanks([target, at(3, 3), partner(5, 5)])).toBe(true)
    expect(flanks([target, at(5, 3), partner(3, 5)])).toBe(true)
  })

  it('is not from the same side, a side and a corner, or two neighbouring sides', () => {
    expect(flanks([target, at(4, 5), partner(5, 5)])).toBe(false)
    expect(flanks([target, at(3, 4), partner(5, 5)])).toBe(false)
    expect(flanks([target, at(3, 4), partner(4, 3)])).toBe(false)
    expect(flanks([target, at(3, 3), partner(5, 3)])).toBe(false)
  })

  it('needs an ally, not an opponent, opposite', () => {
    expect(flanks([target, at(3, 4), figure('b', 'adventurer', 5, 4)])).toBe(false)
    expect(flanks([target, at(3, 4)])).toBe(false)
  })

  it('needs Melee on both, within its Reach', () => {
    expect(flanks([target, at(3, 4), partner(5, 4)], (id) => (id === 'a' ? melee() : undefined))).toBe(false)
    expect(flanks([target, at(3, 4), partner(5, 4)], melee, { melee: false, reach: 4 })).toBe(false)
    expect(flanks([target, at(2, 4), partner(5, 4)])).toBe(false)
    expect(flanks([target, at(2, 4), partner(5, 4)], (id) => ({ melee: true, reach: id === 'a' ? 2 : 1 }))).toBe(true)
  })

  it('needs both able to attack', () => {
    expect(flanks([target, at(3, 4), partner(5, 4, ['paralyze'])])).toBe(false)
  })

  it('has more spots around a large base', () => {
    const large = figure('zeke', 'adventurer', 4, 4, 2)
    expect(flanks([large, at(3, 4), partner(6, 5)])).toBe(true)
    expect(flanks([large, at(3, 4), partner(4, 6)])).toBe(false)
    expect(crossesOpposite({ x: 3, y: 5 }, { x: 6, y: 5 }, { x: 4, y: 4 }, 2)).toBe(true)
  })
})

describe('the situation of an Attack', () => {
  const read = (figures: BoardFigure[], terrain?: Record<string, Terrain>) =>
    situationOf(board(figures, terrain), 'a', 'zeke', melee)

  it('tells an ally in the way from Hindering Terrain', () => {
    expect(read([target, at(1, 4), partner(2, 4)])).toMatchObject({ throughAlly: true, hindering: false })
    expect(read([target, at(1, 4)], { '4,4': 'hindering' })).toMatchObject({ throughAlly: false, hindering: true })
  })

  it('says when the target is out of sight, or in Water', () => {
    expect(read([target, at(1, 4)], { '2,4': 'obstructing' })?.clear).toBe(false)
    expect(read([target, at(3, 4)], { '4,4': 'water' })?.inWater).toBe(true)
  })

  it('says nothing of a figure off the board', () => {
    expect(read([target])).toBeUndefined()
  })
})
