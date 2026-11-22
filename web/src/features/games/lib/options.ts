import type { GameOptions } from '../api/types'

export type OptionKey = Exclude<keyof GameOptions, 'urgencyLimit'>

/** The options of the rulebook (p. 86-88), as the launch box groups them. */
export const optionGroups: { group: 'variants' | 'harder' | 'easier'; keys: OptionKey[] }[] = [
  { group: 'variants', keys: ['noPlayerElimination', 'forgivingTerrain'] },
  { group: 'harder', keys: ['randomCounters', 'tougherMobs', 'deadlierBosses', 'bossRage', 'nobodyLeftBehind'] },
  {
    group: 'easier',
    keys: [
      'noScaling',
      'staticMonsters',
      'slowMonsters',
      'lessVariety',
      'favorableConditions',
      'reconnaissance',
      'xpLeniency',
      'removeUrgency',
    ],
  },
]

/** Those the app cannot play: the GM is reminded of them. */
const byHand = new Set<OptionKey>([
  'forgivingTerrain',
  'randomCounters',
  'deadlierBosses',
  'bossRage',
  'nobodyLeftBehind',
  'favorableConditions',
  'reconnaissance',
])
export const playedByHand = (key: OptionKey) => byHand.has(key)

export function activeOptions(options: GameOptions | null | undefined): OptionKey[] {
  return optionGroups.flatMap((g) => g.keys).filter((key) => !!options?.[key])
}

/** What is sent at the launch: nothing when every option is off, the limit only with its variant. */
export function optionsToSend(options: GameOptions): GameOptions | undefined {
  const active = activeOptions(options)
  if (!active.length) return undefined
  const sent: GameOptions = Object.fromEntries(active.map((key) => [key, true]))
  if (sent.noPlayerElimination && options.urgencyLimit) sent.urgencyLimit = options.urgencyLimit
  return sent
}

/** The Urgency token that loses the encounter. */
export const urgencyMax = (options: GameOptions | null | undefined) =>
  (options?.noPlayerElimination ? options.urgencyLimit : null) ?? 4
