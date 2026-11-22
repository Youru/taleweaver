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
  TableCommand,
} from './types'

const games = '/api/games'
const e = encodeURIComponent
const by = (author?: string | null) => (author ? `?author=${e(author)}` : '')

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
    const play = `${games}/${e(id)}/play`
    switch (command.type) {
      case 'nextTurn':
        return http<GameView>(`${base}/next-turn`, { method: 'POST' })
      case 'correct':
        return http<GameView>(`${play}/corrections`, { method: 'POST', body: command })
      case 'ready':
        return http<GameView>(`${play}/ready`, { method: 'POST' })
      case 'several':
        return http<GameView>(`${play}/several`, { method: 'POST', body: command })
      case 'achievement':
        return http<GameView>(`${base}/achievements/${e(command.achievement)}`, {
          method: 'POST',
          body: { adventurer: command.adventurer },
        })
      case 'damage':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/damage`, {
          method: 'POST',
          body: {
            amount: command.amount,
            fromOpponent: command.fromOpponent,
            tokens: command.tokens,
            author: command.author ?? undefined,
          },
        })
      case 'effect':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/effects`, {
          method: 'POST',
          body: { effect: command.effect, active: command.active },
        })
      case 'defeat':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/defeat${by(command.author)}`, { method: 'POST' })
      case 'target':
        return http<GameView>(`${base}/combatants/${e(command.instance)}/target`, { method: 'POST' })
      case 'activateToken':
        return http<GameView>(`${base}/tokens/${e(command.token)}/activation${by(command.author)}`, { method: 'POST' })
      case 'endCondition':
        return http<GameView>(`${base}/end-conditions/${e(command.endCondition)}${by(command.author)}`, { method: 'POST' })
      case 'spend':
        return http<GameView>(`${play}/actions`, {
          method: 'POST',
          body: { figure: command.figure, action: command.action },
        })
      case 'react':
        return http<GameView>(`${play}/reactions`, {
          method: 'POST',
          body: { figure: command.figure, kind: command.kind, against: command.against },
        })
      case 'endReaction':
        return http<GameView>(`${play}/reactions/current`, { method: 'DELETE' })
      case 'noInstruction':
        return http<GameView>(`${play}/no-instruction`, { method: 'POST' })
      case 'card':
        return http<GameView>(`${play}/cards`, {
          method: 'PUT',
          body: { figure: command.figure, card: command.card, mark: command.mark, value: command.value },
        })
      case 'abilityToken':
        return http<GameView>(`${play}/ability-tokens`, {
          method: 'PUT',
          body: { figure: command.figure, token: command.token, value: command.value },
        })
      case 'tether':
        return http<GameView>(`${play}/tethers`, {
          method: 'PUT',
          body: { figure: command.figure, target: command.target },
        })
      case 'flipToken':
        return http<GameView>(`${play}/tokens/${e(command.token)}/flip`, { method: 'POST' })
      case 'reequipped':
        return http<GameView>(`${play}/reequips/${e(command.adventurer)}`, { method: 'POST' })
      case 'carry':
        return http<GameView>(`${play}/carried/${e(command.id)}`, {
          method: 'PUT',
          body: { figure: command.figure, lost: command.lost ?? false },
        })
      case 'counter':
        return http<GameView>(`${play}/counters/${e(command.id)}`, { method: 'POST', body: { by: command.by } })
      case 'deal':
        return http<GameView>(`${play}/initiative/deal`, { method: 'POST' })
      case 'summonEsper':
        return http<GameView>(`${play}/espers`, {
          method: 'POST',
          body: { card: command.card, summoner: command.summoner },
        })
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
  table: (id: string, command: TableCommand) => {
    const base = `${games}/${e(id)}/session`
    switch (command.type) {
      case 'note':
        return http<GameView>(`${base}/notes`, {
          method: 'POST',
          body: { text: command.text, kind: command.kind, page: command.page },
        })
      case 'deleteNote':
        return http<GameView>(`${base}/notes/${e(command.id)}`, { method: 'DELETE' })
      case 'players':
        return http<GameView>(`${base}/players`, { method: 'PUT', body: { players: command.players } })
      case 'pause':
        return http<GameView>(`${base}/pause`, { method: 'PUT', body: { paused: command.paused } })
      case 'revert':
        return http<GameView>(`${games}/${e(id)}/revert`, {
          method: 'POST',
          body: { actions: command.actions, outOf: command.outOf },
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
