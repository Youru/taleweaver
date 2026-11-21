import { describe, expect, it } from 'vitest'
import type { BoardLayout } from '@/features/table'
import type { Board, BoardFigure } from './grid'
import { seenTokens } from './totems'

const rook: BoardFigure = {
  id: 'adventurer:Rook',
  name: 'Rook',
  side: 'adventurer',
  position: { x: 0, y: 2 },
  size: 1,
  damage: 0,
  initiative: 0,
}

/** A 12×6 board cut by a wall of Obstructing Terrain on column 5, unless `open`. */
function board(open = false): Board {
  const squares = new Set<string>()
  for (let x = 0; x < 12; x++) for (let y = 0; y < 6; y++) squares.add(`${x},${y}`)
  const terrain = new Map<string, 'obstructing'>()
  if (!open) for (let y = 0; y < 6; y++) terrain.set(`5,${y}`, 'obstructing')
  return { squares, figures: [rook], terrain }
}

const token = (key: string, x: number, y: number): BoardLayout['tokens'][number] => ({
  id: key,
  key,
  x,
  y,
  w: 1,
  h: 1,
  turned: false,
  round: true,
})
const layout = (...tokens: BoardLayout['tokens']) => ({ tokens }) as unknown as BoardLayout

describe('seenTokens', () => {
  it('finds a Totem an adventurer sees, whatever the range', () => {
    expect(seenTokens(board(true), layout(token('totem:blue', 11, 5)), () => undefined)).toEqual([
      { key: 'totem:blue', by: 'Rook' },
    ])
  })

  it('leaves a Totem behind Obstructing Terrain', () => {
    expect(seenTokens(board(), layout(token('totem:red', 9, 2)), () => undefined)).toEqual([])
  })

  it('follows the mode the scenario gives, and ignores other tokens', () => {
    const tokens = layout(token('totem:blue', 3, 2), token('loot:blue', 2, 2), token('objective:red', 1, 1))

    const seen = seenTokens(board(), tokens, (key) =>
      key === 'totem:blue' ? 'adjacent' : key === 'objective:red' ? 'lineOfSight' : undefined,
    )

    expect(seen.map((s) => s.key)).toEqual(['objective:red'])
  })
})
