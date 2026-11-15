import type { LocalizedText } from '@/shared/lib/localized'

/** The parts of Taleweaver.Api.Content.Combatant the front reads. */
export interface Combatant {
  id: string
  family: string
  name: LocalizedText
  subtitle?: LocalizedText
  kind: string
  lootLevel?: string
  activation?: string
  stats?: {
    health?: number | '*'
    defense?: number | '*'
    movement?: number | '*'
    armor?: number | '*'
    /** Die colours ("teal"), or "*" when another card gives them. */
    combatDice?: string[]
    convictionDice?: string[]
    castingDie?: string | null
  }
  /** Card image path ("assets/act1/combatants/…"). */
  image?: string
  passives?: { name: LocalizedText; text: LocalizedText }[]
  symbolAbilities?: SymbolAbilityCard[]
}

/** A symbol ability as printed: its cost ("book", "shield×2", "*star" for an Esper's symbols) and effect. */
export interface SymbolAbilityCard {
  cost: { symbol: string }[]
  effect: LocalizedText
}

export interface Effect {
  id: string
  name: LocalizedText
}
