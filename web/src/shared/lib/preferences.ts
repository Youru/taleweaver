import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { Language } from './localized'

const storageKey = 'taleweaver.language'
const oddsKey = 'taleweaver.showOdds'

function initialLanguage(): Language {
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored === 'fr' || stored === 'en') return stored
  } catch {
    // storage unavailable: default below
  }
  return 'fr'
}

function initialShowOdds(): boolean {
  try {
    return localStorage.getItem(oddsKey) !== 'false'
  } catch {
    return true
  }
}

/** Reader preferences (client state only). */
export const usePreferences = defineStore('preferences', () => {
  const language = ref<Language>(initialLanguage())
  watch(language, (value) => {
    try {
      localStorage.setItem(storageKey, value)
    } catch {
      // not persisted: fine
    }
  })
  /** Show the odds of an attack or spell before the dice are rolled (some tables prefer not to know). */
  const showOdds = ref(initialShowOdds())
  watch(showOdds, (value) => {
    try {
      localStorage.setItem(oddsKey, String(value))
    } catch {
      // not persisted: fine
    }
  })
  return { language, showOdds }
})
