import type { Cell } from '@/features/games'
import { byDistance, EFFECTS, opponentsOf, SOI } from './conditions'
import { between, inReach, reachable, sight, type Board, type BoardFigure, type Reach } from './grid'

/** Who an Attack or a Spell goes to, as the AI step words it. */
export type TargetRule =
  | { kind: 'nearest' | 'farthest' | 'same' | 'each' | 'any' }
  /** A criterion the board does not show ("the opponent with the most Damage"): the GM decides. */
  | { kind: 'criterion'; text: string }
  /**
   * Opponents with (or without) an Effect first, as the GM reported them ("prioritizing an opponent
   * with Darkness"); `rest`: what else the card says, left to the GM.
   */
  | { kind: 'effect'; effect: string; has: boolean; rest?: string }
  /** "the opponent with the most Effects". */
  | { kind: 'mostEffects' }

/** How a MOVE is worded on the card (rulebook p. 62). */
export type MoveRule =
  | { kind: 'towards'; who: 'opponent' | 'ally'; pick: 'nearest' | 'farthest' }
  /** "at up to Range N" (go as far as N), or "within Range N" (move only if farther). */
  | { kind: 'range'; n: number; upTo: boolean; pick: 'nearest' | 'farthest' | 'criterion'; withinMovement: boolean }
  | { kind: 'adjacentMany' }
  | { kind: 'flee' }
  | { kind: 'away'; n: number }

export type Action =
  /** `melee`: a Reach (or Adjacent), shortened by Elevation changes (rulebook p. 22). */
  | { kind: 'attack'; spell: boolean; range: number; melee?: boolean; times?: number; target: TargetRule }
  | { kind: 'move'; rule: MoveRule }

/** The sentences of an instruction ("Make a [Melee] ATK. THEN: MOVE …"), questions left out. */
function sentences(text: string): string[] {
  return text
    .split(/\bTHEN:?|•|(?<=\.)\s+/)
    .map((s) => s.trim())
    .filter((s) => s && !s.endsWith('?'))
}

function targetRule(rest: string): TargetRule {
  if (/\b(each|all)\b|different/.test(rest)) return { kind: 'each' }
  if (/same (target|opponent)/.test(rest)) return { kind: 'same' }
  if (/farthest/.test(rest)) return { kind: 'farthest' }
  if (/nearest/.test(rest)) return { kind: 'nearest' }
  const criterion = /(?:against|prioritizing) (?:the |an )?(?:adjacent )?opponent (with .+|without .+)/.exec(rest)
  if (criterion?.[1]) {
    const text = criterion[1].replace(/\.$/, '')
    if (/^with the most effects\b/.test(text)) return { kind: 'mostEffects' }
    const effect = /^(with|without) (\w+)\b(.*)$/.exec(text)
    if (effect && EFFECTS.includes(effect[2]!)) {
      const rest = effect[3]!.replace(/^[\s,]+/, '').trim()
      return { kind: 'effect', effect: effect[2]!, has: effect[1] === 'with', ...(rest ? { rest: text } : {}) }
    }
    return { kind: 'criterion', text }
  }
  return { kind: 'any' }
}

function parseMove(s: string): MoveRule | undefined {
  // The figure itself moves ("MOVE …", "If the Attack hit, move …"); not "FU: Move the target …".
  if (!/^(if [^,]*, )?move\b/.test(s) || /move action/.test(s)) return undefined
  const away = /(\d+) spaces (?:farther|away) from the target/.exec(s)
  if (away) return { kind: 'away', n: Number(away[1]) }
  if (/adjacent to as many opponents/.test(s)) return { kind: 'adjacentMany' }
  if (/farthest space (possible )?from (any )?opponents?/.test(s)) return { kind: 'flee' }
  const towards = /towards the (nearest|farthest) (opponent|ally)\b(?!\s+adjacent)/.exec(s)
  if (towards)
    return { kind: 'towards', pick: towards[1] as 'nearest' | 'farthest', who: towards[2] as 'opponent' | 'ally' }
  const withinMovement = /within movement range/.test(s)
  const pick = (who: string | undefined) =>
    /farthest/.test(who ?? '') ? 'farthest' : /with |without /.test(who ?? '') ? 'criterion' : 'nearest'
  const ranged =
    /to be (at up to|up to|within) (?:range|reach) (\d+) from (the [a-z ]+opponent|an adjacent opponent)/.exec(s)
  if (ranged)
    return { kind: 'range', n: Number(ranged[2]), upTo: ranged[1] !== 'within', pick: pick(ranged[3]), withinMovement }
  const within = /to be within (?:range|reach) (\d+) of the (nearest|farthest) opponent/.exec(s)
  if (within) return { kind: 'range', n: Number(within[1]), upTo: false, pick: pick(within[2]), withinMovement }
  const adjacent = /adjacent to (the [a-z ]*opponent(?: with [a-z ]+)?|an opponent)/.exec(s)
  if (adjacent) return { kind: 'range', n: 1, upTo: false, pick: pick(adjacent[1]), withinMovement }
  return undefined
}

/** The Attacks, Spells and moves the helper can work out in one AI instruction. */
export function parseInstruction(text: string): Action[] {
  const actions: Action[] = []
  for (const sentence of sentences(text)) {
    const s = sentence.toLowerCase().replace(/\s+/g, ' ')
    const attack = /\[(melee|ranged)\]\s*(\d+)?\s*atks?\b(.*)/.exec(s)
    if (attack) {
      const reach = /reach (\d+)/.exec(s)
      const range = attack[1] === 'ranged' ? Number(attack[2] ?? 1) : reach ? Number(reach[1]) : 1
      const melee = attack[1] === 'melee'
      // "Make 2 [Melee] ATKS"
      const times = Number(/\b([2-9]) \[(?:melee|ranged)\][\s\d]*atks\b/.exec(s)?.[1] ?? 1)
      actions.push({
        kind: 'attack',
        spell: false,
        range,
        ...(melee && { melee }),
        ...(times > 1 && { times }),
        target: targetRule(attack[3] ?? ''),
      })
      continue
    }
    const spell = /\bspell \d+(.*)/.exec(s)
    if (spell) {
      const rest = spell[1] ?? ''
      const adjacent = /adjacent/.test(rest)
      const range = adjacent ? 1 : /line of sight/.test(rest) ? 999 : SOI
      actions.push({ kind: 'attack', spell: true, range, ...(adjacent && { melee: true }), target: targetRule(rest) })
      continue
    }
    const move = parseMove(s)
    if (move) actions.push({ kind: 'move', rule: move })
  }
  return actions
}

/** A figure that could be targeted, and how far it is. */
export interface Candidate {
  id: string
  name: string
  range: number
  /** Penalty to the Attack roll from Hindering Terrain or allies in the way (0 or less). */
  penalty?: number
  /** Higher than the target: +1 to the Attack roll (rulebook p. 22). */
  higher?: boolean
}

export type Advice =
  | {
      kind: 'attack'
      spell: boolean
      range: number
      /** Eligible targets, the suggested one first. */
      candidates: Candidate[]
      /** Every candidate is attacked (each, all, different opponents). */
      each: boolean
      /** The suggested target, when the board tells. */
      target?: Candidate
      criterion?: string
    }
  | { kind: 'move'; from: Cell; to: Cell; cost: number; toward?: string; criterion?: boolean }
  | { kind: 'stay'; at: Cell }

export interface TurnContext {
  board: Board
  acting: BoardFigure
  /** The acting figure's Movement, when the card prints a number. */
  movement?: number
  /** Whom each Attack was resolved against, by instruction then Attack: "the same target" is that one. */
  struck?: (string | undefined)[][]
}

const candidate = ({ figure, range }: { figure: BoardFigure; range: number }): Candidate => ({
  id: figure.id,
  name: figure.name,
  range,
})

function attackAdvice(
  action: Extract<Action, { kind: 'attack' }>,
  board: Board,
  acting: BoardFigure,
  previous: Candidate | undefined,
): Extract<Advice, { kind: 'attack' }> {
  // Within range and in Line of Sight (a Spell's SOI only stops at Obstructing Terrain)
  const soi = action.spell && action.range === SOI
  const seen = new Map(opponentsOf(board, acting).map((o) => [o.id, sight(board, acting, o, soi)]))
  const eligible = byDistance(acting, opponentsOf(board, acting)).filter(
    (o) =>
      (action.melee ? inReach(board, acting, o.figure, action.range) : o.range <= action.range) &&
      seen.get(o.figure.id)?.clear,
  )
  // Ties, and any target the card leaves open, go to the figure closer to the front of the Initiative Track.
  const byInitiative = [...eligible].sort((a, b) => a.figure.initiative - b.figure.initiative)
  const rule = action.target
  let ordered = byInitiative
  if (rule.kind === 'nearest') ordered = eligible
  if (rule.kind === 'farthest')
    ordered = [...eligible].sort((a, b) => b.range - a.range || a.figure.initiative - b.figure.initiative)
  if (rule.kind === 'effect') {
    const holds = (f: BoardFigure) => (f.effects ?? []).includes(rule.effect) === rule.has
    ordered = [...byInitiative.filter((o) => holds(o.figure)), ...byInitiative.filter((o) => !holds(o.figure))]
  }
  if (rule.kind === 'mostEffects')
    ordered = [...byInitiative].sort((a, b) => (b.figure.effects?.length ?? 0) - (a.figure.effects?.length ?? 0))
  if (rule.kind === 'same' && previous) {
    const same = eligible.find((o) => o.figure.id === previous.id)
    ordered = same ? [same, ...byInitiative.filter((o) => o !== same)] : byInitiative
  }
  const candidates = ordered.map((o) => {
    const view = seen.get(o.figure.id)
    return {
      ...candidate(o),
      penalty: view?.penalty || undefined,
      ...(!action.spell && view?.higher && { higher: true }),
    }
  })
  const criterion = rule.kind === 'criterion' ? rule.text : rule.kind === 'effect' ? rule.rest : undefined
  const decided = !criterion && rule.kind !== 'each'
  return {
    kind: 'attack',
    spell: action.spell,
    range: action.range,
    candidates,
    each: rule.kind === 'each',
    target: decided ? candidates[0] : undefined,
    criterion,
  }
}

/** The best square for `score` (highest), the cheapest when several are as good. */
function best(squares: Reach[], score: (at: Cell) => number): Reach | undefined {
  let chosen: Reach | undefined
  let top = -Infinity
  for (const s of squares) {
    const value = score(s.at)
    if (value > top || (value === top && chosen && s.cost < chosen.cost)) {
      chosen = s
      top = value
    }
  }
  return chosen
}

function moveAdvice(
  rule: MoveRule,
  context: TurnContext,
  acting: BoardFigure,
  previous: Candidate | undefined,
): Advice | undefined {
  if (context.movement === undefined) return undefined
  const { board } = context
  const squares = reachable(board, acting, context.movement)
  const at = (cell: Cell) => ({ ...acting, position: cell })
  const opponents = opponentsOf(board, acting)
  let chosen: Reach | undefined
  let toward: BoardFigure | undefined
  let criterion = false

  switch (rule.kind) {
    case 'towards': {
      const pool =
        rule.who === 'opponent' ? opponents : board.figures.filter((f) => f.side === acting.side && f.id !== acting.id)
      const sorted = byDistance(acting, pool)
      toward = (
        rule.pick === 'nearest'
          ? sorted[0]
          : [...sorted].sort((a, b) => b.range - a.range || a.figure.initiative - b.figure.initiative)[0]
      )?.figure
      if (toward) chosen = best(squares, (c) => -between(at(c), toward!))
      break
    }
    case 'range': {
      let sorted = byDistance(acting, opponents)
      if (rule.withinMovement) {
        const inReach = sorted.filter((o) => squares.some((s) => between(at(s.at), o.figure) <= rule.n))
        if (inReach.length) sorted = inReach
      }
      if (rule.pick === 'farthest')
        sorted = [...sorted].sort((a, b) => b.range - a.range || a.figure.initiative - b.figure.initiative)
      if (rule.pick === 'criterion') {
        criterion = true
        sorted = [...sorted].sort((a, b) => a.figure.initiative - b.figure.initiative)
      }
      toward = sorted[0]?.figure
      if (!toward) break
      const target = toward
      const now = between(acting, target)
      if (!rule.upTo && now <= rule.n) chosen = { at: acting.position, cost: 0 }
      else
        chosen = best(squares, (c) => {
          const d = between(at(c), target)
          // In range and in Line of Sight first: a square behind a wall does not help the attack after
          const seen = sight(board, at(c), target).clear ? 500 : 0
          return d <= rule.n ? 1000 + seen + (rule.upTo ? d : 0) : -d
        })
      break
    }
    case 'adjacentMany':
      chosen = best(squares, (c) => {
        const ranges = opponents.map((o) => between(at(c), o))
        return 100 * ranges.filter((d) => d <= 1).length - Math.min(...ranges, 99)
      })
      break
    case 'flee':
      chosen = best(squares, (c) => Math.min(...opponents.map((o) => between(at(c), o)), 99))
      break
    case 'away': {
      const target = board.figures.find((f) => f.id === previous?.id)
      if (!target) return undefined
      toward = target
      chosen = best(
        squares.filter((s) => s.cost <= rule.n),
        (c) => between(at(c), target),
      )
      break
    }
  }
  if (!chosen) return undefined
  if (chosen.at.x === acting.position.x && chosen.at.y === acting.position.y) return { kind: 'stay', at: chosen.at }
  return { kind: 'move', from: acting.position, to: chosen.at, cost: chosen.cost, toward: toward?.name, criterion }
}

/**
 * What the board suggests for each AI instruction of the turn, in order: the moves are played out
 * one after the other, so that an Attack after a MOVE is measured from where the figure ends.
 */
export function planTurn(texts: string[], context: TurnContext): Advice[][] {
  let acting = context.acting
  let previous: Candidate | undefined
  return texts.map((text, line) => {
    let part = 0
    return parseInstruction(text).flatMap((action): Advice[] => {
      if (action.kind === 'attack') {
        const advice = attackAdvice(action, context.board, acting, previous)
        const struck = context.struck?.[line]?.[part++]
        previous = struck ? { id: struck, name: struck, range: 0 } : (advice.target ?? previous)
        return [advice]
      }
      const advice = moveAdvice(action.rule, context, acting, previous)
      if (!advice) return []
      if (advice.kind === 'move') {
        acting = { ...acting, position: advice.to }
        context = {
          ...context,
          board: { ...context.board, figures: context.board.figures.map((f) => (f.id === acting.id ? acting : f)) },
        }
      }
      return [advice]
    })
  })
}
