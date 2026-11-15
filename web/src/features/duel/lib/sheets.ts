import { ref, watch, type Ref } from 'vue'
import type { AdventurerSheet } from './fighters'

/**
 * The adventurers' dice, Defense and Armor, as the GM last entered them, per game. Kept in this
 * browser only: a convenience, the game itself does not need them.
 */
export function useAdventurerSheets(game: () => string): {
  sheets: Ref<Record<string, AdventurerSheet>>
  remember: (name: string, patch: Partial<AdventurerSheet>) => void
} {
  const key = () => `taleweaver.duel.${game()}`
  const sheets = ref<Record<string, AdventurerSheet>>(read(key()))
  watch(key, (value) => (sheets.value = read(value)))

  function remember(name: string, patch: Partial<AdventurerSheet>) {
    sheets.value = { ...sheets.value, [name]: { ...sheets.value[name], ...patch } as AdventurerSheet }
    try {
      localStorage.setItem(key(), JSON.stringify(sheets.value))
    } catch {
      // not kept: the GM enters them again
    }
  }

  return { sheets, remember }
}

function read(key: string): Record<string, AdventurerSheet> {
  try {
    const stored = localStorage.getItem(key)
    return stored ? (JSON.parse(stored) as Record<string, AdventurerSheet>) : {}
  } catch {
    return {}
  }
}
