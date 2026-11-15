import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import type { Combatant } from '@/features/catalog'
import type { Cell, EncounterState, LogEntry, Prompt } from '@/features/games'
import type { BoardLayout } from '@/features/table'
import { parseCondition, suggest, type Suggestion } from './conditions'
import { key, type Board, type BoardFigure } from './grid'
import { planTurn, type Advice } from './instructions'

/** What the table shows of the AI's plan: where the acting figure should go, and whom it targets. */
export interface TableHint {
  moves: { figure: string; from: Cell; to: Cell; size: number }[]
  targets: string[]
}

/** The board as the AI helper reads it, from what the table draws. */
export function readBoard(layout: BoardLayout, encounter: EncounterState): Board {
  const squares = new Set<string>()
  for (const tile of layout.tiles)
    if (!tile.hidden)
      for (let x = tile.x; x < tile.x + tile.w; x++)
        for (let y = tile.y; y < tile.y + tile.h; y++) squares.add(key({ x, y }))
  const track = encounter.initiativeTrack
  // Only figures standing on a revealed tile: not those dropped off the tiles.
  const figures = layout.figures
    .filter((f) => squares.has(key({ x: f.x, y: f.y })))
    .map((f): BoardFigure => {
      const combatant = encounter.combatants.find((c) => c.instance === f.id)
      const places = combatant
        ? combatant.initiativeCards.map((card) => track.indexOf(card)).filter((i) => i >= 0)
        : [track.indexOf(f.id)].filter((i) => i >= 0)
      return {
        id: f.id,
        name: f.name,
        side: f.kind,
        position: { x: f.x, y: f.y },
        size: f.size,
        damage: combatant?.damage ?? 0,
        initiative: places.length ? Math.min(...places) : track.length,
      }
    })
  return { squares, figures }
}

/**
 * Where each figure stood when its AI instructions came: the plan is measured from there, so that
 * it does not change once the GM has moved the figure (kept for the session only).
 */
const starts = new Map<string, Cell>()

/**
 * The AI helper: answers the pending AI condition from the positions on the table, and works out the
 * targets and moves of the instructions given this turn. Without walls nor Line of Sight, it only
 * suggests: the GM still answers and moves the figures.
 */
export function useAiAdvice(
  game: MaybeRefOrGetter<string>,
  layout: MaybeRefOrGetter<BoardLayout | undefined>,
  encounter: MaybeRefOrGetter<EncounterState | undefined>,
  cards: MaybeRefOrGetter<Map<string, Combatant>>,
  log: MaybeRefOrGetter<LogEntry[]>,
  prompt: MaybeRefOrGetter<Prompt | undefined>,
) {
  const board = computed(() => {
    const l = toValue(layout)
    const e = toValue(encounter)
    return l && e ? readBoard(l, e) : undefined
  })

  function movementOf(instance: string): number | undefined {
    const card = toValue(encounter)?.combatants.find((c) => c.instance === instance)?.card
    const movement = card ? toValue(cards).get(card)?.stats?.movement : undefined
    return typeof movement === 'number' ? movement : undefined
  }

  const condition = computed<Suggestion | undefined>(() => {
    const p = toValue(prompt)
    const b = board.value
    if (p?.kind !== 'aiCondition' || !p.instance || !b) return undefined
    const text = p.question?.en ?? Object.values(p.question ?? {})[0]
    const parsed = text ? parseCondition(text) : undefined
    const acting = b.figures.find((f) => f.id === p.instance)
    return parsed && acting ? suggest(parsed, b, acting, movementOf(acting.id)) : undefined
  })

  /** The advice for each AI instruction of the current turn, by log entry. */
  const advice = computed(() => {
    const result = new Map<LogEntry, Advice[]>()
    const b = board.value
    const entries = toValue(log)
    if (!b) return result
    let start = entries.length - 1
    while (start >= 0 && entries[start]?.code !== 'turn') start--
    // One run of instructions per figure (a group's figures play one after the other).
    const runs = new Map<string, { first: number; entries: LogEntry[] }>()
    entries.forEach((entry, index) => {
      if (index <= start || entry.code !== 'aiInstruction' || !entry.args?.instance) return
      const run = runs.get(entry.args.instance) ?? { first: index, entries: [] }
      run.entries.push(entry)
      runs.set(entry.args.instance, run)
    })
    for (const [instance, run] of runs) {
      const acting = b.figures.find((f) => f.id === instance)
      if (!acting) continue
      const startKey = `${toValue(game)}|${instance}|${run.first}`
      const from = starts.get(startKey) ?? acting.position
      starts.set(startKey, from)
      const plan = planTurn(
        run.entries.map((e) => e.text?.en ?? ''),
        { board: b, acting: { ...acting, position: from }, movement: movementOf(instance) },
      )
      run.entries.forEach((entry, i) => result.set(entry, plan[i] ?? []))
    }
    return result
  })

  /**
   * The plan drawn on the table: the moves still to play this turn (the figure is not there yet),
   * and the targets of the last figure given instructions.
   */
  const hint = computed<TableHint | undefined>(() => {
    const figures = board.value?.figures ?? []
    const moves: TableHint['moves'] = []
    let targets: string[] = []
    let last: string | undefined
    for (const [entry, list] of advice.value) {
      const figure = figures.find((f) => f.id === entry.args?.instance)
      if (!figure) continue
      if (figure.id !== last) targets = []
      last = figure.id
      for (const a of list) {
        const there = a.kind === 'move' && a.to.x === figure.position.x && a.to.y === figure.position.y
        if (a.kind === 'move' && !there) moves.push({ figure: figure.id, from: a.from, to: a.to, size: figure.size })
        if (a.kind === 'attack') targets.push(...(a.each ? a.candidates : a.target ? [a.target] : []).map((c) => c.id))
      }
    }
    return moves.length || targets.length ? { moves, targets } : undefined
  })

  return { condition, advice, hint }
}
