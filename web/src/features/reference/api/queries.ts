import { useQuery } from '@tanstack/vue-query'
import { http } from '@/shared/api/http'
import type { LocalizedText } from '@/shared/lib/localized'

export interface RuleEntry {
  id: string
  name: LocalizedText
  text?: LocalizedText | null
}

/** The rulebook's reference tables (GET /api/catalog/reference). */
export interface Reference {
  effects: RuleEntry[]
  abilityTokens: RuleEntry[]
  terrainTypes: RuleEntry[]
  tags: RuleEntry[]
  combatantTokens: RuleEntry[]
}

export function useReference() {
  return useQuery({
    queryKey: ['catalog', 'reference'],
    queryFn: ({ signal }) => http<Reference>('/api/catalog/reference', { signal }),
    staleTime: Infinity,
  })
}
