import type { EncounterState, LogEntry } from '../api/types'

/** What the "current turn" card shows. */
export interface CurrentTurn {
  /** before: round 1 not started; adventurer: an adventurer plays; ally: allies play; combatants: a group goes through its AI; idle: nobody left on this card; between: a round ends or starts, its question waits. */
  kind: 'before' | 'between' | 'adventurer' | 'ally' | 'combatants' | 'idle'
  /** Adventurer's name, or the initiative card's id. */
  who?: string
  /** An ally's turn: whose player plays it. */
  playedBy?: string
  /** The figure going through its AI steps. */
  acting?: string
  /**
   * What happened since the turn began (AI instructions, events, spawns…), so that the GM can read it
   * while playing it out: it stays until the next turn.
   */
  entries: LogEntry[]
}

/** Lines the card leaves to the journal: answers and bookkeeping. */
const quiet = new Set(['answer', 'figureTurnEnd', 'turn', 'turnEnd', 'place', 'roundStart', 'roundEnd'])
const roundChange = new Set(['roundStart', 'roundEnd'])

export function currentTurn(encounter: EncounterState, log: LogEntry[]): CurrentTurn {
  if (encounter.round === 0) return { kind: 'before', entries: [] }

  let start = log.length - 1
  while (start >= 0 && log[start]?.code !== 'turn') start--
  const shown = (from: number) => log.slice(from).filter((entry) => !quiet.has(entry.code))
  const change = log.findIndex((entry, i) => i > start && roundChange.has(entry.code))
  // What the end and the start of a round trigger is played before the first turn
  if (change >= 0) return { kind: 'between', entries: shown(change) }

  let previous = start - 1
  while (previous >= 0 && log[previous]?.code !== 'turn') previous--
  const opened = log.findIndex((entry, i) => i > previous && i < start && roundChange.has(entry.code))
  // What the end of the turn before triggered is still to play out
  const ended = log.findIndex((entry, i) => i > previous && i < start && entry.code === 'turnEnd')
  const from = opened >= 0 ? opened : ended >= 0 ? ended : start + 1
  const entries = start < 0 ? [] : shown(from)
  const card = encounter.initiativeTrack[encounter.currentTurn]
  const turn = log[start]
  const adventurer = turn?.args?.adventurer

  if (adventurer) return { kind: 'adventurer', who: adventurer, entries }
  if (turn?.args?.ally) return { kind: 'ally', who: turn.args.ally, playedBy: turn.args.playedBy, entries }
  const acting = encounter.activation?.figures[0]
  if (acting) return { kind: 'combatants', who: card, acting, entries }
  return { kind: 'idle', who: card, entries }
}
