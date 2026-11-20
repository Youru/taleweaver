import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftAction, DraftNode } from '../api/types'
import { mapOf, tokenKey } from './map'

/**
 * The tokens and events of an encounter as the workshop edits them: what each token does when an
 * adventurer activates it, and the events (trigger → conditions → actions) of the engine.
 */
export interface DraftTokenRule {
  mode?: string
  activation?: LocalizedText
  events?: string[]
  [field: string]: unknown
}

export interface DraftTrigger {
  on: string
  token?: string | string[]
  family?: string
  instance?: string
  round?: number
  area?: string
  rule?: string
  [field: string]: unknown
}

export interface DraftEvent {
  id: string
  trigger: DraftTrigger
  conditions?: unknown[]
  actions?: DraftAction[]
  once?: boolean
  hiddenText?: LocalizedText
  note?: LocalizedText
  [field: string]: unknown
}

/** How an adventurer activates a token (the GM reads it on the table). */
export const TOKEN_MODES = [
  'enter',
  'adjacent',
  'lineOfSight',
  'onSpaceEncounterAction',
  'adjacentEncounterAction',
  'endTurnOn',
  'none',
] as const

/** The triggers the engine fires, with the parameter each one takes. */
export const TRIGGERS: Record<string, 'token' | 'round' | 'family' | 'area' | 'rule' | null> = {
  tokenActivated: 'token',
  encounterStart: null,
  roundStart: 'round',
  roundEnd: 'round',
  combatantDefeated: 'family',
  adventurerDefeated: null,
  areaRevealed: 'area',
  hiddenRuleRead: 'rule',
}

/** The actions the workshop edits field by field; any other stays editable as JSON. */
export const SIMPLE_ACTIONS = [
  'showText',
  'revealArea',
  'placeToken',
  'removeToken',
  'spawn',
  'addUrgency',
  'gainGold',
  'gainXp',
  'gainItem',
  'restore',
  'setFlag',
  'endEncounter',
  'addEndCondition',
  'goToNode',
  'placeDiagram',
  'navigate',
  'readHiddenRule',
] as const
export type SimpleAction = (typeof SIMPLE_ACTIONS)[number]
export const isSimple = (action: DraftAction): action is DraftAction & { do: SimpleAction } =>
  (SIMPLE_ACTIONS as readonly string[]).includes(action.do)

/** A new action with the fields it needs. */
export function newAction(kind: SimpleAction, language: string): DraftAction {
  switch (kind) {
    case 'showText':
      return { do: kind, text: { [language]: '' } }
    case 'addUrgency':
      return { do: kind, amount: 1 }
    case 'gainGold':
      return { do: kind, amount: 10 }
    case 'gainXp':
      return { do: kind, amount: 1, each: true }
    case 'gainItem':
      return { do: kind, tiers: ['common'], type: 'consumable', count: 1 }
    case 'spawn':
      return { do: kind, family: '', count: 1 }
    default:
      return { do: kind }
  }
}

export function eventsOf(node: DraftNode): DraftEvent[] {
  return (node.events as DraftEvent[] | undefined) ?? []
}

export function tokenRulesOf(node: DraftNode): Record<string, DraftTokenRule> {
  return (node.tokens as Record<string, DraftTokenRule> | undefined) ?? {}
}

/** The tokens of the map an adventurer can activate (not the terrain), by key, with those already described. */
export function activatableKeys(node: DraftNode, categoryOf: (type: string) => string | undefined): string[] {
  const onMap = mapOf(node)
    .tokens.filter((t) => categoryOf(t.type) !== 'terrain')
    .map((t) => tokenKey(t.type, t.color))
  return [...new Set([...Object.keys(tokenRulesOf(node)), ...onMap])].sort()
}

/** The keys a trigger listens to. */
export function triggerTokens(trigger: DraftTrigger): string[] {
  return trigger.token === undefined ? [] : Array.isArray(trigger.token) ? trigger.token : [trigger.token]
}

/** The events a token sets off: those named by its rule, and those whose trigger listens to it. */
export function eventsOfToken(node: DraftNode, key: string): DraftEvent[] {
  const named = new Set(tokenRulesOf(node)[key]?.events ?? [])
  return eventsOf(node).filter(
    (e) => named.has(e.id) || (e.trigger.on === 'tokenActivated' && triggerTokens(e.trigger).includes(key)),
  )
}

export function freshEventId(node: DraftNode, prefix: string): string {
  const used = new Set(eventsOf(node).map((e) => e.id))
  const base = prefix.replace(/[^a-z0-9]+/gi, '-').toLowerCase()
  let n = 1
  while (used.has(`${base}-${n}`)) n++
  return `${base}-${n}`
}

export function setTokenRule(node: DraftNode, key: string, rule: DraftTokenRule | undefined): DraftNode {
  const rules = { ...tokenRulesOf(node) }
  if (rule) rules[key] = rule
  else delete rules[key]
  return { ...node, tokens: rules }
}

/** A new event set off by a token: its rule names it, so the table offers it on activation. */
export function addTokenEvent(node: DraftNode, key: string): { node: DraftNode; id: string } {
  const id = freshEventId(node, `ev-${key}`)
  const event: DraftEvent = { id, trigger: { on: 'tokenActivated', token: key }, actions: [] }
  const rule = tokenRulesOf(node)[key] ?? {}
  const withEvent = { ...node, events: [...eventsOf(node), event] }
  return { node: setTokenRule(withEvent, key, { ...rule, events: [...(rule.events ?? []), id] }), id }
}

export function addEvent(node: DraftNode, on: string): { node: DraftNode; id: string } {
  const id = freshEventId(node, `ev-${on}`)
  return { node: { ...node, events: [...eventsOf(node), { id, trigger: { on }, actions: [] }] }, id }
}

export function setEvent(node: DraftNode, id: string, event: DraftEvent): DraftNode {
  return { ...node, events: eventsOf(node).map((e) => (e.id === id ? event : e)) }
}

/** Removes an event, and its name from the token rules. */
export function removeEvent(node: DraftNode, id: string): DraftNode {
  const rules = Object.fromEntries(
    Object.entries(tokenRulesOf(node)).map(([key, rule]) => [
      key,
      rule.events ? { ...rule, events: rule.events.filter((e) => e !== id) } : rule,
    ]),
  )
  return { ...node, tokens: rules, events: eventsOf(node).filter((e) => e.id !== id) }
}

/** Where an action happens, as the workshop offers it. */
export type Spot =
  | { kind: 'gm' }
  | { kind: 'on' | 'adjacentTo'; token: string }
  | { kind: 'cell'; x: number; y: number }
  | { kind: 'other' }

export function spotOf(at: unknown): Spot {
  if (at === undefined || at === null) return { kind: 'gm' }
  if (typeof at === 'object') {
    const a = at as Record<string, unknown>
    const keys = Object.keys(a)
    if (typeof a.x === 'number' && typeof a.y === 'number' && keys.length === 2) return { kind: 'cell', x: a.x, y: a.y }
    for (const kind of ['on', 'adjacentTo'] as const)
      if (typeof a[kind] === 'string' && keys.length === 1 && (a[kind] as string).startsWith('token:'))
        return { kind, token: (a[kind] as string).slice(6) }
  }
  return { kind: 'other' }
}

export function atOf(spot: Spot): unknown {
  switch (spot.kind) {
    case 'gm':
      return undefined
    case 'cell':
      return { x: spot.x, y: spot.y }
    case 'on':
    case 'adjacentTo':
      return { [spot.kind]: `token:${spot.token}` }
    case 'other':
      return undefined
  }
}
