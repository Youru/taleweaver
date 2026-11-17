import { assetUrl } from '@/shared/api/assets'

/** A picture of the Encounter template (assets/templates/encounter/…). */
export const templateArt = (path: string) => assetUrl(`assets/templates/encounter/${path}`)

/**
 * The paper of a printed sheet. The template has one background per side of the open booklet, its
 * ornament along the outer edge: the left one for the even pages, the right one for the odd pages
 * (and for a sheet printed alone, without a number). Each is the template's layer as a JPEG
 * (scripts/flatten-print-background.mjs): as PNG they weighed 14 MB each in every PDF.
 */
export const pageSide = (folio: number | undefined): 'left' | 'right' =>
  folio !== undefined && folio % 2 === 0 ? 'left' : 'right'

export const pageBackground = (folio: number | undefined) => ({
  backgroundImage: `url("${templateArt(`print-background-${pageSide(folio)}.jpg`)}")`,
})
