import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftAction, DraftNode } from '../api/types'

/**
 * The Curiously Hidden Rules of an encounter (data model §3.6): read only when an event or a choice says so
 * (`readHiddenRule`), printed under the red filter. A rule may be part of another one (its A or B).
 */
export interface DraftHiddenRule {
  id: string
  name?: LocalizedText
  text?: LocalizedText
  partOf?: string
  actions?: DraftAction[]
  [field: string]: unknown
}

export function hiddenRulesOf(node: DraftNode): DraftHiddenRule[] {
  return (node.hiddenRules as DraftHiddenRule[] | undefined) ?? []
}

function withRules(node: DraftNode, rules: DraftHiddenRule[]): DraftNode {
  const next: DraftNode = { ...node, hiddenRules: rules }
  if (!rules.length) delete next.hiddenRules
  return next
}

/** A new rule, "hidden-rule-N" (or "<parent>-a", "-b"… when part of another). */
export function addHiddenRule(node: DraftNode, language: string, partOf?: string): DraftNode {
  const rules = hiddenRulesOf(node)
  const used = new Set(rules.map((r) => r.id))
  let id: string
  if (partOf) {
    let letter = 0
    do id = `${partOf}-${String.fromCharCode(97 + letter++)}`
    while (used.has(id))
  } else {
    let n = 1
    while (used.has(`hidden-rule-${n}`)) n++
    id = `hidden-rule-${n}`
  }
  const rule: DraftHiddenRule = { id, name: { [language]: '' }, text: { [language]: '' } }
  if (partOf) rule.partOf = partOf
  return withRules(node, [...rules, rule])
}

export function updateHiddenRule(node: DraftNode, id: string, fields: Partial<DraftHiddenRule>): DraftNode {
  return withRules(
    node,
    hiddenRulesOf(node).map((rule) => {
      if (rule.id !== id) return rule
      const next: DraftHiddenRule = { ...rule, ...fields }
      for (const [k, v] of Object.entries(fields)) {
        if (v === undefined || v === '' || (Array.isArray(v) && !v.length)) delete next[k]
      }
      return next
    }),
  )
}

/** Removes a rule; the rules that were part of it become rules of their own. */
export function removeHiddenRule(node: DraftNode, id: string): DraftNode {
  return withRules(
    node,
    hiddenRulesOf(node)
      .filter((r) => r.id !== id)
      .map((r) => {
        if (r.partOf !== id) return r
        const rest: DraftHiddenRule = { ...r }
        delete rest.partOf
        return rest
      }),
  )
}
