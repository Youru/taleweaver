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
  /** The icons printed on the front; absent when it prints none. */
  stats?: ItemStats | null
  backSide?: { name: LocalizedText; tags: string[]; text: LocalizedText; stats?: ItemStats | null } | null
  image?: string | null
  imageBack?: string | null
}

/** The icons of an item card (rulebook p. 30, 89), read on the cards. */
export interface ItemStats {
  /** Combat dice by colour; a black die means the Attack is always Empowered. */
  dice?: string[] | null
  hands?: number | null
  /** The Melee/Ranged icon ("either") takes the range and type of the other weapon. */
  attack?: 'melee' | 'ranged' | 'either' | null
  range?: number | null
  armor?: number | null
  /** "X": set by the card's text. */
  defense?: number | 'X' | null
  health?: number | null
  movement?: number | null
}

/** Mirrors Taleweaver.Api.Content.ItemUpgrade. */
export interface CatalogUpgrade {
  id: string
  name: LocalizedText
  cost?: number | null
  /** "WEAPON UPGRADE", "ARMOR UPGRADE"… */
  tags: string[]
  text?: LocalizedText | null
  image?: string | null
}

/** Mirrors Taleweaver.Api.Content.Discipline. */
export interface CatalogDiscipline {
  id: string
  name: LocalizedText
  /** assemblage, cruor, martial, sanctus, subterfuge, story. */
  tree: string
  level?: number | null
  story?: boolean
  /** XP printed on the card, when known. */
  cost?: number | null
  image?: string | null
}
