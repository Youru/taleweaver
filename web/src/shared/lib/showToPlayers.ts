import { inject, type InjectionKey, type Ref } from 'vue'

/**
 * "Show the players" (room-spec §7.5): the game page provides it while its room is open; a card of the game
 * (a combatant, an item) offers a button that pushes its picture to the phones. Null without an open room.
 */
export type ShowToPlayers = (image: string, caption?: string) => Promise<void>

export const showToPlayersKey: InjectionKey<Ref<ShowToPlayers | null>> = Symbol('showToPlayers')

export function useShowToPlayers(): Ref<ShowToPlayers | null> | undefined {
  return inject(showToPlayersKey, undefined)
}
