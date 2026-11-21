import { http } from '@/shared/api/http'
import type { Answer, EncounterCommand, GameSummary, GameView, ItemCommand } from './types'

const games = '/api/games'
const e = encodeURIComponent

export const gamesApi = {
  list: (signal?: AbortSignal) => http<GameSummary[]>(games, { signal }),
  get: (id: string, signal?: AbortSignal) => http<GameView>(`${games}/${e(id)}`, { signal }),
  start: (scenario: string, party: string[]) => http<GameView>(games, { method: 'POST', body: { scenario, party } }),
  remove: (id: string) => http<void>(`${games}/${e(id)}`, { method: 'DELETE' }),
  answer: (id: string, answer: Answer) => http<GameView>(`${games}/${e(id)}/answer`, { method: 'POST', body: answer }),
  undo: (id: string) => http<GameView>(`${games}/${e(id)}/undo`, { method: 'POST' }),
  encounter: (id: string, command: EncounterCommand) => {
    const base = `${games}/${e(id)}/encounter`
    switch (command.type) {
      case 'nextTurn':
        return http<GameView>(`${base}/next-turn`, { method: 'POST' })
      case 'damage':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/damage`, {
          method: 'POST',
          body: { amount: command.amount },
        })
      case 'effect':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/effects`, {
          method: 'POST',
          body: { effect: command.effect, active: command.active },
        })
      case 'defeat':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/defeat`, { method: 'POST' })
      case 'activateToken':
        return http<GameView>(`${base}/tokens/${e(command.token)}/activation`, { method: 'POST' })
      case 'endCondition':
        return http<GameView>(`${base}/end-conditions/${e(command.endCondition)}`, { method: 'POST' })
      case 'move':
        return http<GameView>(`${base}/figures/${e(command.figure)}/position`, { method: 'PUT', body: command.to })
      case 'revealArea':
        return http<GameView>(`${base}/areas/${e(command.area)}/reveal`, { method: 'POST' })
      case 'moveInitiative':
        return http<GameView>(`${base}/initiative/moves`, {
          method: 'POST',
          body: { card: command.card, by: command.by },
        })
    }
  },
  items: (id: string, command: ItemCommand) => {
    const base = `${games}/${e(id)}/items`
    switch (command.type) {
      case 'add':
        return http<GameView>(base, {
          method: 'POST',
          body: {
            item: command.item,
            owner: command.owner,
            slot: command.slot,
            price: command.price,
            replaces: command.replaces,
          },
        })
      case 'move':
        return http<GameView>(`${base}/${e(command.id)}/owner`, {
          method: 'PUT',
          body: { owner: command.owner, slot: command.slot },
        })
      case 'discard':
        return http<GameView>(`${base}/${e(command.id)}/discard`, { method: 'POST', body: { gold: command.gold } })
      case 'flip':
        return http<GameView>(`${base}/${e(command.id)}/flipped`, { method: 'PUT', body: { flipped: command.flipped } })
      case 'use':
        return http<GameView>(`${base}/${e(command.id)}/use`, {
          method: 'POST',
          body: { discard: command.discard, token: command.token, at: command.at },
        })
    }
  },
}
