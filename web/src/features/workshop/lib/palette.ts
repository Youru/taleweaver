import type { BoardPieces } from '@/features/table'

/** Pieces laid on the board by a scenario (not the trackers and effects kept beside it), in palette order. */
export const BOARD_CATEGORIES = ['terrain', 'objective', 'totem', 'exit', 'loot', 'encounter', 'combatant']

/** One entry of the token palette: a type, in one colour or with one of its pictures. */
export interface TokenChoice {
  type: string
  color?: string
  size?: string
  /** A variant's picture (absent: the type's or the colour's own). */
  image?: string
  category: string
  /** The pack the picture comes from ("be1" for BE1's version of a UM1 terrain). */
  pack: string
  name: string
  /** What the variant shows, from its file name ("hole", "eggs"): read and searched. */
  variant?: string
  href?: string
}

/** The pack of a picture: the folder of an expansion, else the pack of the type. */
export function packOf(image: string | undefined, type: string): string {
  const expansion = image?.match(/expansions\/([^/]+)\//)?.[1]
  return expansion ?? type.split(':')[0] ?? type
}

/** "assets/expansions/be1/tokens/dangerous-1x1-hole.jpg" → "dangerous 1x1 hole". */
export const variantLabel = (image: string) =>
  (image.split('/').pop() ?? image).replace(/\.[a-z]+$/i, '').replace(/[-_]+/g, ' ')

/**
 * Every token the palette offers: each type once per colour, else once with its own picture, then
 * once per variant picture. Filtered by category, by pack and by the words searched (in the name, the
 * variant, the category or the pack), sorted by category then name.
 */
export function tokenChoices(
  pieces: BoardPieces | undefined,
  label: (type: string) => string,
  colorName: (color: string) => string,
  categoryName: (category: string) => string,
  filters: { search?: string; category?: string; pack?: string } = {},
): TokenChoice[] {
  const words = (filters.search ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
  return Object.entries(pieces?.tokens ?? {})
    .filter(([, piece]) => BOARD_CATEGORIES.includes(piece.category))
    .flatMap(([type, piece]): TokenChoice[] => {
      const name = label(type)
      const base = { type, size: piece.size, category: piece.category }
      const colors = Object.entries(piece.colors ?? {})
      const own: TokenChoice[] = colors.length
        ? colors.map(([color, image]) => ({
            ...base,
            color,
            pack: packOf(image, type),
            name: `${name} · ${colorName(color)}`,
            href: image || undefined,
          }))
        : [{ ...base, pack: packOf(piece.image, type), name, href: piece.image }]
      const variants = (piece.variants ?? []).map((image) => ({
        ...base,
        image,
        pack: packOf(image, type),
        name,
        variant: variantLabel(image),
        href: image,
      }))
      return [...own, ...variants]
    })
    .filter((c) => !filters.category || c.category === filters.category)
    .filter((c) => !filters.pack || c.pack === filters.pack)
    .filter((c) => {
      const haystack = `${c.name} ${c.variant ?? ''} ${categoryName(c.category)} ${c.pack}`.toLowerCase()
      return words.every((w) => haystack.includes(w))
    })
    .sort(
      (a, b) =>
        BOARD_CATEGORIES.indexOf(a.category) - BOARD_CATEGORIES.indexOf(b.category) ||
        a.name.localeCompare(b.name) ||
        Number(!!a.image) - Number(!!b.image) ||
        (a.variant ?? '').localeCompare(b.variant ?? '', undefined, { numeric: true }),
    )
}
