import type { AdventurerState, EncounterState } from '../api/types'

/** Every combatant card an encounter shows: the opponents', the allies', those offered, the Conduits in use. */
export function cardsInPlay(encounter: EncounterState, party: readonly AdventurerState[]): string[] {
  return [
    ...encounter.combatants.map((c) => c.card),
    ...(encounter.allies ?? []).map((a) => a.card),
    ...(encounter.allyOptions ?? []).flatMap((o) => o.cards),
    ...party.flatMap((a) => (a.conduitForm ? [a.conduitForm] : [])),
  ]
}
