import type { LogEntry } from '../api/types'

/**
 * The combatants that appeared during the encounter in progress (a totem, an event, a diagram: the
 * engine logs "spawn"; the encounter's own setup logs "place"). The players have just been told they
 * arrive, so they are not kept secret: the GM is asked to put them on the board.
 */
export function arrivedCombatants(log: readonly LogEntry[] | undefined): Set<string> {
  const arrived = new Set<string>()
  for (const entry of log ?? []) {
    if (entry.code === 'encounter') arrived.clear()
    else if (entry.code === 'spawn' && entry.args?.instance) arrived.add(entry.args.instance)
  }
  return arrived
}
