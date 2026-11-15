import { describe, expect, it } from 'vitest'
import type { DraftNode } from '../api/types'
import {
  addArea,
  addCombatant,
  addTile,
  addToken,
  addZone,
  extentOf,
  mapOf,
  moveItem,
  overlappingTiles,
  removeArea,
  removeItem,
  rotateTile,
  setTileArea,
  setupOf,
  spanOf,
  tokenKey,
  turnToken,
  withBoard,
} from './map'

const encounter = (): DraftNode => ({ id: 'fight', type: 'encounter', map: { northArrow: 0, tiles: [] } })

describe('map editor', () => {
  it('lays tiles, turns them a quarter at a time and sees overlaps', () => {
    let map = addTile(mapOf(encounter()), 'um1:tile/um1', { x: 0, y: 0 })
    map = addTile(map, 'um1:tile/um2', { x: 6, y: 0 })
    expect(overlappingTiles(map).size).toBe(0)
    map = rotateTile(rotateTile(map, 1), 1, -270)
    expect(map.tiles[1]?.rotation).toBe(180)
    map = addTile(map, 'um1:tile/um3', { x: 3, y: 2 })
    expect([...overlappingTiles(map)].sort()).toEqual([0, 1, 2])
    // The fields the editor does not know stay
    expect(map.northArrow).toBe(0)
  })

  it('gives tokens ids of their own and the key the rules use', () => {
    const map = mapOf(encounter())
    const first = addToken(map, { type: 'um1:token/objective', color: 'blue' }, { x: 2, y: 3 })
    const second = addToken(first.map, { type: 'um1:token/objective', color: 'blue' }, { x: 4, y: 3 })
    expect([first.id, second.id]).toEqual(['objective-blue-1', 'objective-blue-2'])
    expect(tokenKey('um1:token/objective', 'blue')).toBe('objective:blue')
    expect(tokenKey('um1:token/terrain/obstructing')).toBe('obstructing')

    const long = addToken(map, { type: 'um1:token/terrain/obstructing-long', size: '2x1' }, { x: 0, y: 0 })
    expect(long.map.tokens[0]).toMatchObject({ id: 'terrain-obstructing-long-1', w: 2, h: 1 })
    expect(turnToken(long.map, long.id).tokens[0]).toMatchObject({ w: 1, h: 2 })
  })

  it('moves a token into the fog area of the tile it lands on', () => {
    let map = addTile(mapOf(encounter()), 'um1:tile/um1', { x: 6, y: 0 })
    const room = addArea(map, { fr: 'Crypte' })
    map = setTileArea(room.map, 0, room.id)
    const token = addToken(map, { type: 'um1:token/totem', color: 'red' }, { x: 0, y: 0 })
    const board = moveItem(
      { map: token.map, setup: setupOf(encounter()) },
      { kind: 'token', id: token.id },
      { x: 7, y: 2 },
    )
    expect(board.map.tokens[0]).toMatchObject({ x: 7, y: 2, area: 'area-1' })
    // Removing the area shows its pieces again
    const cleared = removeArea(board.map, 'area-1')
    expect(cleared.tiles[0]).not.toHaveProperty('area')
    expect(cleared.tokens[0]).not.toHaveProperty('area')
  })

  it('draws zones either way and forgets the start when its zone goes', () => {
    expect(spanOf({ x: 5, y: 4 }, { x: 3, y: 1 })).toEqual({ x: 3, y: 1, w: 3, h: 4 })
    const zone = addZone(mapOf(encounter()), 'startingZone', { x: 1, y: 1, w: 1, h: 4 })
    expect(zone.id).toBe('start')
    const board = { map: zone.map, setup: { ...setupOf(encounter()), adventurersStart: 'start' } }
    const after = removeItem(board, { kind: 'zone', id: 'start' })
    expect(after.setup).not.toHaveProperty('adventurersStart')
    expect(addZone(zone.map, 'startingZone', { x: 0, y: 0, w: 1, h: 1 }).id).toBe('start-1')
  })

  it('places setup combatants and sizes the grid on everything laid', () => {
    const node = withBoard(encounter(), (b) => ({
      map: addTile(b.map, 'um1:tile/um1', { x: 0, y: 0 }),
      setup: addCombatant(b.setup, 'fragor', { x: 9, y: 2 }),
    }))
    expect(node.setup).toEqual({ combatants: [{ family: 'fragor', at: { x: 9, y: 2 } }] })
    expect(extentOf({ map: mapOf(node), setup: setupOf(node) })).toEqual({ width: 10, height: 6 })
    const moved = moveItem({ map: mapOf(node), setup: setupOf(node) }, { kind: 'combatant', index: 0 }, { x: 1, y: 1 })
    expect(moved.setup.combatants[0]?.at).toEqual({ x: 1, y: 1 })
  })
})
