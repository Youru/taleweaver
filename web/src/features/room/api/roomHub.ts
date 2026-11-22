import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { onBeforeUnmount, ref } from 'vue'
import { currentScreenToken } from '@/shared/lib/screen'

/** The GM's tablet proves itself with its token. */
function withScreen(query: string) {
  const token = currentScreenToken()
  return token && query.startsWith('game=') ? `${query}&screen_token=${encodeURIComponent(token)}` : query
}

export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'closed'

/** Backoff of the automatic reconnection (room-spec §4.6), then every 30 s for as long as the page lives. */
const retryDelays = [0, 2000, 5000, 10000, 30000]

/**
 * A connection to /hubs/room for the life of the component: the server pushes, the page only listens.
 * `query`: "code=KRVA&access_token=…" for a phone, "game=<id>" for the GM's
 * device, "table_token=…" for a table screen. `onReconnected`: read again what may have been missed.
 */
export function useRoomHub(
  query: string,
  handlers: Record<string, (...args: never[]) => void>,
  onReconnected?: () => void,
) {
  const status = ref<HubStatus>('connecting')
  const connection: HubConnection = new HubConnectionBuilder()
    .withUrl(`/hubs/room?${withScreen(query)}`)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) => retryDelays[previousRetryCount] ?? 30000,
    })
    .configureLogging(LogLevel.Warning)
    .build()
  // Wrapped: a handler that returns a value would be taken by SignalR for a result the server never asked for.
  for (const [name, handler] of Object.entries(handlers))
    connection.on(name, (...args: unknown[]) => {
      ;(handler as (...a: unknown[]) => unknown)(...args)
    })
  connection.onreconnecting(() => (status.value = 'reconnecting'))
  connection.onreconnected(() => {
    status.value = 'connected'
    onReconnected?.()
  })
  connection.onclose(() => (status.value = 'closed'))

  let stopped = false
  let starting = false
  let retry: ReturnType<typeof setTimeout> | undefined
  async function start(missed: boolean) {
    if (stopped || starting) return
    starting = true
    clearTimeout(retry)
    try {
      await connection.start()
      status.value = 'connected'
      if (missed) onReconnected?.()
    } catch {
      if (!stopped) {
        status.value = 'reconnecting'
        retry = setTimeout(() => void start(true), 5000)
      }
    } finally {
      starting = false
    }
  }
  // A device back from sleep, or on the Wi-Fi again: not after the backoff's 30 s, now
  async function wake() {
    if (document.visibilityState !== 'visible') return
    onReconnected?.()
    if (starting || connection.state === HubConnectionState.Connected) return
    await connection.stop()
    await start(true)
  }
  const onWake = () => void wake()
  document.addEventListener('visibilitychange', onWake)
  window.addEventListener('online', onWake)
  void start(false)
  onBeforeUnmount(() => {
    stopped = true
    clearTimeout(retry)
    document.removeEventListener('visibilitychange', onWake)
    window.removeEventListener('online', onWake)
    if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
  })
  return { status }
}
