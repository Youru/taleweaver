import type { BoardLayout } from '@/features/table'
import { sight, type Board } from './grid'

/** A token an adventurer sees: it activates at once (Totems, rulebook p. 14). */
export interface SeenToken {
  /** The key the scenario uses ("totem:blue"). */
  key: string
  /** Who sees it. */
  by: string
}

/**
 * The tokens that activate by Line of Sight (Totems, unless the scenario says otherwise) that an
 * adventurer on the table sees: no Range limit, blocked as an attack's line is (p. 43). Only a
 * suggestion: the GM confirms, since the app knows the squares only if they were followed.
 */
export function seenTokens(
  board: Board,
  layout: BoardLayout,
  modeOf: (key: string) => string | undefined,
): SeenToken[] {
  const viewers = board.figures.filter((f) => f.side === 'adventurer')
  const seen: SeenToken[] = []
  for (const token of layout.tokens) {
    const mode = modeOf(token.key) ?? (token.key.startsWith('totem:') ? 'lineOfSight' : undefined)
    if (mode !== 'lineOfSight' || seen.some((s) => s.key === token.key)) continue
    const target = {
      id: `token:${token.id}`,
      name: token.key,
      side: 'combatant' as const,
      position: { x: token.x, y: token.y },
      size: Math.max(token.w, token.h, 1),
      damage: 0,
      initiative: 0,
    }
    const viewer = viewers.find((v) => sight(board, v, target).clear)
    if (viewer) seen.push({ key: token.key, by: viewer.name })
  }
  return seen
}
