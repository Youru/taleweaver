import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { gamesApi } from './gamesApi'
import type { PreparationCommand } from './preparation'
import type { Answer, EncounterCommand, GameOptions, GameView, ItemCommand, PartyCommand, TableCommand } from './types'

export const gameKeys = {
  all: ['games'] as const,
  list: () => [...gameKeys.all, 'list'] as const,
  detail: (id: string) => [...gameKeys.all, 'detail', id] as const,
  hiddenCards: (id: string) => [...gameKeys.all, 'hiddenCards', id] as const,
}

export function useGames() {
  return useQuery({ queryKey: gameKeys.list(), queryFn: ({ signal }) => gamesApi.list(signal) })
}

/** refreshEvery (ms): reload regularly, for a view that follows a game played from another screen. */
export function useGame(id: MaybeRefOrGetter<string>, options: { refreshEvery?: number } = {}) {
  return useQuery({
    queryKey: computed(() => gameKeys.detail(toValue(id))),
    queryFn: ({ signal }) => gamesApi.get(toValue(id), signal),
    refetchInterval: options.refreshEvery ?? false,
  })
}

export function useStartGame() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ scenario, party, options }: { scenario: string; party: string[]; options?: GameOptions }) =>
      gamesApi.start(scenario, party, options),
    onSuccess: (game) => {
      client.setQueryData(gameKeys.detail(game.id), game)
      void client.invalidateQueries({ queryKey: gameKeys.list() })
    },
  })
}

/** After an Ending: a new game, the one that ended stays as it is. */
export function useRestartGame(id: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (from: 'beginning' | 'lastEncounter') => gamesApi.restart(toValue(id), from),
    onSuccess: (game) => {
      client.setQueryData(gameKeys.detail(game.id), game)
      void client.invalidateQueries({ queryKey: gameKeys.list() })
    },
  })
}

/** The Hidden Cards revealed; `count`: how many the game has, so that a new one is fetched. */
export function useHiddenCards(id: MaybeRefOrGetter<string>, count: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => [...gameKeys.hiddenCards(toValue(id)), toValue(count)]),
    queryFn: ({ signal }) => gamesApi.hiddenCards(toValue(id), signal),
    enabled: computed(() => toValue(count) > 0),
  })
}

/** The Command cards, fetched when the GM brings one by hand. */
export function useAllyCards(wanted: MaybeRefOrGetter<boolean>) {
  return useQuery({
    queryKey: ['catalog', 'allies'] as const,
    queryFn: ({ signal }) => gamesApi.allyCards(signal),
    enabled: computed(() => toValue(wanted)),
    staleTime: Infinity,
  })
}

export function useDeleteGame() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => gamesApi.remove(id),
    onSuccess: () => client.invalidateQueries({ queryKey: gameKeys.list() }),
  })
}

export type GameCommand =
  | { kind: 'answer'; answer: Answer }
  | { kind: 'undo' }
  | { kind: 'table'; command: TableCommand }
  | { kind: 'encounter'; command: EncounterCommand }
  | { kind: 'items'; command: ItemCommand }
  | { kind: 'party'; command: PartyCommand }
  | { kind: 'preparation'; command: PreparationCommand }

/** Every game command returns the full new state: it replaces the cached game. */
export function useGameCommand(id: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useMutation({
    // One at a time, in their order: the duel sends the SP, then the Damage
    scope: { id: 'game-command' },
    mutationFn: (command: GameCommand): Promise<GameView> => {
      const gameId = toValue(id)
      switch (command.kind) {
        case 'answer':
          return gamesApi.answer(gameId, command.answer)
        case 'undo':
          return gamesApi.undo(gameId)
        case 'table':
          return gamesApi.table(gameId, command.command)
        case 'encounter':
          return gamesApi.encounter(gameId, command.command)
        case 'items':
          return gamesApi.items(gameId, command.command)
        case 'party':
          return gamesApi.party(gameId, command.command)
        case 'preparation':
          return gamesApi.preparation(gameId, command.command)
      }
    },
    onSuccess: (game) => {
      client.setQueryData(gameKeys.detail(game.id), game)
      void client.invalidateQueries({ queryKey: gameKeys.list() })
    },
  })
}
