import type { TokenPiece } from '../api/types'

/**
 * The picture a token shows: the one chosen for it on the map (a variant), else its colour's, else its
 * type's.
 */
export function tokenImage(token: { color?: string | null; image?: string | null }, piece: TokenPiece | undefined) {
  return token.image || (token.color && piece?.colors?.[token.color]) || piece?.image || undefined
}

/** Every picture a token type offers: its own, one per colour, then its variants (from every enabled pack). */
export function tokenImages(piece: TokenPiece | undefined): string[] {
  if (!piece) return []
  const colors = Object.values(piece.colors ?? {}).filter((image): image is string => !!image)
  return [...new Set([...(piece.image ? [piece.image] : []), ...colors, ...(piece.variants ?? [])])]
}
