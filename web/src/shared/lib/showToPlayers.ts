import { inject, type InjectionKey, type Ref } from 'vue'

/**
 * "Show the players" (room-spec §7.5): the game page provides it while its room is open; a card of the game
 * (a combatant, an item) offers a button that pushes its picture to every phone, or to one. Null without an open
 * room.
 */
export interface ShowToPlayers {
  players: { id: string; name: string }[]
  /** Without a player: to all of them. */
  show: (image: string, caption?: string, player?: string) => Promise<void>
}

export const showToPlayersKey: InjectionKey<Ref<ShowToPlayers | null>> = Symbol('showToPlayers')

export function useShowToPlayers(): Ref<ShowToPlayers | null> | undefined {
  return inject(showToPlayersKey, undefined)
}
