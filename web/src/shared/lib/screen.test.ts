import { beforeEach, describe, expect, it } from 'vitest'
import { currentScreenToken, isHostDevice, screenHome, storeScreen, type ScreenLink } from './screen'

const tablet: ScreenLink = { role: 'gm', gameId: 'g1', token: 'a' }
const table: ScreenLink = { role: 'table', gameId: 'g1', token: 'b' }

describe('linked screens', () => {
  beforeEach(() => localStorage.clear())

  it('a tablet that becomes the table forgets the GM token', () => {
    storeScreen(tablet)
    expect(currentScreenToken()).toBe('a')
    storeScreen(table)
    expect(currentScreenToken()).toBeUndefined()
  })

  it('only this computer is the host', () => {
    expect(isHostDevice('localhost')).toBe(true)
    expect(isHostDevice('127.0.0.1')).toBe(true)
    expect(isHostDevice('192.168.1.10')).toBe(false)
  })

  it('the GM tablet stays in its game', () => {
    for (const path of ['/', '/workshop/x', '/games/g2', '/games/g1/table'])
      expect(screenHome(path, tablet, false)).toBe('/games/g1')
    for (const path of ['/games/g1', '/manual', '/rules', '/screen/new'])
      expect(screenHome(path, tablet, false)).toBeUndefined()
  })

  it('the table screen never opens the GM view', () => {
    for (const path of ['/', '/games/g1', '/games/g1/table', '/workshop'])
      expect(screenHome(path, table, false)).toBe('/table')
    expect(screenHome('/table', table, false)).toBeUndefined()
  })

  it('the computer goes anywhere', () => {
    expect(screenHome('/', table, true)).toBeUndefined()
  })
})
