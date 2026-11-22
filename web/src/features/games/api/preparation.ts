import type { LocalizedText } from '@/shared/lib/localized'

/** Mirrors Taleweaver.Api.Engine.PartyPreparation: the party being made, and what the scenario's rules allow. */
export interface PartyPreparation {
  choices: PartyChoice[]
  hiddenShown: boolean
  min: number
  max: number
  gold: number
  goldLeft: number
  xp: number
  custom: boolean
  adventurers: AdventurerOffer[]
  kits: StarterKit[]
  itemsOpen: boolean
  items: { item: string; price: number }[]
  maxItems?: number | null
  disciplines: string[]
  maxDisciplines?: number | null
  /** The error code of what prevents the departure. */
  missing?: string | null
}

export interface PartyChoice {
  adventurer: string
  kit?: string | null
  items?: string[] | null
  disciplines?: string[] | null
}

/** A change of a choice: what is left out is kept, an empty kit takes the kit away. */
export interface ChoiceChange {
  kit?: string
  items?: string[]
  disciplines?: string[]
}

export interface AdventurerOffer {
  name: string
  id: string
  image?: string | null
  /** Still to unlock in the story. */
  hidden: boolean
  /** The story takes them along. */
  required: boolean
}

export interface StarterKit {
  id: string
  name: LocalizedText
  adventurer?: string | null
  items: string[]
  disciplines: string[]
}

export type PreparationCommand =
  | ({ type: 'choose'; adventurer: string } & ChoiceChange)
  | { type: 'remove'; adventurer: string }
  | { type: 'hidden'; shown: boolean }
  | { type: 'setOut' }
