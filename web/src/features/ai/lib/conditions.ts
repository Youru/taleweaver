import { between, reachable, sight, type Board, type BoardFigure } from './grid'

/** SOI has a base Range of 4 (rulebook p. 43). */
export const SOI = 4
/** Line of Sight is not known: any figure on the revealed board counts. */
const ANYWHERE = 999

/** An AI condition the helper can answer from the positions on the board. */
export type Condition =
  | { kind: 'always' }
  /** At least `count` opponents within `max` squares (and at least `min` away), seen when `sight` says so. */
  | { kind: 'opponentsWithin'; max: number; min: number; count: number; sight?: 'attack' | 'soi' }
  /** An opponent within `range` after a move of the figure's Movement. */
  | { kind: 'moveAndAttack'; range: number }
  | { kind: 'moveTowardsAlly' }
  | { kind: 'hasDamage' }
  /**
   * A figure within SOI with (or without) an Effect: any Effect when `effect` is not given. `who`:
   * any figure (the acting one too), another ally, or an opponent.
   */
  | { kind: 'effectWithin'; who: 'figure' | 'ally' | 'opponent'; effect?: string; has: boolean }

/** The Effects of the rules (packs/um1/rules.json), as the cards name them. */
export const EFFECTS = [
  'barrier',
  'condemn',
  'courage',
  'darkness',
  'disease',
  'haste',
  'nemesis',
  'paralyze',
  'poison',
  'tether',
  'wilt',
]
const effectWord = new RegExp(`^(${EFFECTS.join('|')})$`)

const word = /(?:range|reach)\s*(\d+)/

/** Reads the printed condition ("Is there an opponent within Range 4?"); undefined when not understood. */
export function parseCondition(text: string): Condition | undefined {
  // "op onent": a frequent OCR slip on the cards.
  const t = text
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .replace(/op ?onent/g, 'opponent')
    .trim()
  if (/^otherwise/.test(t)) return { kind: 'always' }
  if (/^can [^?]*move and attack an opponent( within (range|reach) \d+)?\??$/.test(t)) {
    const m = word.exec(t)
    return { kind: 'moveAndAttack', range: m ? Number(m[1]) : 1 }
  }
  if (/^can [^?]*move (to be )?adjacent to an opponent\??$/.test(t)) return { kind: 'moveAndAttack', range: 1 }
  if (/^can [^?]*move towards an ally\??$/.test(t)) return { kind: 'moveTowardsAlly' }
  if (/^does [^?]* have damage\??$/.test(t)) return { kind: 'hasDamage' }
  // "Is there a figure within SOI with an Effect?", "Is there another ally within SOI without Barrier?"
  const effect =
    /^is there (?:a|an|another) (figure|ally|opponent) (?:within soi )?(with|without) (an effect|\w+)(?: within soi)?\??$/.exec(
      t,
    )
  if (effect && /within soi/.test(t)) {
    const [, who, has, what] = effect as unknown as [string, 'figure' | 'ally' | 'opponent', string, string]
    if (what === 'an effect') return { kind: 'effectWithin', who, has: has === 'with' }
    if (effectWord.test(what)) return { kind: 'effectWithin', who, effect: what, has: has === 'with' }
  }
  // Anything with a second clause or a criterion the board does not show is left to the GM.
  if (!/opponent/.test(t) || /\band\b|\bally\b|token|esper|effect|barrier|damage|health/.test(t)) return undefined
  const count = /more than one/.test(t) ? 2 : 1
  const min = /non-adjacent/.test(t) ? 2 : 0
  if (/adjacent\??$/.test(t)) return { kind: 'opponentsWithin', max: 1, min, count }
  const m = word.exec(t)
  if (m) return { kind: 'opponentsWithin', max: Number(m[1]), min, count }
  if (/within soi/.test(t)) return { kind: 'opponentsWithin', max: SOI, min, count, sight: 'soi' }
  if (/line of sight/.test(t)) return { kind: 'opponentsWithin', max: ANYWHERE, min, count, sight: 'attack' }
  return undefined
}

/** Why the helper suggests an answer: an i18n key under `ai.` and its arguments. */
export interface Reason {
  key: string
  args: Record<string, string | number>
}

export interface Suggestion {
  answer: boolean
  reason: Reason
}

export const opponentsOf = (board: Board, figure: BoardFigure) => board.figures.filter((f) => f.side !== figure.side)
export const alliesOf = (board: Board, figure: BoardFigure) =>
  board.figures.filter((f) => f.side === figure.side && f.id !== figure.id)

/** Figures sorted nearest first, then by initiative. */
export function byDistance(from: BoardFigure, figures: BoardFigure[]) {
  return figures
    .map((f) => ({ figure: f, range: between(from, f) }))
    .sort((a, b) => a.range - b.range || a.figure.initiative - b.figure.initiative)
}

const capitalize = (value: string) => value.charAt(0).toUpperCase() + value.slice(1)

const listed = (items: { figure: BoardFigure; range: number }[]) =>
  items.map((i) => `${i.figure.name} (${i.range})`).join(', ')

/** The answer the positions give, or undefined when the helper cannot tell. */
export function suggest(
  condition: Condition,
  board: Board,
  acting: BoardFigure,
  movement: number | undefined,
): Suggestion | undefined {
  switch (condition.kind) {
    case 'always':
      return undefined
    case 'hasDamage':
      return { answer: acting.damage > 0, reason: { key: 'damage', args: { n: acting.damage } } }
    case 'opponentsWithin': {
      const all = byDistance(acting, opponentsOf(board, acting))
      if (!all.length) return { answer: false, reason: { key: 'noOpponent', args: {} } }
      const seen = (o: BoardFigure) => !condition.sight || sight(board, acting, o, condition.sight === 'soi').clear
      const within = all.filter((o) => o.range <= condition.max && o.range >= condition.min && seen(o.figure))
      if (within.length >= condition.count)
        return { answer: true, reason: { key: 'within', args: { list: listed(within) } } }
      const nearest = all[0]!
      return { answer: false, reason: { key: 'nearest', args: { name: nearest.figure.name, n: nearest.range } } }
    }
    case 'effectWithin': {
      const pool =
        condition.who === 'figure'
          ? board.figures
          : condition.who === 'ally'
            ? alliesOf(board, acting)
            : opponentsOf(board, acting)
      const holds = (f: BoardFigure) => {
        const effects = f.effects ?? []
        const has = condition.effect ? effects.includes(condition.effect) : effects.length > 0
        return has === condition.has
      }
      const near = byDistance(acting, pool).filter(
        (o) => o.range <= SOI && (o.figure.id === acting.id || sight(board, acting, o.figure, true).clear),
      )
      const found = near.filter((o) => holds(o.figure))
      const effect = condition.effect ? capitalize(condition.effect) : ''
      const any = condition.effect ? '' : 'Any'
      if (found.length)
        return {
          answer: true,
          reason: { key: `${condition.has ? 'with' : 'without'}${any}Effect`, args: { effect, list: listed(found) } },
        }
      return {
        answer: false,
        reason: { key: `none${condition.has ? 'With' : 'Without'}${any}Effect`, args: { effect } },
      }
    }
    case 'moveAndAttack': {
      if (movement === undefined) return undefined
      const opponents = opponentsOf(board, acting)
      if (!opponents.length) return { answer: false, reason: { key: 'noOpponent', args: {} } }
      const squares = reachable(board, acting, movement)
      // From a square it can reach, within range and in Line of Sight
      const reached = byDistance(acting, opponents).filter((o) =>
        squares.some((s) => {
          const there = { ...acting, position: s.at }
          return between(there, o.figure) <= condition.range && sight(board, there, o.figure).clear
        }),
      )
      if (reached.length)
        return {
          answer: true,
          reason: { key: 'reachable', args: { list: listed(reached), movement, range: condition.range } },
        }
      return { answer: false, reason: { key: 'outOfReach', args: { movement, range: condition.range } } }
    }
    case 'moveTowardsAlly': {
      if (movement === undefined) return undefined
      const allies = alliesOf(board, acting)
      if (!allies.length) return { answer: false, reason: { key: 'noAlly', args: {} } }
      const squares = reachable(board, acting, movement)
      const closer = allies.some((a) =>
        squares.some((s) => between({ ...acting, position: s.at }, a) < between(acting, a)),
      )
      return { answer: closer, reason: { key: closer ? 'allyCloser' : 'allyBlocked', args: {} } }
    }
  }
}
