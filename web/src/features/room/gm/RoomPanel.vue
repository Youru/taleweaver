<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import QRCode from 'qrcode'
import { useQueryClient } from '@tanstack/vue-query'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import { useDisciplines, useItems } from '@/features/catalog'
import { gameKeys, type AdventurerState, type HeldBy, type OwnedItem } from '@/features/games'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { storeToRefs } from 'pinia'
import { isHostDevice } from '@/shared/lib/screen'
import ScreensDialog from './ScreensDialog.vue'
import RequestList from '../components/RequestList.vue'
import type { Autonomy } from '../api/types'
import { roomGmApi } from '../api/roomApi'
import { useRoomHub } from '../api/roomHub'
import type { RoomView } from '../api/types'
import { joinUrls, seatColors, trustable } from '../lib/room'

/**
 * The GM's room panel (room-spec §4.1, §4.3, §7): open/close, code and QR code, the seats and the adventurers each
 * one holds, and every adventurer of the party with its holder and "Open as a player".
 */
const props = defineProps<{
  gameId: string
  party: AdventurerState[]
  activeGroup?: string[] | null
  /** The stash and the items to hand out: the requests name their cards. */
  stash?: OwnedItem[]
}>()
const open = defineModel<boolean>('open', { required: true })
const screensOpen = defineModel<boolean>('screens', { default: false })
const host = isHostDevice()
const { t } = useI18n()

const queries = useQueryClient()
/** An accepted request, a player's own move, the tablet: the GM's page follows every change of the game. */
function reloadGame() {
  return Promise.all([
    queries.invalidateQueries({ queryKey: gameKeys.detail(props.gameId) }),
    queries.invalidateQueries({ queryKey: gameKeys.hiddenCards(props.gameId) }),
  ])
}
const room = ref<RoomView>()
const error = ref<string>()
const busy = ref(false)
const qr = ref<string>()
const big = ref(false)
const url = ref<string>()

const isOpen = computed(() => room.value?.status === 'open')
const urls = computed(() => (room.value ? joinUrls(room.value.code, room.value.lanAddresses, window.location) : []))
const names = computed(() => props.party.map((a) => a.name))
const holder = (name: string) => room.value?.seats.find((s) => s.adventurers.includes(name))

async function run(action: () => Promise<RoomView>) {
  busy.value = true
  error.value = undefined
  try {
    room.value = await action()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

async function load() {
  try {
    room.value = await roomGmApi.get(props.gameId)
  } catch {
    room.value = undefined
  }
}

/** One active, free adventurer per seat, in the order the seats came (the GM adjusts). */
async function share() {
  if (!room.value) return
  const active = props.party
    .filter((a) => (props.activeGroup ? props.activeGroup.includes(a.name) : !a.injured && !a.unselectable))
    .map((a) => a.name)
  const free = active.filter((name) => !holder(name))
  for (const seat of room.value.seats) {
    if (seat.adventurers.length > 0) continue
    const next = free.shift()
    if (!next) break
    await run(() => roomGmApi.assign(props.gameId, seat.id, [next]))
  }
}

function assign(seatId: string, adventurers: string[]) {
  const seat = room.value?.seats.find((s) => s.id === seatId)
  const taken = adventurers.filter((a) => {
    const other = holder(a)
    return other && other.id !== seatId && other.connected
  })
  if (taken.length && !window.confirm(t('room.gm.confirmMove', { names: taken.join(', ') }))) return
  if (seat) void run(() => roomGmApi.assign(props.gameId, seatId, adventurers))
}

function removeSeat(seatId: string, name: string) {
  if (window.confirm(t('room.gm.confirmRemove', { name }))) void run(() => roomGmApi.removeSeat(props.gameId, seatId))
}

function since(date: string) {
  const minutes = Math.round((Date.now() - new Date(date).getTime()) / 60000)
  return minutes < 1 ? t('room.gm.justNow') : t('room.gm.minutesAgo', { n: minutes })
}

watch(urls, (list) => {
  if (!url.value || !list.includes(url.value)) url.value = list[0]
})
watch(
  url,
  async (value) => {
    qr.value = value ? await QRCode.toDataURL(value, { margin: 1, width: 640 }) : undefined
  },
  { immediate: true },
)
// Loaded at once (the seat colours show on the track), then again each time the panel opens.
void load()
watch(open, (value) => value && void load())

/** Each held adventurer's seat (colour, phone connected), for the GM's initiative track (room-spec §7.3). */
const emit = defineEmits<{
  seats: [seats: Record<string, HeldBy>]
  players: [players: { id: string; name: string }[]]
  pending: [count: number]
  open: [open: boolean]
}>()
watch(isOpen, (value) => emit('open', value), { immediate: true })

// The players' requests (room-spec §6.2): the queue, and how much they may do alone (§6.3)
const { language } = storeToRefs(usePreferences())
const items = useItems()
const disciplines = useDisciplines()
const owned = computed(() => [
  ...props.party.flatMap((a) => [...(a.equipped ?? []), ...(a.pack ?? [])]),
  ...(props.stash ?? []),
])
const itemName = (id: string) => {
  const card = owned.value.find((i) => i.id === id)?.item
  // A purchase names the catalogue's card, not one the party holds
  return pick(items.data.value?.find((i) => i.id === (card ?? id))?.name, language.value) || id
}
const disciplineName = (id: string) =>
  pick(disciplines.data.value?.find((d) => d.id === id)?.name, language.value) || id
const pendingCount = computed(() => room.value?.requests.filter((r) => r.status === 'pending').length ?? 0)
watch(pendingCount, (count) => emit('pending', count), { immediate: true })
const autonomies: { value: Autonomy; label: string }[] = [
  { value: 'validateAll', label: t('room.gm.autonomy.validateAll') },
  { value: 'freeInventoryOutsideEncounter', label: t('room.gm.autonomy.freeInventoryOutsideEncounter') },
]
function refuse(id: string) {
  const reason = window.prompt(t('room.request.refuseReason')) ?? undefined
  void run(() => roomGmApi.refuse(props.gameId, id, reason || undefined))
}
const trusts = computed(() => trustable.map((value) => ({ value, label: t(`room.gm.trust.${value}`) })))
watch(
  room,
  (value) =>
    emit(
      'seats',
      Object.fromEntries(
        value?.status === 'open'
          ? value.seats.flatMap((seat) =>
              seat.adventurers.map((name) => [
                name,
                { color: seatColors[seat.color] ?? seat.color, name: seat.displayName, connected: seat.connected },
              ]),
            )
          : [],
      ),
    ),
  { immediate: true },
)

watch(room, (value) => emit('players', value?.seats.map((seat) => ({ id: seat.id, name: seat.displayName })) ?? []), {
  immediate: true,
})
useRoomHub(
  `game=${encodeURIComponent(props.gameId)}`,
  {
    RoomChanged: (next: RoomView) => (room.value = next),
    GameChanged: () => void reloadGame(),
    ScreenRevoked: () => void reloadGame(),
  },
  () => {
    void load()
    void reloadGame()
  },
)
</script>

<template>
  <ScreensDialog
    v-if="host"
    v-model:open="screensOpen"
    :game-id="gameId"
    :room="room"
    @changed="(next) => (room = next)"
  />
  <Dialog
    v-model:visible="open"
    modal
    :header="t('room.gm.title')"
    class="room-panel"
    :style="{ width: 'min(60rem, 95vw)' }"
  >
    <Message v-if="error" severity="error" closable @close="error = undefined">{{ error }}</Message>
    <div v-if="!isOpen" class="closed">
      <p>{{ t('room.gm.intro') }}</p>
      <Button
        v-if="host"
        icon="pi pi-users"
        :label="t('room.gm.open')"
        :loading="busy"
        @click="run(() => roomGmApi.open(gameId))"
      />
    </div>
    <template v-else-if="room">
      <section class="join">
        <button type="button" class="qr" :aria-label="t('room.gm.enlarge')" @click="big = true">
          <img v-if="qr" :src="qr" :alt="t('room.gm.qr')" />
        </button>
        <div>
          <p class="code">{{ room.code }}</p>
          <Select v-if="urls.length > 1" v-model="url" :options="urls" :aria-label="t('room.gm.address')" />
          <p v-else class="url">{{ url }}</p>
          <p class="muted">{{ t('room.gm.howToJoin') }}</p>
          <div class="buttons">
            <Button
              icon="pi pi-window-maximize"
              :label="t('room.gm.enlarge')"
              severity="secondary"
              @click="big = true"
            />
            <Button
              icon="pi pi-sitemap"
              :label="t('room.gm.share')"
              severity="secondary"
              :disabled="busy"
              @click="share"
            />
            <Button
              v-if="host"
              icon="pi pi-power-off"
              :label="t('room.gm.close')"
              severity="danger"
              text
              :loading="busy"
              @click="run(() => roomGmApi.close(gameId))"
            />
          </div>
        </div>
      </section>

      <section class="requests-queue">
        <h3>
          {{ t('room.request.queue') }}
          <Tag v-if="pendingCount" severity="warn" :value="String(pendingCount)" />
        </h3>
        <label class="autonomy">
          <span>{{ t('room.gm.autonomy.label') }}</span>
          <Select
            :model-value="room.autonomy"
            :options="autonomies"
            option-label="label"
            option-value="value"
            :disabled="busy"
            @update:model-value="(value: Autonomy) => run(() => roomGmApi.settings(gameId, value))"
          />
        </label>
        <p v-if="room.requests.length === 0" class="muted">{{ t('room.request.none') }}</p>
        <Button
          v-if="pendingCount > 1"
          icon="pi pi-check-square"
          :label="t('room.request.acceptAll', { n: pendingCount })"
          class="accept-all"
          :disabled="busy"
          @click="run(() => roomGmApi.acceptAll(gameId))"
        />
        <RequestList
          v-if="room.requests.length"
          gm
          :requests="room.requests"
          :busy="busy"
          :item-name="itemName"
          :discipline-name="disciplineName"
          @accept-from="(seat) => run(() => roomGmApi.acceptAll(gameId, seat))"
          @accept="(id) => run(() => roomGmApi.accept(gameId, id))"
          @refuse="refuse"
        />
      </section>

      <section v-if="host" class="screen">
        <Button icon="pi pi-tablet" :label="t('screens.title')" severity="secondary" @click="screensOpen = true" />
      </section>

      <div class="columns">
        <section>
          <h3>{{ t('room.gm.seats', { n: room.seats.length }) }}</h3>
          <p v-if="room.seats.length === 0" class="muted">{{ t('room.gm.noSeat') }}</p>
          <ul class="seats">
            <li v-for="seat in room.seats" :key="seat.id">
              <div class="seat-head">
                <span class="dot" :style="{ background: seatColors[seat.color] }" aria-hidden="true" />
                <strong>{{ seat.displayName }}</strong>
                <Tag
                  :severity="seat.connected ? 'success' : 'secondary'"
                  :value="seat.connected ? t('room.gm.connected') : t('room.gm.seen', { when: since(seat.lastSeen) })"
                />
                <Button
                  icon="pi pi-times"
                  severity="secondary"
                  text
                  rounded
                  :aria-label="t('room.gm.remove')"
                  @click="removeSeat(seat.id, seat.displayName)"
                />
              </div>
              <MultiSelect
                :model-value="seat.adventurers"
                :options="names"
                :placeholder="t('room.gm.giveAdventurers')"
                :aria-label="t('room.gm.giveTo', { name: seat.displayName })"
                display="chip"
                :disabled="busy"
                @update:model-value="(value: string[]) => assign(seat.id, value)"
              />
              <MultiSelect
                :model-value="seat.trusted ?? []"
                :options="trusts"
                option-label="label"
                option-value="value"
                :placeholder="t('room.gm.trust.none')"
                :aria-label="t('room.gm.trust.label', { name: seat.displayName })"
                :disabled="busy"
                @update:model-value="(value: string[]) => run(() => roomGmApi.trust(gameId, seat.id, value))"
              />
            </li>
          </ul>
        </section>
        <section>
          <h3>{{ t('room.gm.party') }}</h3>
          <ul class="party">
            <li v-for="adventurer in party" :key="adventurer.name">
              <span
                class="dot"
                :style="{
                  background: holder(adventurer.name) ? seatColors[holder(adventurer.name)!.color] : 'transparent',
                }"
                aria-hidden="true"
              />
              <span class="name">{{ adventurer.name }}</span>
              <span class="muted">{{ holder(adventurer.name)?.displayName ?? t('room.gm.byGm') }}</span>
              <a
                :href="`/play/gm/${encodeURIComponent(gameId)}/${encodeURIComponent(adventurer.name)}`"
                target="_blank"
                rel="noopener"
              >
                <Button icon="pi pi-mobile" :label="t('room.gm.openAsPlayer')" severity="secondary" text size="small" />
              </a>
            </li>
          </ul>
        </section>
      </div>
    </template>
  </Dialog>
  <Dialog v-model:visible="big" modal :header="room?.code" :style="{ width: 'min(40rem, 95vw)' }">
    <img v-if="qr" :src="qr" :alt="t('room.gm.qr')" class="big-qr" />
    <p class="url center">{{ url }}</p>
  </Dialog>
</template>

<style scoped>
.requests-queue {
  margin-bottom: 1rem;
}
.requests-queue h3 {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 0.5rem;
}
.accept-all {
  margin-bottom: 0.5rem;
}
.autonomy {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}
.screen {
  margin-bottom: 1rem;
}
.closed {
  text-align: center;
}
.join {
  display: flex;
  gap: 1.5rem;
  align-items: center;
  flex-wrap: wrap;
}
.qr {
  border: 0;
  padding: 0;
  background: white;
  border-radius: 0.5rem;
  cursor: zoom-in;
}
.qr img {
  width: 10rem;
  height: 10rem;
  display: block;
}
.code {
  font-size: 2.5rem;
  font-weight: 700;
  letter-spacing: 0.3em;
  margin: 0;
}
.url {
  font-family: monospace;
  margin: 0.25rem 0;
}
.buttons {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.columns {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr));
  gap: 1.5rem;
  margin-top: 1rem;
}
h3 {
  font-size: 1rem;
}
ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.seats li {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.seat-head,
.party li {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.party .name {
  font-weight: 600;
}
.party a {
  margin-left: auto;
}
.dot {
  width: 0.85rem;
  height: 0.85rem;
  border-radius: 50%;
  border: 1px solid var(--p-content-border-color);
  flex: none;
}
.muted {
  color: var(--p-text-muted-color);
}
.big-qr {
  width: 100%;
  background: white;
  border-radius: 0.5rem;
}
.center {
  text-align: center;
}
</style>
