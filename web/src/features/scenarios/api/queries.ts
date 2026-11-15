import { useQuery } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { scenariosApi } from './scenariosApi'

export const scenarioKeys = {
  all: ['scenarios'] as const,
  list: () => [...scenarioKeys.all, 'list'] as const,
  detail: (id: string) => [...scenarioKeys.all, 'detail', id] as const,
}

export function useScenarios() {
  return useQuery({ queryKey: scenarioKeys.list(), queryFn: ({ signal }) => scenariosApi.list(signal) })
}

export function useScenario(id: MaybeRefOrGetter<string | undefined>) {
  return useQuery({
    queryKey: computed(() => scenarioKeys.detail(toValue(id) ?? '')),
    queryFn: ({ signal }) => scenariosApi.get(toValue(id)!, signal),
    enabled: computed(() => !!toValue(id)),
    staleTime: Infinity,
  })
}
