import { describe, expect, it } from 'vitest'
import { parsePages } from './pageRange'

const pages = (text: string, upTo = 12) => {
  const inRange = parsePages(text)
  return inRange ? Array.from({ length: upTo }, (_, i) => i + 1).filter(inRange) : undefined
}

describe('parsePages', () => {
  it('reads single pages, ranges and open ranges', () => {
    expect(pages('3, 5-7, 11-')).toEqual([3, 5, 6, 7, 11, 12])
  })

  it('prints everything when empty or unreadable', () => {
    expect(parsePages('')).toBeUndefined()
    expect(parsePages('  ')).toBeUndefined()
    expect(parsePages('a-b')).toBeUndefined()
    expect(parsePages('9-4')).toBeUndefined()
  })
})
