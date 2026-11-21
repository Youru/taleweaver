import type { LogEntry } from '../api/types'
import { toneOf } from './formatLog'

/** Where a new page of the story begins: a scene, the end of an encounter, the end of the scenario. */
const PAGE_STARTS = new Set(['scene', 'encounterEnd', 'scenarioEnd', 'storyRound', 'storyRoundReturn'])

/** What the page shows besides its opening: texts read aloud, what was won, what the GM must do. */
const ON_THE_PAGE = new Set(['narrative', 'reward', 'alert'])

/**
 * The page of the story being read, between encounters: from the last scene (or end of encounter)
 * to now, without the bookkeeping (answers, variables, rounds), which stays in the journal.
 */
export function storyPage(log: LogEntry[]): LogEntry[] {
  let start = -1
  for (let i = log.length - 1; i >= 0; i--)
    if (PAGE_STARTS.has(log[i]!.code)) {
      start = i
      break
    }
  return log
    .slice(Math.max(0, start))
    .filter((entry, i) => (i === 0 && start >= 0) || ON_THE_PAGE.has(toneOf(entry.code)))
}

/** The encounter under way has reached an end condition (its last start is before its last end). */
export function encounterOver(log: LogEntry[]): boolean {
  const last = (code: string) => log.map((e) => e.code).lastIndexOf(code)
  return last('encounterEnd') > last('encounter')
}
