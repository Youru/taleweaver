import { pick, type Language, type LocalizedText } from '@/shared/lib/localized'
import type { DraftAction, DraftNode } from '../api/types'

export interface DraftAchievement {
  id: string
  name: LocalizedText
  condition?: LocalizedText
  hiddenText?: LocalizedText
  actions?: DraftAction[]
}

export const achievementsOf = (node: DraftNode) => (node.achievements as DraftAchievement[] | undefined) ?? []

export function addAchievement(list: DraftAchievement[], language: Language): DraftAchievement[] {
  const used = new Set(list.map((item) => item.id))
  let n = 1
  while (used.has(`achievement-${n}`)) n++
  return [...list, { id: `achievement-${n}`, name: { [language]: '' } }]
}

const isXp = (action: DraftAction) => action.do === 'gainXp' && !action.each

/** The XP the adventurer wins, as typed on the sheet. */
export function xpOf(item: DraftAchievement): number {
  const amount = item.actions?.find(isXp)?.amount
  return typeof amount === 'number' ? amount : 0
}

/** Changes the XP and keeps the other rewards, written in the JSON. */
export function withXp(item: DraftAchievement, xp: number): DraftAchievement {
  const others = (item.actions ?? []).filter((action) => !isXp(action))
  const actions = xp > 0 ? [{ do: 'gainXp', amount: xp } as DraftAction, ...others] : others
  const next = { ...item, actions }
  if (!actions.length) delete (next as DraftAchievement).actions
  return next
}

/** As the booklet prints them. */
export const printedAchievements = (node: DraftNode, language: Language) =>
  achievementsOf(node).map((item) => ({
    name: pick(item.name, language),
    condition: pick(item.condition, language),
    hiddenText: pick(item.hiddenText, language),
  }))
