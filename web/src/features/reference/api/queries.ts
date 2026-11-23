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

/** The cleaned rulebook and its glossary (GET /api/rulebook). */
export interface Rulebook {
  /** Each chapter in English, and its translations line for line ("fr"). */
  chapters: { id: string; markdown: string; translations?: Record<string, string> | null }[]
  glossary: string
}

export function useRulebook() {
  return useQuery({
    queryKey: ['rulebook'],
    queryFn: ({ signal }) => http<Rulebook>('/api/rulebook', { signal }),
    staleTime: Infinity,
  })
}

export function useReference() {
  return useQuery({
    queryKey: ['catalog', 'reference'],
    queryFn: ({ signal }) => http<Reference>('/api/catalog/reference', { signal }),
    staleTime: Infinity,
  })
}
