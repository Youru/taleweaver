import { http } from '@/shared/api/http'
import type { Autonomy, JoinResponse, PlayerCommand, PlayerView, RoomSummary, RoomView } from './types'

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
  accept: (gameId: string, request: string) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/requests/${e(request)}/accept`, { method: 'POST' }),
  refuse: (gameId: string, request: string, reason?: string) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/requests/${e(request)}/refuse`, { method: 'POST', body: { reason } }),
  acceptAll: (gameId: string, seat?: string) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/requests/accept-all`, { method: 'POST', body: { seat } }),
  trust: (gameId: string, seatId: string, types: string[]) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/seats/${e(seatId)}/trust`, { method: 'PUT', body: { types } }),
  settings: (gameId: string, autonomy: Autonomy) =>
    http<RoomView>(`/api/games/${e(gameId)}/room/settings`, { method: 'PUT', body: { autonomy } }),
  show: (gameId: string, image: string, caption?: string, seat?: string) =>
    http<void>(`/api/games/${e(gameId)}/room/show`, { method: 'POST', body: { image, caption, seat } }),
  createScreen: (gameId: string) => http<{ token: string }>(`/api/games/${e(gameId)}/room/screen`, { method: 'POST' }),
  revokeScreen: (gameId: string) => http<RoomView>(`/api/games/${e(gameId)}/room/screen`, { method: 'DELETE' }),
  impersonate: (gameId: string, adventurer: string, signal?: AbortSignal) =>
    http<PlayerView>(`/api/games/${e(gameId)}/room/impersonate/${e(adventurer)}`, { signal }),
}

/** A phone's routes: the seat token in the Authorization header. */
export const roomPlayerApi = {
  find: (code: string) => http<RoomSummary>(`/api/rooms/${e(code)}`),
  join: (code: string, displayName: string, color: string) =>
    http<JoinResponse>(`/api/rooms/${e(code)}/seats`, { method: 'POST', body: { displayName, color } }),
  me: (code: string, token: string) => http<PlayerView>(`/api/rooms/${e(code)}/me`, { headers: seat(token) }),
  request: (code: string, token: string, command: PlayerCommand) =>
    http<PlayerView>(`/api/rooms/${e(code)}/me/requests`, { method: 'POST', body: command, headers: seat(token) }),
  cancel: (code: string, token: string, request: string) =>
    http<PlayerView>(`/api/rooms/${e(code)}/me/requests/${e(request)}`, { method: 'DELETE', headers: seat(token) }),
  update: (code: string, token: string, update: { displayName?: string; color?: string; followed?: string }) =>
    http<PlayerView>(`/api/rooms/${e(code)}/me`, { method: 'PUT', body: update, headers: seat(token) }),
}
