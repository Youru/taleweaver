import { http } from '@/shared/api/http'
import { scenarioPath } from '@/features/scenarios'
import type { DraftScenario, ScenarioReport, WorkshopSummary } from './types'

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
  remove: (id: string) => http<void>(`/api/scenarios/homebrew/${encodeURIComponent(nameOf(id))}`, { method: 'DELETE' }),
}
