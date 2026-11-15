import type { LocalizedText } from '@/shared/lib/localized'

/** Mirrors Taleweaver.Api.Features.Packs.GetBoardPieces.Response. */
export interface BoardPieces {
  tiles: Record<string, TilePiece>
  tokens: Record<string, TokenPiece>
  /** Figure of each family, by family. */
  figures: Record<string, FigurePiece>
}

export interface TilePiece {
  name: string
  w: number
  h: number
  image?: string
}

export interface TokenPiece {
  name: LocalizedText
  category: string
  size?: string
  image?: string
  /** One image per colour (objectives, totems, exits, loot…). */
  colors?: Record<string, string>
}

export interface FigurePiece {
  image: string
  baseSize?: string
}
