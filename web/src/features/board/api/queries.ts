import { useQuery } from '@tanstack/vue-query'
import { http } from '@/shared/api/http'
import type { BoardPieces } from './types'

export const boardKeys = {
  pieces: ['catalog', 'board'] as const,
}

/** Tiles, token types and figures of the enabled packs (they never change while the app runs). */
export function useBoardPieces() {
  return useQuery({
    queryKey: boardKeys.pieces,
    queryFn: ({ signal }) => http<BoardPieces>('/api/catalog/board', { signal }),
    staleTime: Infinity,
  })
}
