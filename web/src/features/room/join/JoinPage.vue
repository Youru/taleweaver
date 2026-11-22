<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { roomPlayerApi } from '../api/roomApi'
import type { RoomSummary } from '../api/types'
import { CODE_LENGTH, normalizeCode, seatColors } from '../lib/room'
import { storeProfile, storeSeat, storedProfile, storedSeat } from '../lib/seatStorage'

/** /join/:code?: one screen, a name and a colour (room-spec §4.2). A phone that already sat here goes straight back. */
const props = defineProps<{ code?: string }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const router = useRouter()

const profile = storedProfile()
const code = ref(normalizeCode(props.code ?? ''))
const displayName = ref(profile?.displayName ?? '')
const color = ref(profile?.color ?? 'teal')
const room = ref<RoomSummary>()
const error = ref<string>()
const busy = ref(false)
const canJoin = computed(() => code.value.length === CODE_LENGTH && displayName.value.trim().length > 0 && !busy.value)

function message(e: unknown) {
  const codeOf = e instanceof ApiError ? e.code : undefined
  if (codeOf === 'Rooms.NotFound') return t('room.join.notFound')
  if (codeOf === 'Rooms.Closed') return t('room.join.closed')
  if (codeOf === 'Rooms.Full') return t('room.join.full')
  return (e as Error).message
}

async function find() {
  room.value = undefined
  error.value = undefined
  if (code.value.length !== CODE_LENGTH) return
  if (storedSeat(code.value)) {
    void router.replace({ name: 'player', params: { code: code.value } })
    return
  }
  try {
    room.value = await roomPlayerApi.find(code.value)
  } catch (e) {
    error.value = message(e)
  }
}

async function join() {
  busy.value = true
  error.value = undefined
  try {
    const joined = await roomPlayerApi.join(code.value, displayName.value.trim(), color.value)
    storeSeat({ code: code.value, seatId: joined.seatId, token: joined.seatToken })
    storeProfile({ displayName: displayName.value.trim(), color: color.value })
    void router.replace({ name: 'player', params: { code: code.value } })
  } catch (e) {
    error.value = message(e)
  } finally {
    busy.value = false
  }
}

watch(code, (value) => {
  const normalized = normalizeCode(value)
  if (normalized !== value) code.value = normalized
  else void find()
})
onMounted(find)
</script>

<template>
  <main class="join">
    <h1>{{ t('room.join.title') }}</h1>
    <p v-if="room" class="scenario">{{ pick(room.scenarioTitle, language) || room.scenario }}</p>
    <form @submit.prevent="join">
      <label>
        <span>{{ t('room.join.code') }}</span>
        <InputText
          v-model="code"
          class="code"
          autocomplete="off"
          autocapitalize="characters"
          :maxlength="CODE_LENGTH"
        />
      </label>
      <label>
        <span>{{ t('room.join.name') }}</span>
        <InputText v-model="displayName" autocomplete="nickname" maxlength="24" />
      </label>
      <fieldset>
        <legend>{{ t('room.join.color') }}</legend>
        <button
          v-for="(css, name) in seatColors"
          :key="name"
          type="button"
          class="swatch"
          :style="{ background: css }"
          :aria-label="t(`room.colors.${name}`)"
          :aria-pressed="color === name"
          @click="color = name"
        />
      </fieldset>
      <Message v-if="error" severity="error">{{ error }}</Message>
      <p v-if="room?.seats.length" class="muted">{{ t('room.join.already', { names: room.seats.join(', ') }) }}</p>
      <Button
        type="submit"
        :label="t('room.join.submit')"
        icon="pi pi-sign-in"
        :disabled="!canJoin || !room"
        :loading="busy"
      />
    </form>
  </main>
</template>

<style scoped>
.join {
  max-width: 26rem;
  margin: 0 auto;
  padding: 2rem 1rem;
}
h1 {
  font-size: 1.5rem;
  margin: 0 0 0.25rem;
}
form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-top: 1.5rem;
}
label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.code {
  font-size: 1.5rem;
  letter-spacing: 0.4em;
  text-transform: uppercase;
}
fieldset {
  border: 0;
  padding: 0;
  margin: 0;
  display: flex;
  gap: 0.6rem;
  flex-wrap: wrap;
}
legend {
  margin-bottom: 0.35rem;
}
.swatch {
  width: 2.75rem;
  height: 2.75rem;
  border-radius: 50%;
  border: 3px solid transparent;
}
.swatch[aria-pressed='true'] {
  border-color: var(--p-text-color);
}
.scenario,
.muted {
  color: var(--p-text-muted-color);
  margin: 0;
}
</style>
