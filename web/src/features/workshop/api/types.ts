import type { LocalizedText } from '@/shared/lib/localized'

/**
 * A scenario as the workshop edits it: the fields it knows are typed, and every other field of the
 * file is kept as it is (index signature), so that saving never loses anything.
 * Mirrors Taleweaver.Api.Content.Scenario.
 */
export interface DraftScenario {
  id: string
  title: LocalizedText
  kind?: string
  packs: string[]
  start: string
  nodes: DraftNode[]
  /** A campaign folder's chapters, in order (none: a single-file scenario). */
  chapters?: DraftChapter[]
  /** Read by the workshop only: where each node sits on the graph. */
  editor?: { positions?: Record<string, Position> }
  [field: string]: unknown
}

/** Mirrors Taleweaver.Api.Content.ScenarioChapter: the folder name and the title. */
export interface DraftChapter {
  id: string
  title?: LocalizedText
}

export interface Position {
  x: number
  y: number
}

export interface DraftNode {
  id: string
  /** scene or encounter. */
  type: string
  /** The chapter it belongs to, when the scenario has chapters. */
  chapter?: string
  /** Scene title. */
  title?: LocalizedText
  /** Encounter name. */
  name?: LocalizedText
  narrative?: LocalizedText
  intro?: LocalizedText
  locationType?: string
  lootLevel?: string
  mechanics?: DraftAction[]
  next?: string
  specialRules?: DraftRule[]
  endConditions?: DraftEnd[]
  /** Story Round: the Side Quests and the ways to Venture Forth. */
  sideQuests?: DraftStoryExit[]
  ventureForth?: DraftStoryExit[]
  [field: string]: unknown
}

/** Mirrors Taleweaver.Api.Content.StoryExit. */
export interface DraftStoryExit {
  id?: string
  label?: LocalizedText
  node: string
  page?: number
}

export interface DraftAction {
  do: string
  node?: string
  question?: LocalizedText
  options?: DraftOption[]
  [field: string]: unknown
}

export interface DraftOption {
  label?: LocalizedText
  actions?: DraftAction[]
  [field: string]: unknown
}

export interface DraftEnd {
  id: string
  /** win or lose. */
  kind: string
  text?: LocalizedText
  reward?: LocalizedText
  next?: string
  [field: string]: unknown
}

export interface DraftRule {
  id: string
  name: LocalizedText
  text: LocalizedText
}

/** Mirrors Taleweaver.Api.Content.ScenarioReport. */
export interface ScenarioReport {
  errors: ScenarioIssue[]
  warnings: ScenarioIssue[]
  isValid?: boolean
}

export interface ScenarioIssue {
  code: string
  /** The node id, or "node/detail", or a scenario field (start, nodes). */
  where: string
  message: string
}

/** Mirrors Taleweaver.Api.Storage.ScenarioSummary. */
export interface WorkshopSummary {
  id: string
  title: LocalizedText
  kind?: string
  packs: string[]
  editable?: boolean
}
