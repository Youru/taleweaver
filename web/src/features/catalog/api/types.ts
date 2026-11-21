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

/** An Effect of the rules (Poison, Barrier…), from the catalogue. */
export interface GameEffect {
  id: string
  name: LocalizedText
  text?: LocalizedText
  /** positive or negative */
  polarity?: string
  /** "status": resolves at the start of the figure's turn; "passive": always on. */
  condition?: string
}

/** Mirrors Taleweaver.Api.Content.InitiativeCard: a card of the initiative track (rulebook p. 36). */
export interface InitiativeCard {
  id: string
  label: string
  /** An adventurer's card (black border), else an opponent's (red). */
  adventurer: boolean
  /** Activation numbers printed on a group's card ([1, 3]). */
  group?: number[] | null
  family?: string | null
  hidden: boolean
  /** red or black when the game has both prints of the card. */
  border?: string | null
  /** Only a thumbnail is known. */
  lowres: boolean
  image?: string | null
}

/** Mirrors Taleweaver.Api.Content.Item: an item card (rulebook ch. 6). */
export interface CatalogItem {
  id: string
  name: LocalizedText
  /** mundane, common, uncommon, rare, story, wandering, monster, or none (accessories). */
  tier?: string | null
  /** weapon, armor, core, accessory, relic, consumable. */
  type: string
  unique?: boolean
  story?: boolean
  monsterOf?: string | null
  cost?: number | null
  rarity?: string | null
  tags: string[]
  text?: LocalizedText | null
  backSide?: { name: LocalizedText; tags: string[]; text: LocalizedText } | null
  image?: string | null
  imageBack?: string | null
}
