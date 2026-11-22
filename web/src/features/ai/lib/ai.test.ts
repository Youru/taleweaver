import { describe, expect, it } from 'vitest'
import { parseCondition, suggest } from './conditions'
import {
  inReach,
  lineCells,
  printedElevation,
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
      { kind: 'attack', spell: false, range: 1, melee: true, target: { kind: 'any' } },
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
    expect(sight(board(sickle, rook, ally), sickle, rook)).toEqual({ clear: true, penalty: -1, ally: true })
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

describe('effects', () => {
  const loa = fig('loa-1', 'combatant', 0, 0)
  const rook = fig('adventurer:Rook', 'adventurer', 3, 0, { effects: ['darkness'] })
  const zeke = fig('adventurer:Zeke', 'adventurer', 1, 2)
  const ally = fig('earth-loa-1', 'combatant', 0, 2, { effects: ['barrier'] })

  it('reads the conditions about Effects within SOI', () => {
    expect(parseCondition('Is there a figure within SOI with an Effect?')).toEqual({
      kind: 'effectWithin',
      who: 'figure',
      has: true,
    })
    expect(parseCondition('Is there a figure with an Effect within SOI?')).toMatchObject({ kind: 'effectWithin' })
    expect(parseCondition('Is there another ally within SOI without Barrier?')).toEqual({
      kind: 'effectWithin',
      who: 'ally',
      effect: 'barrier',
      has: false,
    })
  })

  it('answers them from the Effects the GM reported', () => {
    const any = parseCondition('Is there a figure within SOI with an Effect?')!
    expect(suggest(any, board(loa, rook, zeke), loa, 4)?.answer).toBe(true)
    expect(suggest(any, board(loa, zeke), loa, 4)?.answer).toBe(false)
    const noBarrier = parseCondition('Is there another ally within SOI without Barrier?')!
    expect(suggest(noBarrier, board(loa, ally), loa, 4)?.answer).toBe(false)
    expect(suggest(noBarrier, board(loa, { ...ally, effects: [] }), loa, 4)?.answer).toBe(true)
  })

  it('aims first at the opponent with (or without) the Effect the card names', () => {
    const plan = (text: string) => planTurn([text], { board: board(loa, rook, zeke), acting: loa, movement: 0 })[0]?.[0]
    expect(plan('Make a [Ranged] 6 ATK, prioritizing an opponent with Darkness.')).toMatchObject({
      target: { id: 'adventurer:Rook' },
    })
    expect(
      plan('Cast a SPELL 6, prioritizing the opponent with the lowest Conviction Value without Darkness.'),
    ).toBeDefined()
    expect(plan('Cast a SPELL 6, prioritizing an opponent without Darkness. Inflict Darkness.')).toMatchObject({
      target: { id: 'adventurer:Zeke' },
    })
    // A second priority the board does not show stays with the GM
    expect(
      plan(
        'Make a [Ranged] 6 ATK, prioritizing an opponent with Darkness and a secondary priority of the most Damage.',
      ),
    ).toMatchObject({ target: undefined, candidates: [{ id: 'adventurer:Rook' }, { id: 'adventurer:Zeke' }] })
  })
})

/** The board with Elevation: `rows` of digits from the top-left square (the rest is at 1). */
function withElevation(b: Board, rows: string[]): Board {
  const elevation = new Map<string, number>()
  rows.forEach((row, y) => [...row].forEach((d, x) => elevation.set(`${x},${y}`, Number(d))))
  return { ...b, elevation }
}

describe('elevation', () => {
  it('reads the printed Elevation of a laid tile, turned with it', () => {
    const rows = ['311111', '111111', '111111', '111111', '111111', '111111']
    const levels = printedElevation({ x: 6, y: 0, rotation: 90, elevation: rows })
    expect(levels.get('11,0')).toBe(3)
    expect(levels.get('6,0')).toBe(1)
  })

  it('sees down from the edge of a height, with +1 to the Attack roll, but not from further back', () => {
    const edge = fig('sickle-1', 'combatant', 1, 0)
    const back = fig('sickle-2', 'combatant', 0, 0)
    const below = fig('adventurer:Rook', 'adventurer', 4, 0)
    // Squares 0 and 1 at 3, the rest at 1
    const b = withElevation(board(edge, below), ['33'])
    expect(sight(b, edge, below)).toEqual({ clear: true, penalty: 0, higher: true })
    expect(sight(b, below, edge)).toEqual({ clear: true, penalty: 0 })
    expect(sight(withElevation(board(back, below), ['33']), back, below).clear).toBe(false)
    // Same level: through lower squares, not over higher ones
    const left = fig('sickle-3', 'combatant', 0, 0)
    const right = fig('adventurer:Remi', 'adventurer', 4, 0)
    expect(sight(withElevation(board(left, right), ['22122']), left, right).clear).toBe(true)
    expect(sight(withElevation(board(left, right), ['11211']), left, right).clear).toBe(false)
    // A Spell's SOI ignores Elevation
    expect(sight(withElevation(board(left, right), ['11211']), left, right, true).clear).toBe(true)
  })

  it('ignores the figures below the higher level when looking down', () => {
    const edge = fig('sickle-1', 'combatant', 1, 0)
    const target = fig('adventurer:Rook', 'adventurer', 4, 0)
    const between = fig('adventurer:Remi', 'adventurer', 3, 0)
    expect(sight(withElevation(board(edge, target, between), ['33']), edge, target).clear).toBe(true)
    expect(sight(board(edge, target, between), edge, target).clear).toBe(false)
  })

  it('makes side by side figures Adjacent only with a change of 1 or less, and shortens Reach', () => {
    const sickle = fig('sickle-1', 'combatant', 0, 0)
    const rook = fig('adventurer:Rook', 'adventurer', 1, 0)
    const far = fig('adventurer:Remi', 'adventurer', 2, 0)
    expect(inReach(withElevation(board(sickle, rook), ['12']), sickle, rook, 1)).toBe(true)
    expect(inReach(withElevation(board(sickle, rook), ['13']), sickle, rook, 1)).toBe(false)
    expect(inReach(withElevation(board(sickle, rook), ['13']), sickle, rook, 2)).toBe(true)
    expect(inReach(withElevation(board(sickle, far), ['113']), sickle, far, 2)).toBe(false)
    const b = withElevation(board(sickle, rook), ['13'])
    const [melee] = planTurn(['Make a [Melee] ATK on the nearest opponent.'], { board: b, acting: sickle })
    expect(melee?.[0]).toMatchObject({ kind: 'attack', candidates: [] })
    const [ranged] = planTurn(['Make a [Ranged] 3 ATK on the nearest opponent.'], { board: b, acting: sickle })
    expect(ranged?.[0]).toMatchObject({ kind: 'attack', target: { id: 'adventurer:Rook' } })
    expect(suggest(parseCondition('Is there an opponent adjacent?')!, b, sickle, 0)?.answer).toBe(false)
  })

  it('climbs a change of 1, drops a change of 2, never more', () => {
    const sickle = fig('sickle-1', 'combatant', 1, 0)
    const reached = (rows: string[]) =>
      new Set(reachable(withElevation(board(sickle), rows), sickle, 1).map((s) => `${s.at.x},${s.at.y}`))
    expect(reached(['232'])).toEqual(new Set(['1,0', '0,0', '2,0', '1,1']))
    expect(reached(['141']).has('0,0')).toBe(false)
    expect(reached(['242']).has('0,0')).toBe(true)
    expect(reached(['141']).has('1,1')).toBe(false)
  })
})
