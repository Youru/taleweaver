import type { EncounterState } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import type { PlayerEncounter } from '../api/types'

const FOG = 'fog'

/**
 * The table component reads a step and its encounter: built here from what the server let through, with one fog
 * area for every hidden tile.
 */
export function asBoard(view: PlayerEncounter): { node: ScenarioNode; encounter: EncounterState } | undefined {
  const board = view.board
  if (!board) return undefined
  const node: ScenarioNode = {
    id: 'table',
    type: 'encounter',
    tokens: {},
    endConditions: [],
    map: {
      tiles: board.tiles,
      tokens: [],
      zones: board.zones,
      currents: [],
      elevation: board.elevation,
      areas: [{ id: FOG, revealedAtStart: false, name: { en: '?' } }],
    },
  }
  const cards = new Map(view.combatants.map((c) => [c.instance, c]))
  const encounter: EncounterState = {
    node: 'table',
    round: view.round,
    urgency: view.urgency,
    ended: view.ended,
    initiativeTrack: view.initiativeTrack,
    currentTurn: view.currentTurn,
    combatants: board.figures.flatMap(({ instance, figure, position }) => {
      const card = cards.get(instance)
      return card
        ? [{ ...card, health: card.health ?? undefined, figure: figure ?? undefined, position, aggro: false }]
        : []
    }),
    tokens: board.tokens,
    placedDiagrams: [],
    revealedAreas: [],
    activeEndConditions: [],
    adventurers: board.adventurers,
    allies: (board.allies ?? []).flatMap(({ instance, figure, position }) => {
      const ally = view.allies?.find((a) => a.id === instance)
      return ally ? [{ ...ally, card: '', source: 'story' as const, staminaRecovery: 0, figure, position }] : []
    }),
    activation: view.activation ? { ...view.activation, step: 0 } : undefined,
  }
  return { node, encounter }
}
