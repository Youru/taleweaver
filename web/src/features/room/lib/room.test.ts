import { beforeEach, describe, expect, it } from 'vitest'
import { joinUrls, nextAdventurer, normalizeCode, xpLeft } from './room'
import { forgetSeat, storeSeat, storedSeat } from './seatStorage'

describe('room', () => {
  it('builds a join link per LAN address on the page port', () => {
    expect(joinUrls('KRVA', ['192.168.1.10'], { protocol: 'http:', hostname: 'localhost', port: '5173' })).toEqual([
      'http://192.168.1.10:5173/join/KRVA',
    ])
    expect(joinUrls('KRVA', [], { protocol: 'http:', hostname: 'localhost', port: '' })).toEqual([
      'http://localhost/join/KRVA',
    ])
  })

  it('keeps 4 letters of a typed code', () => {
    expect(normalizeCode(' kr-va9z ')).toBe('KRVA')
  })

  it('counts the XP left and wraps the swipe', () => {
    expect(xpLeft({ xp: 10, disciplines: [{ xp: 3 }, { xp: 5 }] })).toBe(2)
    expect(nextAdventurer(['Rook', 'Iris'], 'Iris', 1)).toBe('Rook')
    expect(nextAdventurer(['Rook', 'Iris'], 'Rook', -1)).toBe('Iris')
    expect(nextAdventurer([], undefined, 1)).toBeUndefined()
  })
})

describe('seatStorage', () => {
  beforeEach(() => localStorage.clear())

  it('remembers a seat per room code, in any case', () => {
    storeSeat({ code: 'krva', seatId: 's-1', token: 't' })
    expect(storedSeat('KRVA')).toEqual({ code: 'KRVA', seatId: 's-1', token: 't' })
    forgetSeat('Krva')
    expect(storedSeat('KRVA')).toBeUndefined()
  })
})
