import type { EncounterCommand, EncounterState } from '../api/types'

/** The actions and abilities every adventurer has, with their SP (rulebook p. 38). */
export const actionCosts = [
  ['move', 1],
  ['extraMove', 1],
  ['attack', 2],
  ['encounterAction', 1],
  ['reequip', 1],
  ['dodge', 1],
  ['empower', 1],
] as const

export const reactionKinds = ['counter', 'counterDodged', 'break', 'redirect'] as const

/** "adventurer:Rook#2", a second Initiative Card, is still Rook's. */
export const adventurerOf = (id: string | undefined) =>
  id?.startsWith('adventurer:') ? id.slice('adventurer:'.length).split('#')[0] : undefined

/** "adventurer:Rook" → "Rook", "ally:wisp" → "wisp", a combatant as it is. */
export const figureName = (id: string | undefined | null) =>
  !id ? '' : (adventurerOf(id) ?? (id.startsWith('ally:') ? id.slice('ally:'.length) : id))

/** Every figure in play, by id. */
export function figuresInPlay(encounter: EncounterState | undefined): string[] {
  if (!encounter) return []
  return [
    ...encounter.adventurers.filter((a) => !a.defeated).map((a) => `adventurer:${a.name}`),
    ...(encounter.allies ?? []).filter((a) => !a.defeated).map((a) => `ally:${a.id}`),
    ...encounter.combatants.filter((c) => !c.defeated).map((c) => c.instance),
  ]
}

/** Who plays now on the party's side: they spend the SP, and are proposed as who did it. */
export function playingNow(encounter: EncounterState | undefined): string[] {
  if (!encounter || !encounter.round || encounter.currentTurn < 0 || encounter.activation) return []
  const card = encounter.initiativeTrack[encounter.currentTurn]
  const name = adventurerOf(card)
  if (name) return [`adventurer:${name}`]
  return (encounter.allies ?? []).filter((a) => !a.defeated && a.trackCard === card).map((a) => `ally:${a.id}`)
}

/**
 * Who did it, put on what asks for it (an opponent hurt or Defeated, a token, an End Condition) unless already said.
 * null: nobody known, the engine asks if the scenario needs it.
 */
export function withAuthor(command: EncounterCommand, author: string | null): EncounterCommand {
  switch (command.type) {
    case 'damage':
      if (command.amount <= 0 || command.instance.startsWith('adventurer:') || command.instance.startsWith('ally:'))
        return command
      break
    case 'defeat':
      if (command.instance.startsWith('adventurer:') || command.instance.startsWith('ally:')) return command
      break
    case 'activateToken':
    case 'endCondition':
      break
    default:
      return command
  }
  return command.author !== undefined || !author ? command : { ...command, author }
}
