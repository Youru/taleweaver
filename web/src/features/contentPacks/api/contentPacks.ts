import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { http, httpUpload } from '@/shared/api/http'
import type { LocalizedText } from '@/shared/lib/localized'

/** A content pack installed beside the app (Features/ContentPacks): packs, scenarios, pictures, rules. */
export interface ContentPack {
  id: string
  name: LocalizedText
  version?: string | null
  /** The catalogue packs it brings. */
  packs: string[]
  scenarios: number
  pictures: number
}

const path = '/api/content-packs'
const key = ['content-packs'] as const

export function useContentPacks() {
  return useQuery({ queryKey: key, queryFn: ({ signal }) => http<ContentPack[]>(path, { signal }) })
}

/** Installing or removing changes the catalogue and the scenarios: everything is read again. */
export function useInstallContentPack() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => httpUpload<ContentPack>(path, file),
    onSuccess: () => client.invalidateQueries(),
  })
}

export function useRemoveContentPack() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => http<void>(`${path}/${encodeURIComponent(id)}`, { method: 'DELETE' }),
    onSuccess: () => client.invalidateQueries(),
  })
}
