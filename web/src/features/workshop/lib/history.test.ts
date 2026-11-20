import { describe, expect, it } from 'vitest'
import { createHistory } from './history'

describe('history', () => {
  it('undoes and redoes changes in order', () => {
    const history = createHistory<string>()
    history.record('a', 0)
    history.record('b', 1000)

    expect(history.undo('c')).toBe('b')
    expect(history.undo('b')).toBe('a')
    expect(history.undo('a')).toBeUndefined()
    expect(history.redo('a')).toBe('b')
    expect(history.redo('b')).toBe('c')
    expect(history.canRedo).toBe(false)
  })

  it('merges quick changes into one step', () => {
    const history = createHistory<string>({ mergeWithinMs: 800 })
    history.record('', 0)
    history.record('h', 100)
    history.record('he', 200)

    expect(history.undo('hel')).toBe('')
    expect(history.canUndo).toBe(false)
  })

  it('forgets the redo steps after a new change, and keeps at most the limit', () => {
    const history = createHistory<number>({ limit: 2, mergeWithinMs: 0 })
    history.record(1, 10)
    history.record(2, 20)
    history.record(3, 30)
    history.undo(4)
    history.record(3, 40)

    expect(history.canRedo).toBe(false)
    expect(history.undo(5)).toBe(3)
    expect(history.undo(3)).toBe(2)
    expect(history.undo(2)).toBeUndefined()
  })
})
