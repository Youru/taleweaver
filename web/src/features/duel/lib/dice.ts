import type { Dice, DieFace } from '../api/types'

/** The colours a combat or conviction pool can hold, weakest first (the black die only empowers). */
export const dieColours = ['purple', 'white', 'orange', 'teal', 'red', 'green', 'grey', 'blue'] as const

export interface Symbols {
  shield: number
  book: number
  star: number
}

/** One way a pool can land, with its probability. */
export interface RollOutcome extends Symbols {
  total: number
  skull: boolean
  p: number
}

/**
 * Every result of rolling these dice together: total, symbols and skull, with their probability.
 * Undefined when a colour is unknown (a "*" from a card, or a die missing from the packs).
 */
export function rollDistribution(colours: readonly string[], dice: Dice): RollOutcome[] | undefined {
  let outcomes: RollOutcome[] = [{ total: 0, shield: 0, book: 0, star: 0, skull: false, p: 1 }]
  for (const colour of colours) {
    const faces = dice[colour]?.faces
    if (!faces?.length) return undefined
    const merged = new Map<string, RollOutcome>()
    for (const outcome of outcomes) {
      for (const face of faces) {
        const next = combine(outcome, face, outcome.p / faces.length)
        const key = `${next.total}|${next.shield}|${next.book}|${next.star}|${next.skull}`
        const known = merged.get(key)
        if (known) known.p += next.p
        else merged.set(key, next)
      }
    }
    outcomes = [...merged.values()]
  }
  return outcomes
}

function combine(outcome: RollOutcome, face: DieFace, p: number): RollOutcome {
  return {
    total: outcome.total + face.value,
    shield: outcome.shield + face.shield,
    book: outcome.book + face.book,
    star: outcome.star + face.star,
    skull: outcome.skull || face.skull,
    p,
  }
}
