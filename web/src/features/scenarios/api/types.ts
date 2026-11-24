import type { LocalizedText } from '@/shared/lib/localized'

/** Mirrors Taleweaver.Api.Storage.ScenarioSummary. */
export interface ScenarioSummary {
  id: string
  title: LocalizedText
  kind?: string
  packs: string[]
}

/** The parts of Taleweaver.Api.Content.Scenario the front reads. */
export interface Scenario {
  id: string
  title: LocalizedText
  start: string
  nodes: ScenarioNode[]
  variables?: ScenarioVariable[]
}

/** A variable declared by the scenario (data-model §3.5). */
export interface ScenarioVariable {
  id: string
  type: string
  scope?: string
  /** Its readable name on the Adventure Sheet. */
  name?: LocalizedText
  /** A Story Flag of the book, shown at the top of the Adventure Sheet. */
  storyFlag?: boolean
}

export interface ScenarioNode {
  id: string
  type: string
  name?: LocalizedText
  title?: LocalizedText
  lootLevel?: string
  map?: BoardMap
  setup?: { adventurersStart?: string }
  diagrams?: Diagram[]
  specialRules?: SpecialRule[]
  /** What the GM does by hand in this step. */
  gmNote?: LocalizedText
  tokens: Record<string, TokenRule>
  endConditions: EndCondition[]
  page?: number
  /** Story Round (rulebook p. 13). */
  store?: Store
  sideQuests?: StoryExit[]
  ventureForth?: StoryExit[]
  bountyDifficulty?: number
  /** The Espers an opponent may summon (rulebook p. 76). */
  encounterEspers?: { ref: string; summoner?: string }[]
  /** Achievements of an encounter (rulebook p. 17). */
  achievements?: { id: string; name: LocalizedText; condition?: LocalizedText; hiddenText?: LocalizedText }[]
}

/** The Store of a Story Round: non-unique cards of these tiers/types, unlocked upgrades, random Unique Items. */
export interface Store {
  items: { tiers: string[]; types: string[] }[]
  upgrades?: boolean
  unique?: { tier: string; count: number }
}

export interface StoryExit {
  id?: string
  label?: LocalizedText
  node: string
  page?: number
}

export interface TokenRule {
  mode?: string
  activation?: LocalizedText
  events: string[]
}

export interface EndCondition {
  id: string
  kind: string
  active?: boolean
  text?: LocalizedText
  reward?: LocalizedText
  next?: string
}

/** Coordinates in squares, x to the east and y to the south, origin at the top-left corner. */
export interface BoardMap {
  northArrow?: number
  tiles: MapTile[]
  tokens: MapToken[]
  zones: Zone[]
  currents: Current[]
  elevation: ElevationArea[]
  /** Fog of war: areas hidden until revealed. */
  areas: FogArea[]
}

export interface FogArea {
  id: string
  revealedAtStart?: boolean
  name?: LocalizedText
}

/** Rotation: degrees clockwise from the tile's own orientation (number at the bottom right). */
export interface MapTile {
  ref: string
  x: number
  y: number
  rotation?: number | null
  /** Fog area of the tile. */
  area?: string
}

export interface MapToken {
  id: string
  type: string
  color?: string
  /** The picture shown, when not its type's (a variant). */
  image?: string
  x: number
  y: number
  w?: number
  h?: number
  area?: string
}

export interface Zone {
  id: string
  kind?: string
  rect: { x: number; y: number; w: number; h: number }
}

export interface Current {
  rect?: { x: number; y: number; w: number; h: number }
  tile?: string
  direction?: string
  strength: number
  note?: LocalizedText
}

export interface ElevationArea {
  level: number
  rect: { x: number; y: number; w: number; h: number }
  note?: LocalizedText
}

export interface Diagram {
  id: string
  label?: LocalizedText
  page?: number
  map?: BoardMap
}

export interface SpecialRule {
  id: string
  name: LocalizedText
  text: LocalizedText
}
