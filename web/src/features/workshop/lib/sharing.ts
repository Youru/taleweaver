import type { DraftScenario } from '../api/types'

/** "homebrew:scenario/my-bounty" → "my-bounty.taleweaver-scenario.json": the file another workshop imports. */
export function exportedFileName(id: string): string {
  return `${id.split('/').pop() || 'scenario'}.taleweaver-scenario.json`
}

export type Imported = { scenario: DraftScenario } | { error: 'notJson' | 'notAScenario' }

/**
 * A shared file, read before it goes anywhere: JSON with what every scenario has (a title, a start, nodes).
 * The rest is the server's to check (SaveScenario), and the workshop's checker shows what is missing (packs, cards).
 */
export function readImported(content: string): Imported {
  let value: unknown
  try {
    value = JSON.parse(content)
  } catch {
    return { error: 'notJson' }
  }
  const s = value as Partial<DraftScenario> | null
  if (
    !s ||
    typeof s !== 'object' ||
    Array.isArray(s) ||
    !s.title ||
    typeof s.title !== 'object' ||
    typeof s.start !== 'string' ||
    !Array.isArray(s.nodes)
  )
    return { error: 'notAScenario' }
  return {
    scenario: { ...(s as DraftScenario), kind: s.kind ?? 'bounty', packs: Array.isArray(s.packs) ? s.packs : [] },
  }
}
