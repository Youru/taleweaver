import { ref } from 'vue'

/** The rules panel: one for the whole app, opened by any screen without leaving it. */
export const rulesOpen = ref(false)

export function openRules() {
  rulesOpen.value = true
}
