import type { Dice, DieFace } from '../api/types'

/** The core box dice (packs/um1/dice.json), for tests. */
function face(value: number, shield = 0, book = 0, star = 0, skull = false): DieFace {
  return { value, shield, book, star, skull }
}
const shields = (x: number) => [
  face(x),
  face(x + 1),
  face(x + 2, 1),
  face(x + 3, 1, 1),
  face(x + 4, 2, 1),
  face(x + 5, 2, 1, 1),
]
const books = (x: number) => [
  face(x),
  face(x + 1),
  face(x + 2, 0, 1),
  face(x + 4, 1, 1),
  face(x + 5, 1, 2),
  face(x + 6, 1, 2, 1),
]
const die = (id: string, faces: DieFace[]) => ({ id, name: { en: id }, faces })

export const testDice: Dice = {
  purple: die('purple', books(1)),
  white: die('white', shields(2)),
  orange: die('orange', books(2)),
  teal: die('teal', shields(3)),
  black: die('black', [face(0, 4), face(0, 3, 1), face(0, 2, 2), face(0, 1, 3), face(0, 0, 4), face(0, 0, 0, 0, true)]),
}
