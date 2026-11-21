import type { InitiativeCard } from '@/features/catalog'
import type { EncounterState } from '../api/types'
import { shortId } from './ids'

const ADVENTURER = 'adventurer:'

/** A card of the round's track, as the players see it (rulebook p. 36). */
/** The phone of the room holding an adventurer (room-spec §7.3): its colour, its player, whether it is connected. */
export interface HeldBy {
  color: string
  name: string
  connected: boolean
}

export interface TrackCard {
  /** The card's id on the track ("um1:initiative/cave-sickle-1-3", "adventurer:Rook"). */
  id: string
  /** 1 for the front of the track. */
  order: number
  label: string
  image?: string
  /** Black border: an adventurer or an ally; red: an opponent. */
  side: 'adventurer' | 'opponent'
  /** played: its turn is over this round; current: it plays; next: the following card; later: after that. */
  state: 'played' | 'current' | 'next' | 'later'
  /**
   * A group's figures on the board, in activation order (lowest number first, p. 56); empty for a
   * card that stands for one figure.
   */
  figures: TrackFigure[]
}

export interface TrackFigure {
  instance: string
  number: number
  state: 'played' | 'acting' | 'waiting'
}

/**
 * The track of the round, front first: whose turn it is, who has played and who comes next. A group's
 * card shows its figures still on the board; the one acting is the first of the activation.
 */
export function initiativeTrack(encounter: EncounterState, cards: readonly InitiativeCard[]): TrackCard[] {
  const byId = new Map(cards.map((card) => [card.id, card]))
  const started = encounter.round > 0
  const track = encounter.initiativeTrack
  // Cards moved on the track: what played is not "before the current index" any more (p. 36)
  const played = new Set(encounter.playedCards ?? track.slice(0, encounter.currentTurn + 1))
  const next = track.findIndex((id) => !played.has(id))
  return track.map((id, index) => {
    const state: TrackCard['state'] = !started
      ? 'later'
      : index === encounter.currentTurn
        ? 'current'
        : played.has(id)
          ? 'played'
          : index === next
            ? 'next'
            : 'later'
    const adventurer = id.startsWith(ADVENTURER)
    const card = adventurer ? adventurerCard(shortId(id), cards) : byId.get(id)
    const figures = adventurer ? [] : figuresOf(encounter, id, state)
    return {
      id,
      order: index + 1,
      label: adventurer ? shortId(id) : (card?.label ?? shortId(id)),
      image: card?.image ?? undefined,
      side: adventurer || card?.adventurer ? 'adventurer' : 'opponent',
      state,
      figures: card?.group || figures.length > 1 ? figures : [],
    }
  })
}

function figuresOf(encounter: EncounterState, card: string, state: TrackCard['state']): TrackFigure[] {
  const activation = encounter.activation?.initiativeCard === card ? encounter.activation : undefined
  return encounter.combatants
    .filter((c) => !c.defeated && c.initiativeCards.includes(card))
    .sort((a, b) => a.number - b.number)
    .map((c) => ({
      instance: c.instance,
      number: c.number,
      state:
        state === 'played'
          ? 'played'
          : state !== 'current'
            ? 'waiting'
            : !activation
              ? 'played'
              : activation.figures[0] === c.instance
                ? 'acting'
                : activation.figures.includes(c.instance)
                  ? 'waiting'
                  : 'played',
    }))
}

/** An adventurer's card by their name: the plain print first (a story adventurer may have a red one). */
function adventurerCard(name: string, cards: readonly InitiativeCard[]): InitiativeCard | undefined {
  const lower = name.toLowerCase()
  const rank = (card: InitiativeCard) => (card.border ? 2 : 0) + (card.lowres ? 1 : 0)
  return cards
    .filter((card) => card.adventurer && card.label.toLowerCase() === lower)
    .sort((a, b) => rank(a) - rank(b))[0]
}
