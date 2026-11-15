import { useQuery } from '@tanstack/vue-query'
import { http } from '@/shared/api/http'
import type { Dice } from './types'

export const duelKeys = {
  dice: ['catalog', 'dice'] as const,
}

/** The custom dice (they never change while the app runs). */
export function useDice() {
  return useQuery({
    queryKey: duelKeys.dice,
    queryFn: ({ signal }) => http<Dice>('/api/catalog/dice', { signal }),
    staleTime: Infinity,
  })
}
