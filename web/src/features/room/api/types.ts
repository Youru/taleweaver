import type {
  AdventurerState,
  AllyOptionKind,
  BoardToken,
  Cell,
  ChoiceChange,
  LogEntry,
  OwnedItem,
  PartyPreparation,
} from '@/features/games'
import type { BoardMap, MapTile } from '@/features/scenarios'
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
  autonomy: Autonomy
  /** Every pending request, then the last answered ones, newest first. */
  requests: PlayerRequest[]
}

/** What players may do without the GM (room-spec §6.3). */
export type Autonomy = 'validateAll' | 'freeInventoryOutsideEncounter'

/** What a phone may ask (room-spec §6.2): items of its adventurers or of the stash, disciplines. */
export type PlayerCommand =
  | { type: 'moveItem'; item: string; owner: string; slot?: 'equipped' | 'pack' }
  | { type: 'flipItem'; item: string; flipped: boolean }
  | { type: 'useItem'; item: string; discard: boolean }
  | { type: 'learnDiscipline'; adventurer: string; discipline: string }
  | { type: 'takeAdventurer'; adventurer: string }
  | { type: 'buyItem'; item: string; owner: string; slot?: 'equipped' | 'pack'; price?: number }
  | { type: 'attachUpgrade'; item: string; upgrade: string; price?: number }
  | { type: 'damage'; adventurer: string; amount: number }
  | ({ type: 'chooseAdventurer'; adventurer: string } & ChoiceChange)
  | { type: 'releaseAdventurer'; adventurer: string }
  | { type: 'summon'; adventurer: string; card: string }
  | { type: 'earnConduit'; adventurer: string; card: string }
  | { type: 'conduitForm'; adventurer: string; card?: string }
  | { type: 'dismissAlly'; ally: string }
  | { type: 'allyDamage'; ally: string; amount: number }
  | { type: 'effect'; adventurer: string; effect: string; active: boolean }

export interface PlayerRequest {
  id: string
  seatId: string
  /** The player's name. */
  by: string
  command: PlayerCommand
  status: 'pending' | 'accepted' | 'applied' | 'refused' | 'cancelled' | 'failed'
  /** Why it was refused or failed: an error code or the GM's words. */
  reason?: string | null
  at: string
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
  gameId: string
  scenario: string
  scenarioTitle?: LocalizedText | null
  seats: string[]
}

/** What a phone receives: a white list of the game state (room-spec §8). */
export interface PlayerView {
  /** Changes when the catalogue has new cards for this phone. */
  cards?: string | null
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
  /** The seat's own requests, the pending ones and the last answers. */
  requests: PlayerRequest[]
  autonomy: Autonomy
  /** The Store open now (Shop phase of a Story Round): catalogue ids at their price. */
  shop?: { item: string; price: number }[] | null
  /** Item Upgrades for sale now, with their price by the tier of the card they go on. */
  upgrades?: { upgrade: string; prices: Record<string, number> }[]
  /** Before the departure: what the rules offer. */
  preparation?: PartyPreparation | null
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
  board?: PlayerBoard | null
  allies?: PlayerAlly[]
  /** What the seat's adventurers may bring or become. */
  allyOptions?: PlayerAllyOption[]
}

export interface PlayerAlly {
  id: string
  family: string
  name: LocalizedText
  image?: string | null
  trackCard: string
  owner?: string | null
  esper: boolean
  damage: number
  health?: number | null
  force?: number | null
  stamina: number
  staminaMax: number
  effects: string[]
  defeated: boolean
}

export interface PlayerAllyOption {
  adventurer: string
  kind: AllyOptionKind
  cards: { id: string; name?: LocalizedText | null; image?: string | null }[]
}

/** The table cut on the server: a tile under the fog is only a shape (ref and area "fog"). */
export interface PlayerBoard {
  tiles: MapTile[]
  zones: BoardMap['zones']
  elevation: BoardMap['elevation']
  tokens: BoardToken[]
  figures: { instance: string; figure?: string | null; position: Cell }[]
  adventurers: { name: string; position: Cell }[]
  allies?: { instance: string; figure?: string | null; position: Cell }[]
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
  /** A seat holds them. */
  held: boolean
}

export interface JoinResponse {
  seatId: string
  seatToken: string
  view: PlayerView
}
