/**
 * Undo and redo over whole snapshots of the scenario. Changes that follow each other quickly (typing in a
 * field, dragging a piece) make a single step: only the state before the first one is kept.
 */
export interface History<T> {
  /** Called before a change, with the state it replaces. */
  record(before: T, now: number): void
  /** The state to go back to, or undefined; `current` becomes redoable. */
  undo(current: T): T | undefined
  redo(current: T): T | undefined
  readonly canUndo: boolean
  readonly canRedo: boolean
  clear(): void
}

export function createHistory<T>(options: { limit?: number; mergeWithinMs?: number } = {}): History<T> {
  const limit = options.limit ?? 100
  const mergeWithin = options.mergeWithinMs ?? 800
  let past: T[] = []
  let future: T[] = []
  let last = Number.NEGATIVE_INFINITY

  return {
    record(before, now) {
      future = []
      if (now - last > mergeWithin || !past.length) {
        past.push(before)
        if (past.length > limit) past = past.slice(past.length - limit)
      }
      last = now
    },
    undo(current) {
      const previous = past.pop()
      if (previous === undefined) return undefined
      future.push(current)
      last = Number.NEGATIVE_INFINITY
      return previous
    },
    redo(current) {
      const next = future.pop()
      if (next === undefined) return undefined
      past.push(current)
      last = Number.NEGATIVE_INFINITY
      return next
    },
    get canUndo() {
      return past.length > 0
    },
    get canRedo() {
      return future.length > 0
    },
    clear() {
      past = []
      future = []
      last = Number.NEGATIVE_INFINITY
    },
  }
}
