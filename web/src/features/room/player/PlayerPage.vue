<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { ApiError } from '@/shared/api/http'
import { roomPlayerApi } from '../api/roomApi'
import { useRoomHub } from '../api/roomHub'
import type { PlayerView } from '../api/types'
import { forgetSeat, storedSeat } from '../lib/seatStorage'
import PlayerLayout from './PlayerLayout.vue'

/** /play/:code: a phone's seat, kept up to date by the hub (the whole view at each change, the newest wins). */
const props = defineProps<{ code: string }>()
const { t } = useI18n()
const router = useRouter()

const seat = storedSeat(props.code)
const view = ref<PlayerView>()
const followed = ref<string>()
const closed = ref(false)
const error = ref<string>()

function receive(next: PlayerView) {
  if (view.value && next.version < view.value.version) return
  view.value = next
  if (!followed.value || !next.adventurers.some((a) => a.name === followed.value))
    followed.value = next.seat?.followed ?? next.adventurers[0]?.name
}

async function load() {
  if (!seat) return
  try {
    receive(await roomPlayerApi.me(props.code, seat.token))
  } catch (e) {
    if (e instanceof ApiError && e.code === 'Rooms.Closed') closed.value = true
    else if (e instanceof ApiError && e.code === 'Rooms.InvalidSeatToken') {
      forgetSeat(props.code)
      void router.replace({ name: 'join', params: { code: props.code } })
    } else error.value = (e as Error).message
  }
}

async function follow(name: string) {
  followed.value = name
  if (!seat) return
  try {
    receive(await roomPlayerApi.update(props.code, seat.token, { followed: name }))
  } catch {
    // display only: the next push sets it right
  }
}

const hub = seat
  ? useRoomHub(
      `code=${encodeURIComponent(props.code)}&access_token=${encodeURIComponent(seat.token)}`,
      {
        PlayerViewChanged: (next: PlayerView) => receive(next),
        RoomClosed: () => (closed.value = true),
      },
      () => void load(),
    )
  : undefined
const status = hub?.status

onMounted(() => {
  if (!seat) void router.replace({ name: 'join', params: { code: props.code } })
  else void load()
})
</script>

<template>
  <div v-if="closed" class="center">
    <i class="pi pi-moon" aria-hidden="true" />
    <p>{{ t('room.player.closed') }}</p>
    <RouterLink :to="{ name: 'join' }"><Button :label="t('room.join.another')" text /></RouterLink>
  </div>
  <Message v-else-if="error" severity="error">{{ error }}</Message>
  <PlayerLayout v-else-if="view" :view="view" :followed="followed" :status="status" @follow="follow" />
  <div v-else class="center"><ProgressSpinner :aria-label="t('common.loading')" /></div>
</template>

<style scoped>
.center {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 1rem;
}
.center .pi {
  font-size: 2.5rem;
}
</style>
