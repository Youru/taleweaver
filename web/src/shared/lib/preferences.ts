import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { Language } from './localized'

const storageKey = 'taleweaver.language'

function initialLanguage(): Language {
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored === 'fr' || stored === 'en') return stored
  } catch {
    // storage unavailable: default below
  }
  return 'fr'
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
  return { language }
})
