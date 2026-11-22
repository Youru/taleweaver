import { http } from '@/shared/api/http'
import type { AllyCard } from './allies'
import type { PreparationCommand } from './preparation'
import type {
  Answer,
  EncounterCommand,
  GameOptions,
  GameSummary,
  GameView,
  HiddenCard,
  ItemCommand,
  PartyCommand,
} from './types'

const games = '/api/games'
const e = encodeURIComponent

export const gamesApi = {
  list: (signal?: AbortSignal) => http<GameSummary[]>(games, { signal }),
  get: (id: string, signal?: AbortSignal) => http<GameView>(`${games}/${e(id)}`, { signal }),
  start: (scenario: string, party: string[], options?: GameOptions) =>
    http<GameView>(games, { method: 'POST', body: { scenario, party, options } }),
  restart: (id: string, from: 'beginning' | 'lastEncounter') =>
    http<GameView>(`${games}/${e(id)}/restart`, { method: 'POST', body: { from } }),
  hiddenCards: (id: string, signal?: AbortSignal) => http<HiddenCard[]>(`${games}/${e(id)}/hidden-cards`, { signal }),
  actExportUrl: (id: string) => `${games}/${e(id)}/act-export`,
  remove: (id: string) => http<void>(`${games}/${e(id)}`, { method: 'DELETE' }),
  answer: (id: string, answer: Answer) => http<GameView>(`${games}/${e(id)}/answer`, { method: 'POST', body: answer }),
  undo: (id: string) => http<GameView>(`${games}/${e(id)}/undo`, { method: 'POST' }),
  encounter: (id: string, command: EncounterCommand) => {
    const base = `${games}/${e(id)}/encounter`
    switch (command.type) {
      case 'nextTurn':
        return http<GameView>(`${base}/next-turn`, { method: 'POST' })
      case 'achievement':
        return http<GameView>(`${base}/achievements/${e(command.achievement)}`, {
          method: 'POST',
          body: { adventurer: command.adventurer },
        })
      case 'damage':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/damage`, {
          method: 'POST',
          body: { amount: command.amount, fromOpponent: command.fromOpponent, tokens: command.tokens },
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
      case 'summon':
        return http<GameView>(`${games}/${e(id)}/allies/summons`, {
          method: 'POST',
          body: { adventurer: command.adventurer, card: command.card },
        })
      case 'addAlly':
        return http<GameView>(`${games}/${e(id)}/allies`, {
          method: 'POST',
          body: { card: command.card, owner: command.owner },
        })
      case 'dismissAlly':
        return http<GameView>(`${games}/${e(id)}/allies/${e(command.ally)}/dismissal`, {
          method: 'POST',
          body: { ignoreDamage: command.ignoreDamage ?? false },
        })
      case 'earnConduit':
        return http<GameView>(`${games}/${e(id)}/allies/conduits/${e(command.adventurer)}`, {
          method: 'POST',
          body: { card: command.card },
        })
      case 'conduitForm':
        return http<GameView>(`${games}/${e(id)}/allies/conduits/${e(command.adventurer)}/form`, {
          method: 'PUT',
          body: { card: command.card },
        })
      case 'stamina':
        return http<GameView>(`${games}/${e(id)}/party/${e(command.adventurer)}/stamina`, {
          method: 'PUT',
          body: { value: command.value },
        })
    }
  },
  allyCards: (signal?: AbortSignal) => http<AllyCard[]>('/api/catalog/allies', { signal }),
  preparation: (id: string, command: PreparationCommand) => {
    const base = `${games}/${e(id)}/preparation`
    switch (command.type) {
      case 'choose': {
        const { adventurer, kit, items, disciplines } = command
        return http<GameView>(`${base}/adventurers`, { method: 'PUT', body: { adventurer, kit, items, disciplines } })
      }
      case 'remove':
        return http<GameView>(`${base}/adventurers/${e(command.adventurer)}`, { method: 'DELETE' })
      case 'hidden':
        return http<GameView>(`${base}/hidden`, { method: 'PUT', body: { shown: command.shown } })
      case 'setOut':
        return http<GameView>(`${base}/departure`, { method: 'POST' })
    }
  },
  party: (id: string, command: PartyCommand) => {
    const base = `${games}/${e(id)}/party`
    switch (command.type) {
      case 'activeGroup':
        return http<GameView>(`${base}/active-group`, { method: 'PUT', body: { adventurers: command.adventurers } })
      case 'status':
        return http<GameView>(`${base}/${e(command.adventurer)}/${command.status}`, {
          method: 'PUT',
          body: { value: command.value },
        })
      case 'learn':
        return http<GameView>(`${base}/${e(command.adventurer)}/disciplines`, {
          method: 'POST',
          body: { discipline: command.discipline, xp: command.xp },
        })
      case 'forget':
        return http<GameView>(`${base}/${e(command.adventurer)}/disciplines/${e(command.discipline)}`, {
          method: 'DELETE',
        })
      case 'respec':
        return http<GameView>(`${base}/${e(command.adventurer)}/respec`, { method: 'POST' })
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
      case 'unlockUpgrade':
        return http<GameView>(`${base}/upgrades/unlocked`, { method: 'POST', body: { upgrade: command.upgrade } })
      case 'attachUpgrade':
        return http<GameView>(`${base}/${e(command.id)}/upgrades`, {
          method: 'POST',
          body: { upgrade: command.upgrade, price: command.price },
        })
      case 'detachUpgrade':
        return http<GameView>(`${base}/${e(command.id)}/upgrades/${e(command.upgrade)}`, { method: 'DELETE' })
      case 'use':
        return http<GameView>(`${base}/${e(command.id)}/use`, {
          method: 'POST',
          body: { discard: command.discard, token: command.token, at: command.at },
        })
    }
  },
}
