import { onBeforeUnmount, reactive, watch } from 'vue'

/** A phone's own settings (room-spec §5.2-5.3, ⚙): kept on the phone only. */
export interface PlayerSettings {
  /** Vibrate when it is the seat's turn. */
  vibrate: boolean
  /** Show the adventurer whose turn starts. */
  autoFollow: boolean
  /** Keep the screen on during the game (Screen Wake Lock API). */
  keepAwake: boolean
}

const key = 'taleweaver.room.settings'
const defaults: PlayerSettings = { vibrate: true, autoFollow: true, keepAwake: true }

function load(): PlayerSettings {
  try {
    return { ...defaults, ...(JSON.parse(localStorage.getItem(key) ?? '{}') as Partial<PlayerSettings>) }
  } catch {
    return { ...defaults }
  }
}

let settings: PlayerSettings | undefined

export function usePlayerSettings(): PlayerSettings {
  if (!settings) {
    settings = reactive(load())
    watch(settings, (value) => {
      try {
        localStorage.setItem(key, JSON.stringify(value))
      } catch {
        // not kept: fine for this page
      }
    })
  }
  return settings
}

/** The screen stays on while the page is shown and the setting is on; released otherwise. */
export function useWakeLock(settings: PlayerSettings) {
  let lock: { release: () => Promise<void> } | undefined
  async function update() {
    const wakeLock = (navigator as Navigator & { wakeLock?: { request: (type: 'screen') => Promise<typeof lock> } })
      .wakeLock
    if (settings.keepAwake && document.visibilityState === 'visible' && wakeLock && !lock) {
      try {
        lock = await wakeLock.request('screen')
      } catch {
        lock = undefined
      }
    } else if (!settings.keepAwake && lock) {
      await lock.release().catch(() => undefined)
      lock = undefined
    }
  }
  // The browser drops the lock when the page is hidden: taken again when it comes back
  const onVisibility = () => {
    if (document.visibilityState === 'hidden') lock = undefined
    void update()
  }
  document.addEventListener('visibilitychange', onVisibility)
  watch(
    () => settings.keepAwake,
    () => void update(),
    { immediate: true },
  )
  onBeforeUnmount(() => {
    document.removeEventListener('visibilitychange', onVisibility)
    void lock?.release().catch(() => undefined)
  })
}
