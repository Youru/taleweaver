import { useQueries, useQuery } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { http } from '@/shared/api/http'
import type { CatalogDiscipline, CatalogItem, CatalogUpgrade, Combatant, GameEffect, InitiativeCard } from './types'

/** "um1:combatant/fragor-1-3-common" → "/api/packs/um1/combatants/fragor-1-3-common". */
export function combatantPath(id: string): string {
  const [pack = '', rest = ''] = id.split(':')
  return `/api/packs/${encodeURIComponent(pack)}/combatants/${encodeURIComponent(rest.split('/').pop() ?? '')}`
}

export const catalogKeys = {
  combatant: (id: string) => ['catalog', 'combatant', id] as const,
  effects: ['catalog', 'effects'] as const,
  initiative: ['catalog', 'initiative'] as const,
  items: ['catalog', 'items'] as const,
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

/** The Effects of the rules (they never change while the app runs). */
export function useEffects() {
  return useQuery({
    queryKey: catalogKeys.effects,
    queryFn: ({ signal }) => http<GameEffect[]>('/api/catalog/effects', { signal }),
    staleTime: Infinity,
  })
}

/** The initiative cards of the enabled packs (they never change while the app runs). */
export function useInitiativeCards() {
  return useQuery({
    queryKey: catalogKeys.initiative,
    queryFn: ({ signal }) => http<InitiativeCard[]>('/api/catalog/initiative', { signal }),
    staleTime: Infinity,
  })
}

/** The item cards of the enabled packs (they never change while the app runs). */
export function useItems() {
  return useQuery({
    queryKey: catalogKeys.items,
    queryFn: ({ signal }) => http<CatalogItem[]>('/api/catalog/items', { signal }),
    staleTime: Infinity,
  })
}

/** The Item Upgrades of the enabled packs (rulebook p. 28). */
export function useItemUpgrades() {
  return useQuery({
    queryKey: ['catalog', 'itemUpgrades'] as const,
    queryFn: ({ signal }) => http<CatalogUpgrade[]>('/api/catalog/item-upgrades', { signal }),
    staleTime: Infinity,
  })
}

/** The Discipline cards of the enabled packs (rulebook p. 24, 33). */
export function useDisciplines() {
  return useQuery({
    queryKey: ['catalog', 'disciplines'] as const,
    queryFn: ({ signal }) => http<CatalogDiscipline[]>('/api/catalog/disciplines', { signal }),
    staleTime: Infinity,
  })
}
