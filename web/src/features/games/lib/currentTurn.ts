import type { EncounterState, LogEntry } from '../api/types'

/** What the "current turn" card shows. */
export interface CurrentTurn {
  /** before: round 1 not started; adventurer: an adventurer plays; combatants: a group goes through its AI; idle: nobody left on this card. */
  kind: 'before' | 'adventurer' | 'combatants' | 'idle'
  /** Adventurer's name, or the initiative card's id. */
  who?: string
  /** The figure going through its AI steps. */
  acting?: string
  /**
   * What happened since the turn began (AI instructions, events, spawns…), so that the GM can read it
   * while playing it out: it stays until the next turn.
   */
  entries: LogEntry[]
}

/** Lines the card leaves to the journal: answers and bookkeeping. */
const quiet = new Set(['answer', 'figureTurnEnd', 'turn', 'place'])

export function currentTurn(encounter: EncounterState, log: LogEntry[]): CurrentTurn {
  if (encounter.round === 0) return { kind: 'before', entries: [] }

  let start = log.length - 1
  while (start >= 0 && log[start]?.code !== 'turn') start--
  const entries = (start < 0 ? [] : log.slice(start + 1)).filter((entry) => !quiet.has(entry.code))
  const card = encounter.initiativeTrack[encounter.currentTurn]
  const turn = log[start]
  const adventurer = turn?.args?.adventurer

  if (adventurer) return { kind: 'adventurer', who: adventurer, entries }
  const acting = encounter.activation?.figures[0]
  if (acting) return { kind: 'combatants', who: card, acting, entries }
  return { kind: 'idle', who: card, entries }
}
