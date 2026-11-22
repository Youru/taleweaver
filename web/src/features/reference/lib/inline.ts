import { assetUrl } from '@/shared/api/assets'
import { termPattern } from './text'

/** A piece of a line of the book: words, an icon, or a link to another rule. */
export interface Piece {
  text: string
  bold?: boolean
  mark?: boolean
  icon?: string
  link?: { page: number; name?: string }
}

const iconFiles: Record<string, string> = {
  Melee: 'range-and-hand-icons/melee-icon',
  Ranged: 'range-and-hand-icons/range-icon',
  'Melee/Ranged': 'range-and-hand-icons/melee-ranged-icon',
  Shield: 'combat-symbols/shield-icon',
  Book: 'combat-symbols/book-icon',
  Star: 'combat-symbols/burst-icon',
  Skull: 'combat-symbols/skull',
  Armor: 'stat-icons-text/armor-icon',
  Defense: 'stat-icons-text/defense-icon',
  Health: 'stat-icons-text/hp-icon',
  Movement: 'stat-icons-text/movement-icon',
  Exhaust: 'text-tag-icons/exhaust-text-icon',
  'Per Encounter': 'text-tag-icons/per-encounter-text-icon',
}
const dice = new Set(['black', 'blue', 'green', 'grey', 'orange', 'purple', 'red', 'teal', 'white'])

function iconOf(name: string, before: string): string | undefined {
  // The book names the colour just before the die: "a **BLACK** [Die]"
  const colour = /([a-z]+)[^a-z]*$/i.exec(before)?.[1]?.toLowerCase() ?? ''
  const file = name === 'Die' ? (dice.has(colour) ? `dice/${colour}-dice-icon` : undefined) : iconFiles[name]
  return file && assetUrl(`assets/templates/discipline-card/icons/${file}.png`)
}

/** "**Being Defeated** Pg. 69", "Pg. 95", "p. 12", "[Shield]", "**SP**". */
export function readInline(text: string, marks: string[] = []): Piece[] {
  const pieces: Piece[] = []
  const token = /\*\*|\[([A-Z][A-Za-z/ ]*)\]|\(?(?:Pg\.?|\bp\.) ?(?:\*\*)?(\d{1,3})(?:\*\*)?\)?/g
  let bold = false
  let name: string | undefined
  let at = 0
  for (const match of text.matchAll(token)) {
    const words = text.slice(at, match.index)
    if (words) pieces.push({ text: words, bold })
    if (words && bold) name = words.replace(/[.,:]$/, '')
    at = match.index + match[0].length
    if (match[0] === '**') {
      bold = !bold
    } else if (match[1]) {
      pieces.push({ text: match[1], bold, icon: iconOf(match[1], text.slice(0, match.index)) })
    } else {
      pieces.push({ text: match[0].replaceAll('*', ''), link: { page: Number(match[2]), name } })
      name = undefined
    }
  }
  if (at < text.length) pieces.push({ text: text.slice(at), bold })
  return marks.length ? markPieces(pieces, marks) : pieces
}

function markPieces(pieces: Piece[], marks: string[]): Piece[] {
  const sources = [...marks].sort((a, b) => b.length - a.length).map((term) => termPattern(term).source)
  const pattern = new RegExp(sources.join('|'), 'gi')
  return pieces.flatMap((piece) => {
    if (piece.icon || piece.link) return [piece]
    const parts: Piece[] = []
    let at = 0
    for (const match of piece.text.matchAll(pattern)) {
      if (match.index > at) parts.push({ ...piece, text: piece.text.slice(at, match.index) })
      parts.push({ ...piece, text: match[0], mark: true })
      at = match.index + match[0].length
    }
    if (at < piece.text.length) parts.push({ ...piece, text: piece.text.slice(at) })
    return parts
  })
}
