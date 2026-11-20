import type { InjectionKey } from 'vue'

/**
 * A selection of pages typed by the author, as in a print dialog: "5-12", "3, 7, 20-22", "30-" (to the end).
 * Returns undefined when the text is empty or unreadable (every page is printed).
 */
export function parsePages(text: string | undefined): ((page: number) => boolean) | undefined {
  const parts = (text ?? '').split(',').map((p) => p.trim()).filter(Boolean)
  if (!parts.length) return undefined
  const ranges: [number, number][] = []
  for (const part of parts) {
    const match = /^(\d+)\s*(?:-\s*(\d*))?$/.exec(part)
    if (!match) return undefined
    const from = Number(match[1])
    const to = match[2] === undefined ? from : match[2] === '' ? Number.POSITIVE_INFINITY : Number(match[2])
    if (to < from) return undefined
    ranges.push([from, to])
  }
  return (page) => ranges.some(([from, to]) => page >= from && page <= to)
}

/** Provided by the print page: is this page outside the pages to print? (false when printing them all) */
export const OUTSIDE_RANGE: InjectionKey<(page: number | undefined) => boolean> = Symbol('outsideRange')
