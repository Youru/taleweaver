import { useQueries } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { http } from '@/shared/api/http'
import type { Combatant } from './types'

/** "um1:combatant/fragor-1-3-common" → "/api/packs/um1/combatants/fragor-1-3-common". */
export function combatantPath(id: string): string {
  const [pack = '', rest = ''] = id.split(':')
  return `/api/packs/${encodeURIComponent(pack)}/combatants/${encodeURIComponent(rest.split('/').pop() ?? '')}`
}

export const catalogKeys = {
  combatant: (id: string) => ['catalog', 'combatant', id] as const,
}

/** The cards of the given ids, by id (cards never change while the app runs). */
export function useCombatants(ids: MaybeRefOrGetter<string[]>) {
  const queries = useQueries({
    queries: computed(() =>
      [...new Set(toValue(ids))].map((id) => ({
        queryKey: catalogKeys.combatant(id),
        queryFn: ({ signal }: { signal: AbortSignal }) => http<Combatant>(combatantPath(id), { signal }),
        staleTime: Infinity,
      })),
    ),
  })
  return computed(() => {
    const byId = new Map<string, Combatant>()
    for (const query of queries.value) {
      if (query.data) byId.set(query.data.id, query.data)
    }
    return byId
  })
}
