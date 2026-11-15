import { describe, expect, it } from 'vitest'
import { cellAt, fitBox, panBox, zoomBox } from './viewport'

describe('viewport', () => {
  const full = fitBox(1800, 1200)

  it('zooms around a point, which stays where it is on screen', () => {
    const zoomed = zoomBox(full, 2, { x: 900, y: 600 }, full)

    expect(zoomed).toEqual({ x: 450, y: 300, w: 900, h: 600 })
  })

  it('stops zooming in at a close-up and out a little beyond the board', () => {
    expect(zoomBox(full, 100, { x: 0, y: 0 }, full).w).toBeCloseTo(1800 * 0.15)
    expect(zoomBox(full, 0.01, { x: 0, y: 0 }, full).w).toBeCloseTo(1800 * 1.5)
  })

  it('pans so that the content follows the pointer', () => {
    expect(panBox(full, 100, -50)).toEqual({ x: -100, y: 50, w: 1800, h: 1200 })
  })

  it('finds the square under a point', () => {
    expect(cellAt({ x: 250, y: 99 }, 100)).toEqual({ x: 2, y: 0 })
  })
})
