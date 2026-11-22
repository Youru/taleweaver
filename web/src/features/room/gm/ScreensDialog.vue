<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import QRCode from 'qrcode'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import Select from 'primevue/select'
import { roomGmApi } from '../api/roomApi'
import type { RoomView, ScreenRole } from '../api/types'
import { joinUrls } from '../lib/room'

/**
 * Where the GM plays and what the table sees (room-spec §7.6): one QR code per role, good once, hidden as soon as
 * it is scanned. On the GM's computer only.
 */
const props = defineProps<{ gameId: string; room: RoomView | undefined }>()
const emit = defineEmits<{ changed: [room: RoomView] }>()
const open = defineModel<boolean>('open', { required: true })
const { t } = useI18n()

/** The server's PairingLifetime */
const lifetime = 10 * 60_000
const roles = ['gm', 'table'] as const

const code = ref<{ role: ScreenRole; token: string }>()
const image = ref<string>()
const link = ref<string>()
const bases = ref<string[]>([])
const base = ref<string>()
const noWifi = ref(false)
const notice = ref<{ severity: 'success' | 'warn'; text: string }>()
const error = ref<string>()
const busy = ref(false)
let expiry: number | undefined

function hide() {
  window.clearTimeout(expiry)
  code.value = undefined
  image.value = undefined
  link.value = undefined
}

async function showCode(role: ScreenRole) {
  hide()
  notice.value = undefined
  error.value = undefined
  busy.value = true
  try {
    let room = props.room
    if (room?.status !== 'open') room = await roomGmApi.open(props.gameId)
    const { token } = await roomGmApi.createScreen(props.gameId, role)
    room = await roomGmApi.get(props.gameId)
    emit('changed', room)
    noWifi.value = room.lanAddresses.length === 0
    if (noWifi.value) return
    bases.value = joinUrls(room.code, room.lanAddresses, window.location).map((url) => new URL(url).origin)
    if (!base.value || !bases.value.includes(base.value)) base.value = bases.value[0]
    code.value = { role, token }
    expiry = window.setTimeout(() => {
      hide()
      notice.value = { severity: 'warn', text: t('screens.expired') }
    }, lifetime)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

async function revoke(role: ScreenRole) {
  hide()
  notice.value = undefined
  try {
    emit('changed', await roomGmApi.revokeScreens(props.gameId, role))
  } catch (e) {
    error.value = (e as Error).message
  }
}

watch([code, base], async () => {
  if (!code.value || !base.value) return
  const url = `${base.value}/screen/${code.value.token}`
  const drawn = await QRCode.toDataURL(url, { margin: 1, width: 480 })
  // Scanned or replaced meanwhile
  if (!code.value || !url.endsWith(code.value.token)) return
  image.value = drawn
  link.value = url
})

watch(
  () => props.room,
  (room) => {
    const role = code.value?.role
    if (!role || !room || busy.value || room.screens.waiting.includes(role)) return
    hide()
    if (room.status === 'open') notice.value = { severity: 'success', text: t(`screens.${role}.done`) }
  },
)
watch(open, (value) => {
  if (value) return
  hide()
  notice.value = undefined
  error.value = undefined
})
onBeforeUnmount(hide)

const count = (role: ScreenRole) => {
  const screens = props.room?.status === 'open' ? props.room.screens : undefined
  return role === 'gm' ? Number(screens?.gm ?? false) : (screens?.tables ?? 0)
}
</script>

<template>
  <Dialog v-model:visible="open" modal :header="t('screens.title')" :style="{ width: 'min(48rem, 95vw)' }">
    <p class="muted">{{ t('screens.intro') }}</p>
    <div class="roles">
      <section v-for="role in roles" :key="role" :class="{ chosen: code?.role === role }">
        <h3>
          <i :class="role === 'gm' ? 'pi pi-tablet' : 'pi pi-eye'" aria-hidden="true" />
          {{ t(`screens.${role}.title`) }}
        </h3>
        <p>{{ t(`screens.${role}.intro`) }}</p>
        <p v-if="count(role)" class="muted">{{ t(`screens.${role}.linked`, { n: count(role) }) }}</p>
        <div class="buttons">
          <Button :label="t(`screens.${role}.show`)" icon="pi pi-qrcode" :disabled="busy" @click="showCode(role)" />
          <Button
            v-if="count(role)"
            :label="t(`screens.${role}.revoke`)"
            severity="danger"
            text
            :disabled="busy"
            @click="revoke(role)"
          />
        </div>
      </section>
    </div>
    <Message v-if="error" severity="error">{{ error }}</Message>
    <Message v-if="noWifi" severity="warn">{{ t('screens.noWifi') }}</Message>
    <Message v-if="notice" :severity="notice.severity">{{ notice.text }}</Message>
    <div v-if="image && code" class="code">
      <img :src="image" :alt="t(`screens.${code.role}.qr`)" />
      <Select v-if="bases.length > 1" v-model="base" :options="bases" :aria-label="t('room.gm.address')" />
      <p class="muted">{{ t('screens.scan') }}</p>
      <code class="link">{{ link }}</code>
    </div>
  </Dialog>
</template>

<style scoped>
.roles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}
section {
  display: flex;
  flex-direction: column;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
section.chosen {
  border-color: var(--p-primary-color);
}
h3 {
  margin: 0;
}
section p {
  margin: 0.5rem 0;
}
.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: auto;
}
.muted {
  color: var(--p-text-muted-color);
}
.code {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}
.code img {
  width: min(15rem, 60vw, 40dvh);
  background: #fff;
}
.link {
  font-size: 0.75rem;
  overflow-wrap: anywhere;
}
</style>
