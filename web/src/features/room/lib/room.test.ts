import { beforeEach, describe, expect, it } from 'vitest'
import { asGameCommand, describeRequest, joinUrls, nextAdventurer, normalizeCode, turnOf, xpLeft } from './room'
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

describe('turnOf', () => {
  const encounter = {
    round: 2,
    ended: false,
    initiativeTrack: ['um1:initiative/fragor', 'adventurer:Rook', 'adventurer:Iris'],
    currentTurn: 0,
    playedCards: ['um1:initiative/fragor'],
  }

  it('warns the seat whose adventurer plays next', () => {
    expect(turnOf(encounter, ['Rook'])).toEqual({ next: 'Rook', after: 'um1:initiative/fragor' })
  })

  it('tells the seat its adventurer is playing', () => {
    const playing = { ...encounter, currentTurn: 1, playedCards: [...encounter.playedCards, 'adventurer:Rook'] }
    expect(turnOf(playing, ['Rook', 'Iris'])).toEqual({ playing: 'Rook', next: 'Iris', after: 'adventurer:Rook' })
  })

  it('says nothing between rounds nor to another seat', () => {
    expect(turnOf({ ...encounter, round: 0 }, ['Rook'])).toEqual({})
    expect(turnOf(encounter, ['Iris'])).toEqual({})
  })
})

describe('describeRequest', () => {
  const names = (id: string) => (id === 'item-3' ? 'Cave Sickle' : id)

  it('tells equipping, packing and handing to the stash apart', () => {
    expect(describeRequest({ type: 'moveItem', item: 'item-3', owner: 'Rook', slot: 'equipped' }, names)).toEqual({
      key: 'room.request.equip',
      args: { item: 'Cave Sickle', owner: 'Rook' },
    })
    expect(describeRequest({ type: 'moveItem', item: 'item-3', owner: 'Iris', slot: 'pack' }, names).key).toBe(
      'room.request.toPack',
    )
    expect(describeRequest({ type: 'moveItem', item: 'item-3', owner: 'stash' }, names).key).toBe(
      'room.request.toStash',
    )
  })

  it('becomes the same command when the GM plays the adventurer', () => {
    expect(asGameCommand({ type: 'useItem', item: 'item-3', discard: true })).toEqual({
      kind: 'items',
      command: { type: 'use', id: 'item-3', discard: true },
    })
  })
})
