/**
 * A device linked by QR code (room-spec §7.6): the GM's tablet, whose token goes with every request and runs that
 * game, or a screen that shows the table, whose token only reads the table's view. A device is one or the other.
 */
export type ScreenLink = { role: 'gm' | 'table'; gameId: string; token: string }

const key = 'taleweaver.device'
let memory: ScreenLink | undefined

export function storedScreen(): ScreenLink | undefined {
  try {
    const link = JSON.parse(localStorage.getItem(key) ?? 'null') as ScreenLink | null
    if (link?.token && link.gameId && (link.role === 'gm' || link.role === 'table')) return link
  } catch {
    // unreadable, or no storage: the link of this page, if any
  }
  return memory
}

export function storeScreen(link: ScreenLink) {
  memory = link
  try {
    localStorage.setItem(key, JSON.stringify(link))
  } catch {
    // Private window: the link lives as long as the page
  }
}

/** The GM token of this device, if it is the GM's tablet: never a table's. */
export function currentScreenToken(): string | undefined {
  const link = storedScreen()
  return link?.role === 'gm' ? link.token : undefined
}

/** The computer that hosts the app, the GM by itself: it alone manages the game, the room and the QR codes. */
export function isHostDevice(hostname: string = window.location.hostname): boolean {
  return hostname === 'localhost' || hostname === '[::1]' || hostname.startsWith('127.')
}

/** Where a linked device goes instead of a page that is not for it; undefined: the page is fine. */
export function screenHome(path: string, link: ScreenLink | undefined, host: boolean): string | undefined {
  if (host || !link) return undefined
  const game = `/games/${link.gameId}`
  const hostOnly = path === '/' || path.startsWith('/workshop')
  if (link.role === 'table') return hostOnly || path.startsWith('/games/') ? '/table' : undefined
  return hostOnly || (path.startsWith('/games/') && path !== game) ? game : undefined
}
