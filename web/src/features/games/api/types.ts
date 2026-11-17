import type { LocalizedText } from '@/shared/lib/localized'

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
  encounter?: EncounterState
  prompt?: Prompt
  log: LogEntry[]
}

export interface CampaignState {
  currentNode?: string
  gold: number
  party: { name: string; xp: number }[]
  items: string[]
  revealedHiddenCards: string[]
}

export interface EncounterState {
  node: string
  round: number
  urgency: number
  ended: boolean
  initiativeTrack: string[]
  currentTurn: number
  combatants: CombatantState[]
  tokens: BoardToken[]
  placedDiagrams: string[]
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
  aggro: boolean
  defeated: boolean
}

export type PromptKind =
  'continue' | 'choice' | 'skillCheck' | 'aiCondition' | 'spawnPossible' | 'number' | 'adventurer'

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
}

export type Answer = { option: string; value?: undefined } | { value: number; option?: undefined }

/** Commands of the encounter, as sent to /api/games/{id}/encounter/… */
export type EncounterCommand =
  | { type: 'nextTurn' }
  | { type: 'damage'; instance: string; amount: number }
  | { type: 'effect'; instance: string; effect: string; active: boolean }
  | { type: 'defeat'; instance: string }
  | { type: 'activateToken'; token: string }
  | { type: 'endCondition'; endCondition: string }
  /** figure: a combatant's instance, or "adventurer:<name>". */
  | { type: 'move'; figure: string; to: Cell }
  | { type: 'revealArea'; area: string }

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
}
