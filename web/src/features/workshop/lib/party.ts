import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftScenario } from '../api/types'

/** The "party" block as the workshop writes it (mirrors Taleweaver.Api.Content.Party); other fields are kept. */
export interface DraftParty {
  size?: number | null
  minSize?: number | null
  startingGold?: number | null
  startingXp?: number | null
  allowedAdventurers?: string[] | null
  excludedAdventurers?: string[] | null
  requiredAdventurers?: string[] | null
  custom?: boolean | null
  start?: DraftStart | null
  [field: string]: unknown
}

export interface DraftStart {
  items?: { tiers?: string[]; types?: string[] | null; max?: number | null } | null
  disciplines?: { trees?: string[] | null; maxLevel?: number | null; max?: number | null } | null
  kits?: DraftKit[]
  [field: string]: unknown
}

export interface DraftKit {
  id: string
  name: LocalizedText
  adventurer?: string
  items?: string[]
  disciplines?: string[]
}

export const partyOf = (scenario: DraftScenario) => (scenario.party ?? {}) as DraftParty

/** The scenario with this change to its party; what says nothing (null, an empty list) is not written. */
export function withParty(scenario: DraftScenario, change: Partial<DraftParty>): DraftScenario {
  return { ...scenario, party: compact({ ...partyOf(scenario), ...change }) }
}

/** The scenario with this change to what is had at the departure. */
export function withStart(scenario: DraftScenario, change: Partial<DraftStart>): DraftScenario {
  const start = compact({ ...(partyOf(scenario).start ?? {}), ...change })
  return withParty(scenario, { start: Object.keys(start).length ? start : undefined })
}

function compact<T extends object>(value: T): T {
  const said = Object.entries(value).filter(([, v]) => v != null && !(Array.isArray(v) && v.length === 0))
  return Object.fromEntries(said) as T
}

/** A kit id no other kit has. */
export function nextKitId(kits: readonly DraftKit[]): string {
  let n = kits.length + 1
  while (kits.some((kit) => kit.id === `kit-${n}`)) n++
  return `kit-${n}`
}
