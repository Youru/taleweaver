import type { AdventurerState, LogEntry, OwnedItem } from '@/features/games'
import type { LocalizedText } from '@/shared/lib/localized'

/** The room as the GM sees it (GET /api/games/{id}/room). */
export interface RoomView {
  gameId: string
  code: string
  status: 'open' | 'closed'
  seats: SeatView[]
  /** Private IPv4 addresses of the GM's machine: where the phones of the same Wi-Fi reach it. */
  lanAddresses: string[]
  /** A second screen of the GM's has a token (room-spec §7). */
  hasScreen: boolean
}

export interface SeatView {
  id: string
  displayName: string
  color: string
  adventurers: string[]
  followed?: string | null
  lastSeen: string
  connected: boolean
}

export interface RoomSummary {
  code: string
  scenario: string
  scenarioTitle?: LocalizedText | null
  seats: string[]
}

/** What a phone receives: a white list of the game state (room-spec §8). */
export interface PlayerView {
  version: number
  gameId: string
  scenarioTitle?: LocalizedText | null
  roomCode?: string | null
  seat?: { id: string; displayName: string; color: string; followed?: string | null } | null
  /** The GM playing an adventurer in a tab of their own. */
  impersonated: boolean
  /** The seat's adventurers, whole. */
  adventurers: AdventurerState[]
  /** Every party member as the table sees them. */
  party: PartyMemberView[]
  gold: number
  stash: OwnedItem[]
  unassigned: OwnedItem[]
  encounter?: PlayerEncounter | null
  /** The public lines of the log (room-spec §8), with their place in the GM's log. */
  log: { index: number; entry: LogEntry }[]
}

/** The encounter as the table sees it: never the AI, the hidden rules nor the end conditions. */
export interface PlayerEncounter {
  round: number
  urgency: number
  ended: boolean
  initiativeTrack: string[]
  currentTurn: number
  playedCards: string[]
  combatants: PlayerCombatant[]
  activation?: { initiativeCard: string; figures: string[] } | null
  /** Effects on each adventurer's figure, by name. */
  adventurerEffects: Record<string, string[]>
}

export interface PlayerCombatant {
  instance: string
  card: string
  family: string
  number: number
  initiativeCards: string[]
  damage: number
  effects: string[]
  defeated: boolean
  /** From the combatant's card: a phone may not read the cards themselves (their AI is the GM's). */
  name?: LocalizedText | null
  image?: string | null
  health?: number | null
}

export interface PartyMemberView {
  name: string
  damage: number
  health: number
  defeated: boolean
  injured: boolean
  unselectable: boolean
  active: boolean
  mine: boolean
}

export interface JoinResponse {
  seatId: string
  seatToken: string
  view: PlayerView
}
