/** A phone's seat in a room, kept on the phone so that opening the link again reconnects it (room-spec §4.2). */
export interface StoredSeat {
  code: string
  seatId: string
  token: string
}

const seatsKey = 'taleweaver.room.seats'
const profileKey = 'taleweaver.room.profile'

function read<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : fallback
  } catch {
    return fallback
  }
}

function write(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // private window, storage blocked: the player joins again next time
  }
}

export function storedSeat(code: string): StoredSeat | undefined {
  return read<StoredSeat[]>(seatsKey, []).find((s) => s.code.toUpperCase() === code.toUpperCase())
}

/** Remembers the seat of this room (one per code, the last ten rooms). */
export function storeSeat(seat: StoredSeat) {
  const others = read<StoredSeat[]>(seatsKey, []).filter((s) => s.code.toUpperCase() !== seat.code.toUpperCase())
  write(seatsKey, [{ ...seat, code: seat.code.toUpperCase() }, ...others].slice(0, 10))
}

export function forgetSeat(code: string) {
  write(
    seatsKey,
    read<StoredSeat[]>(seatsKey, []).filter((s) => s.code.toUpperCase() !== code.toUpperCase()),
  )
}

/** The name and colour last used, to prefill the join form. */
export function storedProfile(): { displayName: string; color: string } | undefined {
  return read<{ displayName: string; color: string } | undefined>(profileKey, undefined)
}

export function storeProfile(profile: { displayName: string; color: string }) {
  write(profileKey, profile)
}
