import type { Language } from '@/shared/lib/localized'
import type { DraftNode, DraftScenario } from '../api/types'
import { captionOf } from './graph'

export interface SearchHit {
  id: string
  type: string
  caption: string
  /** Where the words were found: the title, the id, or somewhere in the texts (with an excerpt). */
  where: 'caption' | 'id' | 'text'
  excerpt?: string
}

/** Lower case, without accents: "Éclat" finds "eclat". */
const fold = (value: string) =>
  value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()

/** Every text of a node in the given language (narrative, intro, events, rules, options…), any depth. */
function textsOf(value: unknown, language: Language, out: string[] = []): string[] {
  if (Array.isArray(value)) for (const item of value) textsOf(item, language, out)
  else if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>
    const own = record[language]
    if (typeof own === 'string') out.push(own)
    else for (const [key, item] of Object.entries(record)) if (key !== 'editor') textsOf(item, language, out)
  }
  return out
}

function excerpt(text: string, at: number, length: number): string {
  const start = Math.max(0, at - 30)
  const end = Math.min(text.length, at + length + 50)
  return `${start > 0 ? '…' : ''}${text.slice(start, end).replace(/\s+/g, ' ')}${end < text.length ? '…' : ''}`
}

/** The steps matching every word of the query: titles and ids first, then the texts. */
export function searchNodes(scenario: DraftScenario, query: string, language: Language, limit = 20): SearchHit[] {
  const words = fold(query).split(/\s+/).filter(Boolean)
  if (!words.length) return []
  const hits: SearchHit[] = []
  const matches = (value: string) => {
    const folded = fold(value)
    return words.every((w) => folded.includes(w))
  }
  for (const node of scenario.nodes as DraftNode[]) {
    const caption = captionOf(node, language)
    const base = { id: node.id, type: node.type, caption }
    if (matches(caption)) hits.push({ ...base, where: 'caption' })
    else if (matches(node.id)) hits.push({ ...base, where: 'id' })
    else {
      const text = textsOf(node, language).find(matches)
      if (text) hits.push({ ...base, where: 'text', excerpt: excerpt(text, fold(text).indexOf(words[0]!), words[0]!.length) })
    }
  }
  const rank = { caption: 0, id: 1, text: 2 }
  return hits.sort((a, b) => rank[a.where] - rank[b.where]).slice(0, limit)
}
