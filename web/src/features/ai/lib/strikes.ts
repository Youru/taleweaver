import type { LogEntry } from '@/features/games'
import { parseInstruction } from './instructions'

/** An Attack or a Spell an AI instruction asked for, and whom it was resolved against. */
export interface Strike {
  /** "line:part", the line being the instruction's in the journal. */
  id: string
  entry: LogEntry
  figure: string
  spell: boolean
  /** Absent: the wording was not understood. */
  melee?: boolean
  range?: number
  /** One against each opponent: as many as there are targets. */
  each: boolean
  times: number
  /** The targets it was resolved against, in order. */
  done: string[]
}

type Asked = Pick<Strike, 'spell' | 'melee' | 'range' | 'each' | 'times'>

function askedBy(text: string): Asked[] {
  const asked = parseInstruction(text).flatMap((action): Asked[] => {
    if (action.kind !== 'attack') return []
    const times = action.times ?? 1
    const each = times === 1 && action.target.kind === 'each'
    return [{ spell: action.spell, melee: !!action.melee, range: action.range, each, times }]
  })
  if (asked.length || !/\bATK\b|\bSPELL \d/.test(text)) return asked
  // Worded in a way the helper does not read: still one to resolve
  return [{ spell: !/\bATK\b/.test(text), each: false, times: 1 }]
}

/** The Attacks and Spells of the AI instructions given this turn, resolved or not: the journal says it all. */
export function strikesOfTurn(log: LogEntry[]): Strike[] {
  let start = log.length - 1
  while (start >= 0 && log[start]?.code !== 'turn') start--
  const resolved: { step: string; target: string }[] = []
  for (const { args } of log.slice(start + 1))
    if (args?.step && args.target) resolved.push({ step: args.step, target: args.target })

  const strikes: Strike[] = []
  log.forEach((entry, line) => {
    const figure = entry.args?.instance
    if (line <= start || entry.code !== 'aiInstruction' || !figure) return
    askedBy(entry.text?.en ?? '').forEach((asked, part) => {
      const id = `${line}:${part}`
      const done = resolved.filter((r) => r.step.startsWith(`${id}:`)).map((r) => r.target)
      strikes.push({ ...asked, id, entry, figure, done })
    })
  })
  return strikes
}

/** Still to resolve (against `target`, when given). */
export function isOpen(strike: Strike, target?: string): boolean {
  if (strike.each) return !target || !strike.done.includes(target)
  return strike.done.length < strike.times
}

/** What the engine refuses to see twice: the occurrence, or the target of a "each". */
export const stepKey = (strike: Strike, target: string) => `${strike.id}:${strike.each ? target : strike.done.length}`
