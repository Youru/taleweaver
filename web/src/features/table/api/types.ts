import type { LocalizedText } from '@/shared/lib/localized'

/** Mirrors Taleweaver.Api.Features.Packs.GetBoardPieces.Response. */
export interface BoardPieces {
  tiles: Record<string, TilePiece>
  tokens: Record<string, TokenPiece>
  /** Figure of each family, by family. */
  figures: Record<string, FigurePiece>
  /** Families of combatants the setup can place, by family. */
  families: Record<string, FamilyPiece>
}

export interface TilePiece {
  name: string
  w: number
  h: number
  image?: string
  /** Scenery of this side (forest, crypt…). */
  theme?: string
  /** The other side of the same tile. */
  pairedSide?: string
  /**
   * Terrain printed on the side, tile not rotated: rows top to bottom, one letter a square
   * (. normal, h hindering, w water, m muck, o obstructing, d dangerous, s special). Surveyed by eye.
   */
  terrain?: string[]
  /** Elevation of each square (digits), as `terrain`; absent when the side is flat. */
  elevation?: string[]
}

export interface TokenPiece {
  name: LocalizedText
  category: string
  size?: string
  image?: string
  /** One image per colour (objectives, totems, exits, loot…). */
  colors?: Record<string, string>
  /** Its other pictures (another print, an expansion's version): a map token may show one. */
  variants?: string[]
}

export interface FigurePiece {
  image: string
  baseSize?: string
}

export interface FamilyPiece {
  name: LocalizedText
  /** command or intelligent. */
  kind: string
  baseSize?: string
}
