import { describe, expect, it } from 'vitest'
import { parseCondition, suggest } from './conditions'
import {
  lineCells,
  printedTerrain,
  range,
  reachable,
  rotate,
  sight,
  terrainOf,
  type Board,
  type BoardFigure,
  type Terrain,
} from './grid'
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

/** The same board with Terrain tokens on some squares. */
function withTerrain(b: Board, cells: Record<string, Terrain>): Board {
  return { ...b, terrain: new Map(Object.entries(cells)) }
}

describe('terrain', () => {
  it('knows a Terrain token by its catalogue type', () => {
    expect(terrainOf('um1:token/terrain/obstructing-long')).toBe('obstructing')
    expect(terrainOf('um1:token/terrain/water-long-dark')).toBe('water')
    expect(terrainOf('um1:token/totem')).toBeUndefined()
  })

  it('goes around Obstructing and Dangerous Terrain, and pays 2 to leave Hindering Terrain', () => {
    const sickle = fig('sickle-1', 'combatant', 0, 0)
    const b = withTerrain(board(sickle), { '1,0': 'obstructing', '0,1': 'hindering', '2,1': 'dangerous' })
    const cost = new Map(reachable(b, sickle, 4).map((s) => [`${s.at.x},${s.at.y}`, s.cost]))
    expect(cost.has('1,0')).toBe(false)
    expect(cost.has('2,1')).toBe(false)
    expect(cost.get('0,1')).toBe(1)
    // Out of the Hindering square: 1 to get in, 2 to leave
    expect(cost.get('1,1')).toBe(3)
  })
})

describe('line of sight', () => {
  it('crosses the squares between the centres, offering both squares at a corner', () => {
    expect(lineCells({ x: 0, y: 0 }, { x: 3, y: 0 })).toEqual([[{ x: 1, y: 0 }], [{ x: 2, y: 0 }]])
    expect(lineCells({ x: 0, y: 0 }, { x: 2, y: 2 })).toEqual([
      [
        { x: 1, y: 0 },
        { x: 0, y: 1 },
      ],
      [{ x: 1, y: 1 }],
      [
        { x: 2, y: 1 },
        { x: 1, y: 2 },
      ],
    ])
  })

  it('stops at Obstructing Terrain, the edge of the tiles and opponents, not at allies', () => {
    const sickle = fig('sickle-1', 'combatant', 0, 2)
    const rook = fig('adventurer:Rook', 'adventurer', 4, 2)
    expect(sight(board(sickle, rook), sickle, rook)).toEqual({ clear: true, penalty: 0 })
    expect(sight(withTerrain(board(sickle, rook), { '2,2': 'obstructing' }), sickle, rook).clear).toBe(false)
    const blocker = fig('adventurer:Remi', 'adventurer', 2, 2)
    expect(sight(board(sickle, rook, blocker), sickle, rook).clear).toBe(false)
    // A Spell's SOI sees past figures
    expect(sight(board(sickle, rook, blocker), sickle, rook, true).clear).toBe(true)
    const ally = fig('sickle-2', 'combatant', 2, 2)
    expect(sight(board(sickle, rook, ally), sickle, rook)).toEqual({ clear: true, penalty: -1 })
    expect(sight(withTerrain(board(sickle, rook, ally), { '3,2': 'hindering' }), sickle, rook).penalty).toBe(-2)
    // Off the tiles: no Line of Sight
    const far = fig('adventurer:Zeke', 'adventurer', 0, 5)
    const holed = board(sickle, far)
    holed.squares.delete('0,4')
    expect(sight(holed, sickle, far).clear).toBe(false)
  })

  it('does not suggest a target hidden behind Obstructing Terrain', () => {
    const sickle = fig('sickle-1', 'combatant', 0, 2)
    const hidden = fig('adventurer:Rook', 'adventurer', 2, 2)
    const seen = fig('adventurer:Remi', 'adventurer', 0, 5)
    const b = withTerrain(board(sickle, hidden, seen), { '1,2': 'obstructing' })
    const [advice] = planTurn(['[Ranged] 4 ATK the nearest opponent.'], { board: b, acting: sickle })
    expect(advice?.[0]).toMatchObject({ kind: 'attack', target: { id: 'adventurer:Remi' } })
    const condition = parseCondition('Is there an opponent within Line of Sight?')!
    expect(suggest(condition, withTerrain(board(sickle, hidden), { '1,2': 'obstructing' }), sickle, 4)?.answer).toBe(
      false,
    )
  })
})

describe('printed terrain', () => {
  it('turns a square of the tile clockwise with the tile', () => {
    expect(rotate(1, 0, 0)).toEqual({ x: 1, y: 0 })
    expect(rotate(1, 0, 90)).toEqual({ x: 5, y: 1 })
    expect(rotate(1, 0, 180)).toEqual({ x: 4, y: 5 })
    expect(rotate(1, 0, 270)).toEqual({ x: 0, y: 4 })
  })

  it('reads the survey of a laid tile onto the board, special squares aside', () => {
    const rows = ['o.....', '.....s', '......', '......', '......', 'd....w']
    const terrain = printedTerrain({ x: 6, y: 0, rotation: 90, printed: rows })
    expect(Object.fromEntries(terrain)).toEqual({ '11,0': 'obstructing', '6,0': 'dangerous', '6,5': 'water' })
  })
})
