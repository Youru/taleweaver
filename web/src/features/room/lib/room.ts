/** The 6 seat colours (same list as the API), with their CSS colour. */
export const seatColors: Record<string, string> = {
  teal: '#14b8a6',
  amber: '#f59e0b',
  rose: '#f43f5e',
  violet: '#8b5cf6',
  lime: '#84cc16',
  sky: '#0ea5e9',
}

/** No 0/O/1/I/L in a room code. */
export function normalizeCode(text: string): string {
  return text
    .toUpperCase()
    .replace(/[^A-Z]/g, '')
    .slice(0, 4)
}

/**
 * The links a phone can open: one per LAN address of the GM's machine, on the port the GM's page is served
 * from (the Vite server, which forwards to the API). Localhost when the machine has no LAN address.
 */
export function joinUrls(
  code: string,
  lanAddresses: string[],
  location: { protocol: string; hostname: string; port: string },
): string[] {
  const port = location.port ? `:${location.port}` : ''
  const hosts = lanAddresses.length > 0 ? lanAddresses : [location.hostname]
  return hosts.map((host) => `${location.protocol}//${host}${port}/join/${code}`)
}

/** The XP not spent on disciplines. */
export function xpLeft(adventurer: { xp: number; disciplines?: { xp: number }[] }): number {
  return adventurer.xp - (adventurer.disciplines ?? []).reduce((sum, d) => sum + d.xp, 0)
}

/** The adventurer shown after a swipe (wraps around). */
export function nextAdventurer(names: string[], current: string | undefined, step: 1 | -1): string | undefined {
  if (names.length === 0) return undefined
  const index = current ? names.indexOf(current) : -1
  return names[(index + step + names.length) % names.length]
}

const ADVENTURER = 'adventurer:'

/**
 * Where the seat's adventurers stand on the track (room-spec §5.2): the one playing now, or the one playing
 * right after the card in play ("get ready"). Cards are "adventurer:<name>" or catalogue ids.
 */
export function turnOf(
  encounter: { round: number; ended: boolean; initiativeTrack: string[]; currentTurn: number; playedCards: string[] },
  mine: string[],
): { playing?: string; next?: string; after?: string } {
  if (encounter.ended || encounter.round === 0) return {}
  const owner = (card: string | undefined) =>
    card?.startsWith(ADVENTURER) && mine.includes(card.slice(ADVENTURER.length))
      ? card.slice(ADVENTURER.length)
      : undefined
  const current = encounter.initiativeTrack[encounter.currentTurn]
  const played = new Set(encounter.playedCards)
  const next = encounter.initiativeTrack.find((card) => !played.has(card) && card !== current)
  const playing = owner(current)
  const coming = owner(next)
  return {
    ...(playing ? { playing } : {}),
    ...(coming && current ? { next: coming, after: current } : {}),
  }
}
