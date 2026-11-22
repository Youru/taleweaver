import type { AdventurerState, EncounterState } from '../api/types'

/** Every combatant card an encounter shows: the opponents', the allies', those offered, the Conduits in use. */
export function cardsInPlay(encounter: EncounterState, party: readonly AdventurerState[]): string[] {
  return [
    // A figure the scenario describes has no card in the catalogue
    ...encounter.combatants.map((c) => c.card).filter((id) => !id.startsWith('figure:')),
    ...(encounter.allies ?? []).map((a) => a.card),
    ...(encounter.allyOptions ?? []).flatMap((o) => o.cards),
    ...party.flatMap((a) => (a.conduitForm ? [a.conduitForm] : [])),
  ]
}
