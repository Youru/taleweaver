import type { GameCommand } from '@/features/games'
import type { PlayerCommand } from '../api/types'

/** The 6 seat colours (same list as the API), with their CSS colour. */
export const seatColors: Record<string, string> = {
  teal: '#14b8a6',
  amber: '#f59e0b',
  rose: '#f43f5e',
  violet: '#8b5cf6',
  lime: '#84cc16',
  sky: '#0ea5e9',
}

/** Letters of a room code (same as the API): 6, without 0/O/1/I/L. */
export const CODE_LENGTH = 6

/** A whole code: 6 letters, or the 4 of a room opened before 2026-11-21 and still open. */
export const isCode = (code: string) => code.length === CODE_LENGTH || code.length === 4

/** No 0/O/1/I/L in a room code. */
export function normalizeCode(text: string): string {
  return text
    .toUpperCase()
    .replace(/[^A-Z]/g, '')
    .slice(0, CODE_LENGTH)
}

/**
 * The links a phone can open: one per LAN address of the GM's machine, on the port the GM's page is served
 * from (the Vite server, which forwards to the API). Localhost when the machine has no LAN address.
 */
export function joinUrls(
  code: string,
  lanAddresses: string[],
  location: { protocol: string; hostname: string; port: string },
): string[] {
  const port = location.port ? `:${location.port}` : ''
  const hosts = lanAddresses.length > 0 ? lanAddresses : [location.hostname]
  return hosts.map((host) => `${location.protocol}//${host}${port}/join/${code}`)
}

/** The XP not spent on disciplines. */
export function xpLeft(adventurer: { xp: number; disciplines?: { xp: number }[] }): number {
  return adventurer.xp - (adventurer.disciplines ?? []).reduce((sum, d) => sum + d.xp, 0)
}

/** The adventurer shown after a swipe (wraps around). */
export function nextAdventurer(names: string[], current: string | undefined, step: 1 | -1): string | undefined {
  if (names.length === 0) return undefined
  const index = current ? names.indexOf(current) : -1
  return names[(index + step + names.length) % names.length]
}

const ADVENTURER = 'adventurer:'

/**
 * Where the seat's adventurers stand on the track (room-spec §5.2): the one playing now, or the one playing
 * right after the card in play ("get ready"). Cards are "adventurer:<name>" or catalogue ids.
 */
export function turnOf(
  encounter: {
    round: number
    ended: boolean
    initiativeTrack: string[]
    currentTurn: number
    playedCards: string[]
    allies?: { trackCard: string; owner?: string | null; defeated: boolean }[]
  },
  mine: string[],
): { playing?: string; next?: string; after?: string } {
  if (encounter.ended || encounter.round === 0) return {}
  // An ally plays by the hand of who brought it
  const owner = (card: string | undefined) => {
    const name = card?.startsWith(ADVENTURER)
      ? card.slice(ADVENTURER.length)
      : encounter.allies?.find((a) => !a.defeated && a.trackCard === card)?.owner
    return name && mine.includes(name) ? name : undefined
  }
  const current = encounter.initiativeTrack[encounter.currentTurn]
  const played = new Set(encounter.playedCards)
  const next = encounter.initiativeTrack.find((card) => !played.has(card) && card !== current)
  const playing = owner(current)
  const coming = owner(next)
  return {
    ...(playing ? { playing } : {}),
    ...(coming && current ? { next: coming, after: current } : {}),
  }
}

const words = (slug: string) => slug.replace(/-/g, ' ').replace(/\b[a-z]/g, (c) => c.toUpperCase())
/** "um1:combatant/zulfiqar-command" → "Zulfiqar". */
const cardName = (id: string) => words((id.split('/').pop() ?? id).replace(/-(command|conduit).*$/, ''))
const allyName = (figure: string) => words(figure.replace(/^ally:/, ''))

/**
 * How a request reads (room-spec §6.2): the i18n key under room.request and its arguments. `itemName` names an
 * item the party holds by its id ("item-3" → "Cave Sickle").
 */
export function describeRequest(
  command: PlayerCommand,
  itemName: (id: string) => string,
  disciplineName: (id: string) => string = (id) => id,
  upgradeName: (id: string) => string = (id) => id.split('/').pop() ?? id,
): { key: string; args: Record<string, string> } {
  switch (command.type) {
    case 'moveItem':
      return {
        key:
          command.owner === 'stash'
            ? 'room.request.toStash'
            : command.slot === 'pack'
              ? 'room.request.toPack'
              : 'room.request.equip',
        args: { item: itemName(command.item), owner: command.owner },
      }
    case 'flipItem':
      return { key: 'room.request.flip', args: { item: itemName(command.item) } }
    case 'useItem':
      return { key: 'room.request.use', args: { item: itemName(command.item) } }
    case 'takeAdventurer':
      return { key: 'room.request.takeAdventurer', args: { adventurer: command.adventurer } }
    case 'buyItem':
      return {
        key: command.owner === 'stash' ? 'room.request.buyForStash' : 'room.request.buy',
        args: { item: itemName(command.item), owner: command.owner, price: String(command.price ?? '?') },
      }
    case 'attachUpgrade':
      return {
        key: 'room.request.upgrade',
        args: {
          item: itemName(command.item),
          upgrade: upgradeName(command.upgrade),
          price: String(command.price ?? '?'),
        },
      }
    case 'damage':
      return {
        key: command.amount >= 0 ? 'room.request.damage' : 'room.request.heal',
        args: { adventurer: command.adventurer, amount: String(Math.abs(command.amount)) },
      }
    case 'effect':
      return {
        key: command.active ? 'room.request.effectOn' : 'room.request.effectOff',
        args: { adventurer: command.adventurer, effect: command.effect },
      }
    case 'chooseAdventurer':
    case 'releaseAdventurer':
      return { key: `room.request.${command.type}`, args: { adventurer: command.adventurer } }
    case 'summon':
    case 'earnConduit':
      return { key: `room.request.${command.type}`, args: { adventurer: command.adventurer, card: cardName(command.card) } }
    case 'conduitForm':
      return command.card
        ? { key: 'room.request.conduitForm', args: { adventurer: command.adventurer, card: cardName(command.card) } }
        : { key: 'room.request.conduitLeft', args: { adventurer: command.adventurer } }
    case 'dismissAlly':
      return { key: 'room.request.dismissAlly', args: { ally: allyName(command.ally) } }
    case 'allyDamage':
      return { key: 'room.request.allyDamage', args: { ally: allyName(command.ally), amount: String(command.amount) } }
    case 'learnDiscipline':
      return {
        key: 'room.request.learnDiscipline',
        args: { discipline: disciplineName(command.discipline), adventurer: command.adventurer },
      }
  }
}

/**
 * The GM playing an adventurer as a player (room-spec §7.4): the same action, applied at once. Null for what is
 * not a game command (asking for an adventurer).
 */
export function asGameCommand(command: PlayerCommand): GameCommand | null {
  switch (command.type) {
    case 'takeAdventurer':
    case 'chooseAdventurer':
    case 'releaseAdventurer':
      return null
    case 'summon':
    case 'earnConduit':
    case 'conduitForm':
    case 'dismissAlly':
      return { kind: 'encounter', command }
    case 'allyDamage':
      return { kind: 'encounter', command: { type: 'damage', instance: command.ally, amount: command.amount } }
    case 'moveItem':
      return { kind: 'items', command: { type: 'move', id: command.item, owner: command.owner, slot: command.slot } }
    case 'flipItem':
      return { kind: 'items', command: { type: 'flip', id: command.item, flipped: command.flipped } }
    case 'useItem':
      return { kind: 'items', command: { type: 'use', id: command.item, discard: command.discard } }
    case 'buyItem':
      return {
        kind: 'items',
        command: {
          type: 'add',
          item: command.item,
          owner: command.owner,
          slot: command.owner === 'stash' ? undefined : (command.slot ?? 'pack'),
          price: command.price,
        },
      }
    case 'attachUpgrade':
      return {
        kind: 'items',
        command: { type: 'attachUpgrade', id: command.item, upgrade: command.upgrade, price: command.price },
      }
    case 'damage':
      return {
        kind: 'encounter',
        command: { type: 'damage', instance: `adventurer:${command.adventurer}`, amount: command.amount },
      }
    case 'effect':
      return {
        kind: 'encounter',
        command: {
          type: 'effect',
          instance: `adventurer:${command.adventurer}`,
          effect: command.effect,
          active: command.active,
        },
      }
    case 'learnDiscipline':
      return {
        kind: 'party',
        command: { type: 'learn', adventurer: command.adventurer, discipline: command.discipline, xp: 0 },
      }
  }
}
