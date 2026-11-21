import type { Language } from '@/shared/lib/localized'
import type { DraftNode } from '../api/types'
import { text } from './graph'

/** Mirrors Taleweaver.Api.Content.Store: lines of tiers and types, upgrades unlocked, random Unique Items. */
export interface DraftStore {
  items: { tiers?: string[]; types?: string[] }[]
  upgrades?: boolean
  unique?: { tier: string; count: number }
}

/** The node's Store, with its defaults. */
export function storeOf(node: DraftNode): DraftStore {
  const store = node.store as DraftStore | undefined
  return { ...store, items: store?.items ?? [] }
}

/** A new Story Round: the Store of the books' first one (all Mundane items, all Accessories, upgrades, 1 Mundane Unique). */
export function newStoryRound(id: string, language: Language, name: string): DraftNode {
  return {
    id,
    type: 'storyRound',
    name: text(language, name),
    narrative: text(language, ''),
    store: {
      items: [{ tiers: ['mundane'] }, { types: ['accessory'] }],
      upgrades: true,
      unique: { tier: 'mundane', count: 1 },
    },
    sideQuests: [],
    ventureForth: [{ label: text(language, ''), node: '' }],
  }
}
