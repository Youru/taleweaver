/**
 * The GM's manual. Its texts name the interface by key, so a renamed button renames itself here:
 * `[[home.play]]` is that label, `[[#room]]` a link to that section.
 */
/**
 * A screenshot of the app with numbered marks (public/help/<language>/<image>.png, taken by manual-shots/ on the
 * demonstration content): legend[0] explains mark 1, and so on.
 */
export interface ManualFigure {
  image: string
  caption: string
  legend?: string[]
}

export interface ManualPart {
  title?: string
  figure?: ManualFigure
  text?: string
  steps?: string[]
  list?: string[]
  note?: string
}

export interface ManualSection {
  id: string
  title: string
  lead: string
  parts: ManualPart[]
}

export interface Manual {
  groups: { title: string; sections: ManualSection[] }[]
}

export type Piece = { text: string } | { label: string } | { section: string }

export function piecesOf(text: string): Piece[] {
  // split() with a group: the references are at the odd places
  return text.split(/\[\[([^\]]+)\]\]/).flatMap<Piece>((piece, i) => {
    if (i % 2 === 0) return piece ? [{ text: piece }] : []
    return [piece.startsWith('#') ? { section: piece.slice(1) } : { label: piece }]
  })
}
