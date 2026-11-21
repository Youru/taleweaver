import { http } from '@/shared/api/http'
import type { JoinResponse, PlayerView, RoomSummary, RoomView } from './types'

const e = encodeURIComponent
const seat = (token: string) => ({ Authorization: `Seat ${token}` })

/** The GM's routes (this machine only). */
export const roomGmApi = {
  get: (gameId: string, signal?: AbortSignal) => http<RoomView>(`/api/games/${e(gameId)}/room`, { signal }),
  open: (gameId: string) => http<RoomView>(`/api/games/${e(gameId)}/room`, { method: 'POST' }),
  close: (gameId: string) => http<RoomView>(`/api/games/${e(gameId)}/room`, { method: 'DELETE' }),
  assign: (gameId: string, seatId: string, adventurers: string[]) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/seats/${e(seatId)}/adventurers`, {
      method: 'PUT',
      body: { adventurers },
    }),
  removeSeat: (gameId: string, seatId: string) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/seats/${e(seatId)}`, { method: 'DELETE' }),
  impersonate: (gameId: string, adventurer: string, signal?: AbortSignal) =>
    http<PlayerView>(`/api/games/${e(gameId)}/room/impersonate/${e(adventurer)}`, { signal }),
}

/** A phone's routes: the seat token in the Authorization header. */
export const roomPlayerApi = {
  find: (code: string) => http<RoomSummary>(`/api/rooms/${e(code)}`),
  join: (code: string, displayName: string, color: string) =>
    http<JoinResponse>(`/api/rooms/${e(code)}/seats`, { method: 'POST', body: { displayName, color } }),
  me: (code: string, token: string) => http<PlayerView>(`/api/rooms/${e(code)}/me`, { headers: seat(token) }),
  update: (code: string, token: string, update: { displayName?: string; color?: string; followed?: string }) =>
    http<PlayerView>(`/api/rooms/${e(code)}/me`, { method: 'PUT', body: update, headers: seat(token) }),
}
