import { describe, expect, it } from 'vitest'
import type { DraftScenario } from '../api/types'
import {
  addChapter,
  chapterView,
  moveChapter,
  portalPositions,
  portalTarget,
  removeChapter,
  renameChapter,
} from './chapters'

const saga = (): DraftScenario => ({
  id: 'homebrew:scenario/saga',
  title: { en: 'Saga' },
  packs: [],
  start: 'a',
  chapters: [{ id: 'chapter-01', title: { en: 'One' } }, { id: 'chapter-02' }],
  nodes: [
    { id: 'a', type: 'scene', chapter: 'chapter-01', next: 'b' },
    { id: 'b', type: 'scene', chapter: 'chapter-01', next: 'c' },
    { id: 'c', type: 'scene', chapter: 'chapter-02', next: 'd' },
    { id: 'd', type: 'scene', chapter: 'chapter-02', next: '@victory' },
  ],
})

describe('chapterView', () => {
  it('shows the steps of the chapter and a portal to the next one', () => {
    const view = chapterView(saga(), 'chapter-01')

    expect(view.scenario.nodes.map((n) => n.id)).toEqual(['a', 'b'])
    expect(view.portals).toEqual([{ id: 'portal:out:c', direction: 'out', node: 'c', chapter: 'chapter-02' }])
  })

  it('starts a later chapter at the step the previous one leads to, with a portal back', () => {
    const view = chapterView(saga(), 'chapter-02')

    expect(view.scenario.start).toBe('c')
    expect(view.portals).toEqual([{ id: 'portal:in:b', direction: 'in', node: 'b', chapter: 'chapter-01' }])
  })

  it('shows everything without a chapter', () => {
    expect(chapterView(saga(), null).scenario.nodes).toHaveLength(4)
  })
})

describe('portals', () => {
  it('reads the step behind a portal', () => {
    expect(portalTarget('portal:out:the-mast')).toBe('the-mast')
    expect(portalTarget('the-mast')).toBe('the-mast')
  })

  it('places entries on the left and exits on the right', () => {
    const placed = portalPositions(
      [
        { id: 'portal:in:x', direction: 'in', node: 'x' },
        { id: 'portal:out:y', direction: 'out', node: 'y' },
        { id: 'portal:out:z', direction: 'out', node: 'z' },
      ],
      { a: { x: 0, y: 0 }, b: { x: 300, y: 0 } },
    )

    expect(placed['portal:in:x']!.x).toBeLessThan(0)
    expect(placed['portal:out:y']!.x).toBeGreaterThan(300)
    expect(placed['portal:out:z']!.y).toBeGreaterThan(placed['portal:out:y']!.y)
  })
})

describe('editing chapters', () => {
  it('puts every step in the first chapter added', () => {
    const single = removeChapter(removeChapter(saga(), 'chapter-02'), 'chapter-01')
    const next = addChapter(single, { en: 'Prologue' })

    expect(next.chapters).toEqual([{ id: 'chapter-01', title: { en: 'Prologue' } }])
    expect(next.nodes.every((n) => n.chapter === 'chapter-01')).toBe(true)
  })

  it('adds the next chapters empty, with a free folder name', () => {
    const next = addChapter(saga(), { en: 'Three' })

    expect(next.chapters?.map((c) => c.id)).toEqual(['chapter-01', 'chapter-02', 'chapter-03'])
    expect(next.nodes.filter((n) => n.chapter === 'chapter-03')).toEqual([])
  })

  it('hands the steps of a removed chapter to the previous one', () => {
    const next = removeChapter(saga(), 'chapter-02')

    expect(next.chapters?.map((c) => c.id)).toEqual(['chapter-01'])
    expect(next.nodes.every((n) => n.chapter === 'chapter-01')).toBe(true)
  })

  it('turns the scenario back into a single file when the last chapter goes', () => {
    const next = removeChapter(removeChapter(saga(), 'chapter-02'), 'chapter-01')

    expect(next.chapters).toBeUndefined()
    expect(next.nodes.some((n) => 'chapter' in n)).toBe(false)
  })

  it('renames and reorders', () => {
    const next = moveChapter(renameChapter(saga(), 'chapter-02', { en: 'Two' }), 'chapter-02', -1)

    expect(next.chapters).toEqual([{ id: 'chapter-02', title: { en: 'Two' } }, { id: 'chapter-01', title: { en: 'One' } }])
    expect(moveChapter(next, 'chapter-02', -1)).toBe(next)
  })
})
