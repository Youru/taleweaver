import type { LocalizedText } from '@/shared/lib/localized'
import type { AllyOption, AllyState } from './allies'
import type { PartyPreparation } from './preparation'

/** Mirrors Taleweaver.Api.Engine.GameState and its parts. */
export type GameStatus = 'InProgress' | 'Finished'

export interface GameView {
  id: string
  scenario: string
  party: string[]
  createdAt: string
  updatedAt: string
  canUndo: boolean
  state: GameState
}

export interface GameState {
  scenario: string
  status: GameStatus
  ending?: string
  campaign: CampaignState
  /** The Bounty played from a Story Round: its nodes are the ones read until it ends. */
  bounty?: { scenario: string; storyRound: string; lastEnd?: string | null } | null
  encounter?: EncounterState
  /** The party being made: the scenario is not read yet. */
  preparation?: PartyPreparation | null
  prompt?: Prompt
  /** The optional rules of this game. */
  options?: GameOptions | null
  abraxis?: AbraxisGame | null
  log: LogEntry[]
}

/** The optional rules chosen at the launch (rulebook p. 86-88): all off by default. */
export interface GameOptions {
  noPlayerElimination?: boolean
  /** With No Player Elimination: 3, 4 or 5. */
  urgencyLimit?: number | null
  tougherMobs?: boolean
  noScaling?: boolean
  staticMonsters?: boolean
  slowMonsters?: boolean
  lessVariety?: boolean
  removeUrgency?: boolean
  xpLeniency?: boolean
  forgivingTerrain?: boolean
  randomCounters?: boolean
  deadlierBosses?: boolean
  bossRage?: boolean
  nobodyLeftBehind?: boolean
  favorableConditions?: boolean
  reconnaissance?: boolean
}

export interface AbraxisGame {
  round: number
  name?: LocalizedText | null
  rule?: LocalizedText | null
  players: { name: string; points: number }[]
}

/** A Hidden Card revealed; no picture: it is taken from the box. */
export interface HiddenCard {
  size: string
  code: string
  pictures: { name: LocalizedText; image: string }[]
}

export interface CampaignState {
  currentNode?: string
  gold: number
  party: AdventurerState[]
  /** Every item gained, in order (a history): catalogue ids or the scenario's description. */
  items: string[]
  /** The Party Stash (rulebook p. 25). */
  stash?: OwnedItem[]
  /** Items gained (loot, rewards) not handed out yet. */
  unassigned?: OwnedItem[]
  revealedHiddenCards: string[]
  /** The Active Group (p. 11); missing: every member neither Injured nor Unselectable. */
  activeGroup?: string[] | null
  /** The Story Round the party is in (or went on a Side Quest from). */
  storyRound?: StoryRoundState | null
  /** The Unique Items drawn for each Story Round's Store, by node. */
  storeUniques?: Record<string, string[]>
  sideQuestsAttempted?: string[]
  storyRoundsLeft?: string[]
  bountiesCompleted?: string[]
  bountiesAttempted?: string[]
  /** Item Upgrades unlocked (Adventure Sheet). */
  unlockedUpgrades?: string[]
  achievements?: {
    id: string
    scenario: string
    node: string
    adventurer: string
    name?: LocalizedText | null
  }[]
  freeRespecs?: string[]
}

export interface StoryRoundState {
  node: string
  phase: 'shop' | 'explore' | 'sideQuest'
}

export interface AdventurerState {
  name: string
  xp: number
  injured?: boolean
  unselectable?: boolean
  /** Disciplines learnt: `xp` counts every XP gained, the XP left is xp minus their cost. */
  disciplines?: { id: string; xp: number }[]
  equipped?: OwnedItem[]
  pack?: OwnedItem[]
  /** Damage tokens, kept from one encounter to the next (rulebook p. 69). */
  damage?: number
  /** Defeated: back at the next encounter's Prepare Phase, with half their HP. */
  defeated?: boolean
  /** SP held. */
  stamina?: number
  /** Maximum HP: the card's plus the equipped cards' Health (computed by the engine). */
  health?: number
  /** SP gained each turn, and the most SP held (the Stamina Recovery Bar). */
  staminaRecovery?: number
  staminaMax?: number
  summonTokens?: number
  /** Conduit cards earned, and the one in the place of the Adventurer Card (rulebook p. 77). */
  conduits?: string[]
  conduitForm?: string | null
}

/** One item card the party holds; `item` is missing while the card is still to be drawn by the table. */
export interface OwnedItem {
  /** "item-3", unique in the game. */
  id: string
  /** Catalogue id, or missing for a card still to draw (Item Loot…). */
  item?: string | null
  /** A card still to draw: its tiers ("rare") and type ("consumable"). */
  tiers?: string[] | null
  type?: string | null
  /** itemLoot, monsterLoot, uniqueLoot, scenario, gm. */
  source?: string | null
  flipped: boolean
  /** Item Upgrades attached (catalogue ids). */
  upgrades?: string[]
}

/** Who holds an item: an adventurer's name, the Party Stash, or the items still to hand out. */
export type ItemOwner = string
export type ItemSlot = 'equipped' | 'pack'

/** Commands on the party's items, as sent to /api/games/{id}/items… */
export type ItemCommand =
  | { type: 'add'; item: string; owner: ItemOwner; slot?: ItemSlot; price?: number; replaces?: string }
  | { type: 'move'; id: string; owner: ItemOwner; slot?: ItemSlot }
  | { type: 'discard'; id: string; gold?: number }
  | { type: 'flip'; id: string; flipped: boolean }
  | { type: 'use'; id: string; discard: boolean; token?: string; at?: Cell }
  | { type: 'unlockUpgrade'; upgrade: string }
  | { type: 'attachUpgrade'; id: string; upgrade: string; price?: number }
  | { type: 'detachUpgrade'; id: string; upgrade: string }

export interface EncounterState {
  node: string
  round: number
  urgency: number
  ended: boolean
  initiativeTrack: string[]
  currentTurn: number
  /** Cards that had their turn this round, the one playing included. */
  playedCards?: string[]
  combatants: CombatantState[]
  tokens: BoardToken[]
  allies?: AllyState[]
  allyOptions?: AllyOption[]
  placedDiagrams: string[]
  /** A NAVIGATE cleared the encounter's own map: only the placed diagrams are on the table. */
  mapCleared?: boolean
  /** Fog areas revealed so far. */
  revealedAreas: string[]
  adventurers: AdventurerFigure[]
  activeEndConditions: string[]
  activation?: { initiativeCard: string; figures: string[]; step: number }
}

export interface CombatantState {
  instance: string
  card: string
  family: string
  number: number
  initiativeCards: string[]
  figure?: string
  /** Square where the scenario placed the figure (not followed afterwards). */
  position?: Cell
  damage: number
  effects: string[]
  /** Maximum HP when an option changes the card's. */
  health?: number | null
  aggro: boolean
  defeated: boolean
}

export type PromptKind =
  | 'continue'
  | 'choice'
  | 'skillCheck'
  | 'aiCondition'
  | 'spawnPossible'
  | 'number'
  | 'adventurer'
  | 'storyRound'
  | 'abraxis'

export interface Prompt {
  kind: PromptKind
  question?: LocalizedText
  options: { id: string; label?: LocalizedText }[]
  instance?: string
}

export interface LogEntry {
  code: string
  text?: LocalizedText
  args?: Record<string, string>
}

/** Mirrors Taleweaver.Api.Features.Games.ListGames.GameSummary. */
export interface GameSummary {
  id: string
  scenario: string
  party: string[]
  createdAt: string
  updatedAt: string
  status: GameStatus
  currentNode?: string
  preparing?: boolean
}

/** Commands on the party's members, as sent to /api/games/{id}/party… */
export type PartyCommand =
  | { type: 'activeGroup'; adventurers: string[] }
  | { type: 'status'; adventurer: string; status: 'injured' | 'unselectable'; value: boolean }
  | { type: 'learn'; adventurer: string; discipline: string; xp: number }
  | { type: 'forget'; adventurer: string; discipline: string }
  | { type: 'respec'; adventurer: string }

export type Answer =
  | { option: string; value?: undefined }
  | { value: number; option?: undefined }
  /** A round of Abraxis: each player's symbols extra or missing, `value` the points everyone loses. */
  | { misses: number[]; value: number; option?: undefined }

/** Commands of the encounter, as sent to /api/games/{id}/encounter/… */
export type EncounterCommand =
  | { type: 'nextTurn' }
  /** fromOpponent, tokens: a Loyal Esper's Damage (its summoner takes it too; Summon tokens discarded). */
  | { type: 'damage'; instance: string; amount: number; fromOpponent?: boolean; tokens?: number }
  | { type: 'summon'; adventurer: string; card: string }
  | { type: 'addAlly'; card: string; owner?: string }
  | { type: 'dismissAlly'; ally: string; ignoreDamage?: boolean }
  | { type: 'earnConduit'; adventurer: string; card: string }
  /** No card: the Adventurer Card again. */
  | { type: 'conduitForm'; adventurer: string; card?: string }
  | { type: 'effect'; instance: string; effect: string; active: boolean }
  | { type: 'defeat'; instance: string }
  | { type: 'activateToken'; token: string }
  | { type: 'endCondition'; endCondition: string }
  /** figure: a combatant's instance, or "adventurer:<name>". */
  | { type: 'move'; figure: string; to: Cell }
  | { type: 'revealArea'; area: string }
  /** An initiative card moved `by` places, negative toward the front. */
  | { type: 'moveInitiative'; card: string; by: number }
  /** The adventurer now holds `value` SP. */
  | { type: 'stamina'; adventurer: string; value: number }
  | { type: 'achievement'; achievement: string; adventurer: string }

export interface Cell {
  x: number
  y: number
}

export interface Rect extends Cell {
  w: number
  h: number
}

export interface BoardToken {
  id: string
  /** "type:colour", the key used by the scenario ("objective:red"). */
  key: string
  /** Catalogue id ("um1:token/objective"). */
  type?: string
  color?: string
  /** The picture chosen on the map, when not its type's (a variant). */
  image?: string
  /** Squares covered, when the scenario gives them. */
  area?: Rect
  removed: boolean
}

export interface AdventurerFigure {
  name: string
  position?: Cell
  /** Effects on the adventurer this encounter (catalogue ids, or home-made). */
  effects?: string[]
  /** Defeated: off the board until the next encounter. */
  defeated?: boolean
}
