import { http } from '@/shared/api/http'
import type { Scenario, ScenarioSummary } from './types'

/** "um1:scenario/blight-of-rhamsted" → "/api/scenarios/um1/blight-of-rhamsted". */
export function scenarioPath(id: string): string {
  const [pack = '', rest = ''] = id.split(':')
  const name = rest.split('/').pop() ?? ''
  return `/api/scenarios/${encodeURIComponent(pack)}/${encodeURIComponent(name)}`
}

export const scenariosApi = {
  list: (signal?: AbortSignal) => http<ScenarioSummary[]>('/api/scenarios', { signal }),
  get: (id: string, signal?: AbortSignal) => http<Scenario>(scenarioPath(id), { signal }),
}
