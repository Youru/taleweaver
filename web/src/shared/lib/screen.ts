/**
 * The GM's second screen (room-spec §7): a tablet given a token by the QR code of the room panel. The token is
 * sent with every request and acts as the GM's device for that game while the room is open.
 */
const key = 'taleweaver.screen'

export function screenToken(): string | undefined {
  try {
    return localStorage.getItem(key) ?? undefined
  } catch {
    return undefined
  }
}

export function storeScreenToken(token: string) {
  try {
    localStorage.setItem(key, token)
  } catch {
    // Private window: the token lives as long as the page (kept below)
  }
  memory = token
}

let memory: string | undefined

/** The token of this device, if it is a second screen. */
export function currentScreenToken(): string | undefined {
  return memory ?? screenToken()
}
