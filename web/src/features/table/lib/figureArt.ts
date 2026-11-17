import type { BoardPieces } from '../api/types'

/**
 * The picture of a figure on a board: its round portrait, as the books draw it on their diagrams,
 * else the standee (only its left part fits a disc: `portrait` false). Names are tried in order (a
 * figure's own name, then its family).
 */
export function figureArt(
  pieces: BoardPieces | undefined,
  ...names: (string | undefined)[]
): { image?: string; portrait: boolean } {
  const found = names.flatMap((n) => (n && pieces?.figures[n] ? [pieces.figures[n]] : []))
  const portrait = found.find((f) => f.portrait)?.portrait
  if (portrait) return { image: portrait, portrait: true }
  return { image: found.find((f) => f.image)?.image, portrait: false }
}
