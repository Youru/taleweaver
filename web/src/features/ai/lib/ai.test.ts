import { describe, expect, it } from 'vitest'
import { parseCondition, suggest } from './conditions'
import { range, reachable, type Board, type BoardFigure } from './grid'
import { parseInstruction, planTurn } from './instructions'

/** A 12×6 board (two tiles) with nothing on it. */
function board(...figures: BoardFigure[]): Board {
  const squares = new Set<string>()
  for (let x = 0; x < 12; x++) for (let y = 0; y < 6; y++) squares.add(`${x},${y}`)
  return { squares, figures }
}

let order = 0
const fig = (id: string, side: BoardFigure['side'], x: number, y: number, extra: Partial<BoardFigure> = {}) => ({
  id,
  name: id,
  side,
  position: { x, y },
  size: 1,
  damage: 0,
  initiative: order++,
  ...extra,
})

describe('range', () => {
  it('counts diagonals, base to base for large figures', () => {
    expect(range({ x: 0, y: 0 }, 1, { x: 3, y: 2 }, 1)).toBe(3)
    expect(range({ x: 0, y: 0 }, 2, { x: 2, y: 1 }, 1)).toBe(1)
  })
})

describe('reachable', () => {
  it('moves orthogonally, never through an opponent, never stopping on an ally', () => {
    const sickle = fig('sickle-1', 'combatant', 0, 0)
    const rook = fig('adventurer:Rook', 'adventurer', 1, 0)
    const ally = fig('sickle-2', 'combatant', 0, 1)
    const squares = reachable(board(sickle, rook, ally), sickle, 2)
    const at = squares.map((s) => `${s.at.x},${s.at.y}`)
    expect(at).not.toContain('1,0')
    expect(at).not.toContain('0,1')
    // Through the ally: leaving its square costs 2, so (0,2) needs 3 and (1,1) needs 3 as well.
    expect(at).toEqual(['0,0'])
  })

  it('counts each step for 1 on open ground', () => {
    const sickle = fig('sickle-1', 'combatant', 5, 2)
    const squares = reachable(board(sickle), sickle, 2)
    expect(squares.find((s) => s.at.x === 6 && s.at.y === 3)?.cost).toBe(2)
    expect(squares.some((s) => s.at.x === 7 && s.at.y === 3)).toBe(false)
  })
})

describe('conditions', () => {
  it('reads the usual wordings', () => {
    expect(parseCondition('Is there an opponent adjacent?')).toEqual({
      kind: 'opponentsWithin',
      max: 1,
      min: 0,
      count: 1,
    })
    expect(parseCondition('Is there more than one opponent adjacent?')).toMatchObject({ count: 2 })
    expect(parseCondition('Is there an opponent within Range 4?')).toMatchObject({ max: 4 })
    expect(parseCondition('Is there a non-adjacent opponent within Range 8?')).toMatchObject({ min: 2, max: 8 })
    expect(parseCondition('Is there an opponent within SOI?')).toMatchObject({ max: 4 })
    expect(parseCondition('Can it Move and Attack an opponent within Reach 2?')).toEqual({
      kind: 'moveAndAttack',
      range: 2,
    })
    expect(parseCondition('Can the Enslaved Spirit Move and Attack an opponent?')).toEqual({
      kind: 'moveAndAttack',
      range: 1,
    })
    expect(parseCondition('Otherwise:')).toEqual({ kind: 'always' })
    expect(parseCondition('Is there an opponent adjacent and an ally within 6 spaces?')).toBeUndefined()
    expect(parseCondition('Are there any Tentacle tokens on the board?')).toBeUndefined()
  })

  it('answers from the positions and says why', () => {
    const sickle = fig('sickle-1', 'combatant', 0, 0)
    const rook = fig('adventurer:Rook', 'adventurer', 3, 2)
    const b = board(sickle, rook)
    expect(suggest(parseCondition('Is there an opponent adjacent?')!, b, sickle, 4)).toEqual({
      answer: false,
      reason: { key: 'nearest', args: { name: 'adventurer:Rook', n: 3 } },
    })
    expect(suggest(parseCondition('Is there an opponent within Range 4?')!, b, sickle, 4)?.answer).toBe(true)
    // 4 orthogonal steps bring it next to Rook (2,1 or 3,1…).
    expect(suggest(parseCondition('Can it Move and Attack an opponent?')!, b, sickle, 4)?.answer).toBe(true)
    expect(suggest(parseCondition('Can it Move and Attack an opponent?')!, b, sickle, 2)?.answer).toBe(false)
  })
})

describe('instructions', () => {
  it('splits an instruction into its Attacks and moves', () => {
    expect(
      parseInstruction('Make a [Melee] ATK. THEN: MOVE to be at up to Range 4 from an adjacent opponent.'),
    ).toEqual([
      { kind: 'attack', spell: false, range: 1, target: { kind: 'any' } },
      { kind: 'move', rule: { kind: 'range', n: 4, upTo: true, pick: 'nearest', withinMovement: false } },
    ])
    expect(parseInstruction('Make a [Ranged] 4 ATK against the farthest opponent within Range.')).toEqual([
      { kind: 'attack', spell: false, range: 4, target: { kind: 'farthest' } },
    ])
    expect(parseInstruction('Make a [Melee] ATK against the opponent with the most Damage.')[0]).toMatchObject({
      target: { kind: 'criterion', text: 'with the most damage' },
    })
    expect(parseInstruction('If the Attack hit, move 4 spaces away from the target.')).toEqual([
      { kind: 'move', rule: { kind: 'away', n: 4 } },
    ])
    expect(parseInstruction('FU: Move the target up to 2 spaces towards the farthest ally.')).toEqual([])
  })

  it('moves towards the nearest opponent, then attacks from where it ends', () => {
    order = 0
    const sickle = fig('sickle-1', 'combatant', 0, 0)
    const rook = fig('adventurer:Rook', 'adventurer', 6, 0)
    const zeke = fig('adventurer:Zeke', 'adventurer', 0, 5)
    const [move, attack] = planTurn(['MOVE towards the nearest opponent.', 'Make a [Melee] ATK.'], {
      board: board(sickle, rook, zeke),
      acting: sickle,
      movement: 4,
    })
    expect(move).toEqual([
      { kind: 'move', from: { x: 0, y: 0 }, to: { x: 0, y: 4 }, cost: 4, toward: 'adventurer:Zeke', criterion: false },
    ])
    expect(attack?.[0]).toMatchObject({ kind: 'attack', target: { id: 'adventurer:Zeke', range: 1 } })
  })

  it('keeps its distance with "up to Range N", and stays when "within" already holds', () => {
    order = 0
    const animate = fig('animate-1', 'combatant', 3, 2)
    const rook = fig('adventurer:Rook', 'adventurer', 4, 2)
    const b = board(animate, rook)
    const upTo = planTurn(['MOVE to be at up to Range 4 from the nearest opponent.'], {
      board: b,
      acting: animate,
      movement: 6,
    })[0]?.[0]
    if (upTo?.kind !== 'move') throw new Error('a move was expected')
    expect(range(upTo.to, 1, rook.position, 1)).toBe(4)
    const within = planTurn(['MOVE to be within Range 4 of the nearest opponent.'], {
      board: b,
      acting: animate,
      movement: 6,
    })[0]?.[0]
    expect(within).toEqual({ kind: 'stay', at: { x: 3, y: 2 } })
  })

  it('breaks ties with the Initiative Track', () => {
    order = 0
    const zeke = fig('adventurer:Zeke', 'adventurer', 1, 0)
    const rook = fig('adventurer:Rook', 'adventurer', 0, 1)
    const sickle = fig('sickle-1', 'combatant', 0, 0)
    const attack = planTurn(['Make a [Melee] ATK.'], {
      board: board(sickle, rook, zeke),
      acting: sickle,
      movement: 4,
    })[0]?.[0]
    expect(attack).toMatchObject({ target: { id: 'adventurer:Zeke' } })
  })
})
