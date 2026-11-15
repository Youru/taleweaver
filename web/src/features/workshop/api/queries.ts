import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import type { DraftScenario } from './types'
import { workshopApi } from './workshopApi'

/** Same keys as the scenarios feature, so that the home page sees what the workshop saved. */
export const workshopKeys = {
  all: ['scenarios'] as const,
  list: () => [...workshopKeys.all, 'list'] as const,
  draft: (id: string) => [...workshopKeys.all, 'draft', id] as const,
  detail: (id: string) => [...workshopKeys.all, 'detail', id] as const,
}

export function useWorkshopList() {
  return useQuery({ queryKey: workshopKeys.list(), queryFn: ({ signal }) => workshopApi.list(signal) })
}

export function useDraft(id: MaybeRefOrGetter<string | undefined>) {
  return useQuery({
    queryKey: computed(() => workshopKeys.draft(toValue(id) ?? '')),
    queryFn: ({ signal }) => workshopApi.get(toValue(id)!, signal),
    enabled: computed(() => !!toValue(id)),
    // The editor works on its own copy: no refetch under the author's feet.
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  })
}

export function useSaveDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (scenario: DraftScenario) => workshopApi.save(scenario),
    onSuccess: (_, scenario) => {
      client.setQueryData(workshopKeys.draft(scenario.id), scenario)
      void client.invalidateQueries({ queryKey: workshopKeys.list() })
      void client.invalidateQueries({ queryKey: workshopKeys.detail(scenario.id) })
    },
  })
}

export function useDeleteDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => workshopApi.remove(id),
    onSuccess: () => client.invalidateQueries({ queryKey: workshopKeys.list() }),
  })
}
