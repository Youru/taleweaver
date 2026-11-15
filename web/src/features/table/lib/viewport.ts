/** The part of the board shown, in SVG units (the SVG viewBox). */
export interface ViewBox {
  x: number
  y: number
  w: number
  h: number
}

/** Visible width, as a share of the whole board's: from a close-up to a little more than the board. */
export const CLOSEST = 0.15
export const FARTHEST = 1.5

/** The whole board, with a small margin. */
export function fitBox(width: number, height: number, margin = 0): ViewBox {
  return { x: -margin, y: -margin, w: width + 2 * margin, h: height + 2 * margin }
}

/**
 * Zooms by `factor` (> 1 zooms in) keeping `center` (SVG units) where it is on screen.
 * The visible width stays between CLOSEST and FARTHEST times the whole board's.
 */
export function zoomBox(box: ViewBox, factor: number, center: { x: number; y: number }, full: ViewBox): ViewBox {
  const width = Math.min(Math.max(box.w / factor, full.w * CLOSEST), full.w * FARTHEST)
  const scale = width / box.w
  return {
    x: center.x - (center.x - box.x) * scale,
    y: center.y - (center.y - box.y) * scale,
    w: width,
    h: box.h * scale,
  }
}

/** Moves the view by (dx, dy) SVG units, the content following the pointer. */
export function panBox(box: ViewBox, dx: number, dy: number): ViewBox {
  return { ...box, x: box.x - dx, y: box.y - dy }
}

/** The square under a point in SVG units, `size` units per square. */
export function cellAt(point: { x: number; y: number }, size: number): { x: number; y: number } {
  return { x: Math.floor(point.x / size), y: Math.floor(point.y / size) }
}
