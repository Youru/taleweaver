import type { Reference, RuleEntry } from '../api/queries'

export const tables = ['effects', 'terrainTypes', 'abilityTokens', 'combatantTokens', 'tags'] as const
export type Table = (typeof tables)[number]

/**
 * The rule of a token on the board: "um1:token/terrain/obstructing-long" is Obstructing, "um1:token/plank" a Plank.
 * The longest name wins; undefined for what the tables do not describe (totem, loot).
 */
export function ruleOfToken(reference: Reference, type: string | undefined): RuleEntry | undefined {
  const name = type?.split('/').pop()
  if (!name) return undefined
  const known = type!.includes('/terrain/')
    ? reference.terrainTypes
    : [...reference.abilityTokens, ...reference.combatantTokens, ...reference.effects]
  return known
    .filter((entry) => name === entry.id || name.startsWith(`${entry.id}-`))
    .sort((a, b) => b.id.length - a.id.length)[0]
}
