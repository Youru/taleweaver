import type { LocalizedText } from '@/shared/lib/localized'

/** One face of a custom die (Taleweaver.Api.Content.DieFace). The black die only shows symbols, or the skull. */
export interface DieFace {
  value: number
  shield: number
  book: number
  star: number
  skull: boolean
}

export interface Die {
  id: string
  name: LocalizedText
  image?: string
  faces: DieFace[]
}

/** The dice of the enabled packs, by colour ("white", "black"…). */
export type Dice = Record<string, Die>
