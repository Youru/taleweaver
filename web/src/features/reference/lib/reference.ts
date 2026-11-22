import type { Reference, RuleEntry } from '../api/queries'

export const tables = ['effects', 'terrainTypes', 'abilityTokens', 'combatantTokens', 'tags'] as const
export type Table = (typeof tables)[number]

const plain = (text: string) =>
  text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()

/** The entries whose name or text holds every word typed, table by table. */
export function search(reference: Reference, typed: string): { table: Table; entries: RuleEntry[] }[] {
  const words = plain(typed).split(/\s+/).filter(Boolean)
  return tables
    .map((table) => ({
      table,
      entries: reference[table].filter((entry) => {
        const text = plain([...Object.values(entry.name), ...Object.values(entry.text ?? {})].join(' '))
        return words.every((word) => text.includes(word))
      }),
    }))
    .filter((found) => found.entries.length > 0)
}

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
