import type { Language, LocalizedText } from '@/shared/lib/localized'
import { pick } from '@/shared/lib/localized'
import type { DraftChapter, DraftNode, DraftScenario, Position } from '../api/types'
import { COLUMN, ROW, exitsOf, isEnding } from './graph'

/**
 * A long scenario (an act of the adventure book) cut into chapters, saved as a campaign folder: one
 * folder per chapter, one file per step. The graph shows one chapter at a time; a link to a step of
 * another chapter becomes a "portal", which opens that chapter.
 */
export const chaptersOf = (scenario: DraftScenario): DraftChapter[] => scenario.chapters ?? []

export const hasChapters = (scenario: DraftScenario) => chaptersOf(scenario).length > 0

/** A step leading to (out) or reached from (in) a step of another chapter. */
export interface Portal {
  /** Graph id: "portal:out:<target>" or "portal:in:<source>". */
  id: string
  direction: 'in' | 'out'
  /** The step on the other side. */
  node: string
  chapter?: string
}

export const portalId = (direction: Portal['direction'], node: string) => `portal:${direction}:${node}`

/** "portal:out:the-mast" → "the-mast" (a step id is returned as it is). */
export function portalTarget(id: string): string {
  const match = /^portal:(in|out):(.+)$/.exec(id)
  return match ? match[2]! : id
}

export interface ChapterView {
  /** The chapter's steps only, the start moved to its first entry so that the layout begins there. */
  scenario: DraftScenario
  portals: Portal[]
}

/** What the graph shows of a chapter (null: the whole scenario). */
export function chapterView(scenario: DraftScenario, chapter: string | null): ChapterView {
  if (!chapter || !hasChapters(scenario)) return { scenario, portals: [] }
  const inside = scenario.nodes.filter((n) => n.chapter === chapter)
  const ids = new Set(inside.map((n) => n.id))
  const chapterOf = new Map(scenario.nodes.map((n) => [n.id, n.chapter]))
  const out = new Map<string, Portal>()
  for (const node of inside)
    for (const exit of exitsOf(node))
      if (exit.target && !isEnding(exit.target) && !ids.has(exit.target) && !out.has(exit.target))
        out.set(exit.target, {
          id: portalId('out', exit.target),
          direction: 'out',
          node: exit.target,
          chapter: chapterOf.get(exit.target),
        })
  const entries: Portal[] = []
  const entered = new Set<string>()
  for (const node of scenario.nodes)
    if (!ids.has(node.id) && exitsOf(node).some((e) => e.target && ids.has(e.target))) {
      entries.push({ id: portalId('in', node.id), direction: 'in', node: node.id, chapter: node.chapter })
      for (const e of exitsOf(node)) if (e.target && ids.has(e.target)) entered.add(e.target)
    }
  const start = ids.has(scenario.start)
    ? scenario.start
    : (inside.find((n) => entered.has(n.id)) ?? inside[0])?.id ?? ''
  return { scenario: { ...scenario, nodes: inside, start }, portals: [...entries, ...out.values()] }
}

/** Entries on the left of the chapter, exits on the right, one under the other. */
export function portalPositions(portals: Portal[], positions: Record<string, Position>): Record<string, Position> {
  const xs = Object.values(positions).map((p) => p.x)
  const left = (xs.length ? Math.min(...xs) : 0) - COLUMN
  const right = (xs.length ? Math.max(...xs) : 0) + COLUMN
  const placed: Record<string, Position> = {}
  let rowIn = 0
  let rowOut = 0
  for (const portal of portals)
    placed[portal.id] =
      portal.direction === 'in' ? { x: left, y: rowIn++ * ROW } : { x: right, y: rowOut++ * ROW }
  return placed
}

/** "chapter-01", "chapter-02"… the next folder name not used yet. */
export function freshChapterId(scenario: DraftScenario): string {
  const used = new Set(chaptersOf(scenario).map((c) => c.id))
  let n = 1
  while (used.has(chapterName(n))) n++
  return chapterName(n)
}
const chapterName = (n: number) => `chapter-${String(n).padStart(2, '0')}`

/**
 * A new chapter at the end. The first one turns the scenario into a campaign folder: every step goes
 * into it, so that none is left outside.
 */
export function addChapter(scenario: DraftScenario, title: LocalizedText): DraftScenario {
  const chapter: DraftChapter = { id: freshChapterId(scenario), title }
  const first = !hasChapters(scenario)
  return {
    ...scenario,
    chapters: [...chaptersOf(scenario), chapter],
    nodes: first ? scenario.nodes.map((n) => ({ ...n, chapter: chapter.id })) : scenario.nodes,
  }
}

export function renameChapter(scenario: DraftScenario, id: string, title: LocalizedText): DraftScenario {
  return { ...scenario, chapters: chaptersOf(scenario).map((c) => (c.id === id ? { ...c, title } : c)) }
}

/**
 * Removes a chapter: its steps go to the previous chapter (the next one for the first). Removing the
 * last one turns the scenario back into a single file.
 */
export function removeChapter(scenario: DraftScenario, id: string): DraftScenario {
  const chapters = chaptersOf(scenario)
  const index = chapters.findIndex((c) => c.id === id)
  if (index < 0) return scenario
  const rest = chapters.filter((c) => c.id !== id)
  if (!rest.length) {
    const single = { ...scenario, nodes: scenario.nodes.map(withoutChapter) }
    delete single.chapters
    return single
  }
  const heir = rest[Math.max(0, index - 1)]!.id
  return {
    ...scenario,
    chapters: rest,
    nodes: scenario.nodes.map((n) => (n.chapter === id ? { ...n, chapter: heir } : n)),
  }
}

/** Moves a chapter up (-1) or down (+1) in the list. */
export function moveChapter(scenario: DraftScenario, id: string, by: -1 | 1): DraftScenario {
  const chapters = [...chaptersOf(scenario)]
  const from = chapters.findIndex((c) => c.id === id)
  const to = from + by
  if (from < 0 || to < 0 || to >= chapters.length) return scenario
  ;[chapters[from], chapters[to]] = [chapters[to]!, chapters[from]!]
  return { ...scenario, chapters }
}

function withoutChapter(node: DraftNode): DraftNode {
  const rest = { ...node }
  delete rest.chapter
  return rest
}

/** "Chapter 1" or the folder name when it has no title. */
export function chapterCaption(chapter: DraftChapter | undefined, language: Language): string {
  return chapter ? pick(chapter.title, language) || chapter.id : ''
}

/** How many steps each chapter holds. */
export function chapterSizes(scenario: DraftScenario): Map<string, number> {
  const sizes = new Map<string, number>()
  for (const node of scenario.nodes) if (node.chapter) sizes.set(node.chapter, (sizes.get(node.chapter) ?? 0) + 1)
  return sizes
}
