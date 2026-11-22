import type { LocalizedText } from '@/shared/lib/localized'
import type { Cell } from './types'

/** Mirrors Taleweaver.Api.Engine.AllyState: a Command Combatant, on the adventurers' side (rulebook p. 58). */
export interface AllyState {
  exhaustedCards?: string[]
  usedCards?: string[]
  abilityTokens?: Record<string, number> | null
  id: string
  card: string
  family: string
  name: LocalizedText
  /** "ally:<initiative card>". */
  trackCard: string
  owner?: string | null
  source: 'summoning' | 'felledAfterlife' | 'story'
  esper: boolean
  health?: number | null
  force?: number | null
  damage: number
  stamina: number
  staminaRecovery: number
  staminaMax: number
  effects: string[]
  position?: Cell | null
  figure?: string | null
  defeated: boolean
}

export type AllyOptionKind = 'summoning' | 'felledAfterlife' | 'earnConduit' | 'conduit' | 'exalted'

export interface AllyOption {
  adventurer: string
  kind: AllyOptionKind
  cards: string[]
}

/** A Command card the GM may bring by hand; hidden: a spoiler. */
export interface AllyCard {
  id: string
  name: LocalizedText
  image?: string | null
  hidden: boolean
}
