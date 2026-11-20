import { http, httpFile } from '@/shared/api/http'
import { scenarioPath } from '@/features/scenarios'
import type { DraftScenario, ScenarioReport, WorkshopSummary } from './types'

/** What the print page shows: a scenario, one encounter (node), the cards (what=cards), texts in the clear (veiled=0). */
export interface PrintQuery {
  scenario: string
  node?: string
  what?: string
  veiled?: string
  /** Pages of the booklet to print ("5-12, 20"). */
  pages?: string
}

/** "homebrew:scenario/my-bounty" → "my-bounty". */
export const nameOf = (id: string) => id.split('/').pop() ?? ''

export const workshopApi = {
  list: (signal?: AbortSignal) => http<WorkshopSummary[]>('/api/scenarios', { signal }),
  get: (id: string, signal?: AbortSignal) => http<DraftScenario>(scenarioPath(id), { signal }),
  /** Saves a homebrew scenario and returns what is left to fix. */
  save: (scenario: DraftScenario) =>
    http<ScenarioReport>(`/api/scenarios/homebrew/${encodeURIComponent(nameOf(scenario.id))}`, {
      method: 'PUT',
      body: scenario,
    }),
  validate: (scenario: DraftScenario, signal?: AbortSignal) =>
    http<ScenarioReport>('/api/scenarios/validate', { method: 'POST', body: scenario, signal }),
  /** The print page as a finished PDF, made by the API (same query as /workshop/print). */
  pdf: (query: PrintQuery) => {
    const params = new URLSearchParams(
      Object.entries(query).filter((entry): entry is [string, string] => typeof entry[1] === 'string' && !!entry[1]),
    )
    return httpFile(`/api/print/pdf?${params}`)
  },
  remove: (id: string) => http<void>(`/api/scenarios/homebrew/${encodeURIComponent(nameOf(id))}`, { method: 'DELETE' }),
}
