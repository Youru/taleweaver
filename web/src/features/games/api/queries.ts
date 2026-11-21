import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { gamesApi } from './gamesApi'
import type { Answer, EncounterCommand, GameView, ItemCommand, PartyCommand } from './types'

export const gameKeys = {
  all: ['games'] as const,
  list: () => [...gameKeys.all, 'list'] as const,
  detail: (id: string) => [...gameKeys.all, 'detail', id] as const,
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
    mutationFn: ({ scenario, party }: { scenario: string; party: string[] }) => gamesApi.start(scenario, party),
    onSuccess: (game) => {
      client.setQueryData(gameKeys.detail(game.id), game)
      void client.invalidateQueries({ queryKey: gameKeys.list() })
    },
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
  | { kind: 'encounter'; command: EncounterCommand }
  | { kind: 'items'; command: ItemCommand }
  | { kind: 'party'; command: PartyCommand }

/** Every game command returns the full new state: it replaces the cached game. */
export function useGameCommand(id: MaybeRefOrGetter<string>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (command: GameCommand): Promise<GameView> => {
      const gameId = toValue(id)
      switch (command.kind) {
        case 'answer':
          return gamesApi.answer(gameId, command.answer)
        case 'undo':
          return gamesApi.undo(gameId)
        case 'encounter':
          return gamesApi.encounter(gameId, command.command)
        case 'items':
          return gamesApi.items(gameId, command.command)
        case 'party':
          return gamesApi.party(gameId, command.command)
      }
    },
    onSuccess: (game) => {
      client.setQueryData(gameKeys.detail(game.id), game)
      void client.invalidateQueries({ queryKey: gameKeys.list() })
    },
  })
}
