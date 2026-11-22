<script setup lang="ts">
import { computed, provide, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Message from 'primevue/message'
import { useAdventurers, useItems } from '@/features/catalog'
import { formatLog, GameJournal } from '@/features/games'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import { usePlayerSettings, useWakeLock } from '../lib/playerSettings'
import { assetUrl, thumbnailWidthKey } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { HubStatus } from '../api/roomHub'
import type { PlayerCommand, PlayerView } from '../api/types'
import { nextAdventurer, seatColors, turnOf } from '../lib/room'
import BoardTab from './BoardTab.vue'
import ChooseAdventurer from './ChooseAdventurer.vue'
import EquipmentTab from './EquipmentTab.vue'
import HeroTab from './HeroTab.vue'
import ShopTab from './ShopTab.vue'
import TurnTab from './TurnTab.vue'

/**
 * The phone (room-spec §5): portrait, one column, the adventurer switcher and the live status on top, the tabs
 * at the bottom where the thumb is. A swipe on the content changes adventurer.
 */
const props = defineProps<{
  view: PlayerView
  followed: string | undefined
  status?: HubStatus
  /** Ask for something (a seat), or do it (the GM playing an adventurer). Without it, read only. */
  act?: (command: PlayerCommand) => Promise<void>
  cancel?: (request: string) => Promise<void>
}>()
const emit = defineEmits<{ follow: [name: string] }>()
const { t, te } = useI18n()
const { language } = storeToRefs(usePreferences())
// The cards in grids come small to the phone (room-spec §10.3)
provide(thumbnailWidthKey, 320)
const settings = usePlayerSettings()
if (!props.view.impersonated) useWakeLock(settings)
const settingsOpen = ref(false)
const languages = [
  { value: 'fr', label: 'Français' },
  { value: 'en', label: 'English' },
]
const cards = useAdventurers()

type Tab = 'hero' | 'equipment' | 'turn' | 'board' | 'shop' | 'journal'
const chosen = ref<Tab>('hero')
const fighting = computed(() => !!props.view.encounter && !props.view.encounter.ended)
const hasBoard = computed(() => fighting.value && !!props.view.encounter?.board)
/** The Tour tab exists only during an encounter: outside one, the hero's sheet shows instead. */
const tab = computed<Tab>({
  get: () =>
    (chosen.value === 'turn' && !fighting.value) ||
    (chosen.value === 'board' && !hasBoard.value) ||
    (chosen.value === 'shop' && !props.view.shop)
      ? 'hero'
      : chosen.value,
  set: (value) => (chosen.value = value),
})
const journal = computed(() => props.view.log.map((line) => line.entry))
const names = computed(() => props.view.adventurers.map((a) => a.name))
const current = computed(
  () => props.view.adventurers.find((a) => a.name === props.followed) ?? props.view.adventurers[0],
)
const cardOf = (name: string) => cards.data.value?.find((c) => pick(c.name, 'en') === name || c.id === name)
const color = computed(() => seatColors[props.view.seat?.color ?? ''] ?? 'var(--p-primary-color)')
const healthLeft = computed(() => (current.value?.health ?? 0) - (current.value?.damage ?? 0))
const healthRatio = computed(() => (current.value?.health ? Math.max(0, healthLeft.value) / current.value.health : 0))
const effectsNow = computed(() =>
  current.value ? (props.view.encounter?.adventurerEffects[current.value.name] ?? []) : [],
)

// A hit makes the strip flash red and shake, a heal flashes green (room-spec §5.2); same adventurer only.
const hit = ref<'hurt' | 'healed' | null>(null)
const delta = ref(0)
let hitTimer: ReturnType<typeof setTimeout> | undefined
watch([() => current.value?.name, healthLeft], ([name, left], [before, previous]) => {
  if (name !== before || previous === undefined || left === previous) return
  delta.value = left - previous
  hit.value = left < previous ? 'hurt' : 'healed'
  clearTimeout(hitTimer)
  hitTimer = setTimeout(() => (hit.value = null), 900)
})

// The GM's answer to a request (room-spec §6.2): a short line that fades, longer when refused.
const others = computed(() => props.view.party.filter((p) => !p.mine).map((p) => p.name))
const answer = ref<{ ok: boolean; text: string } | null>(null)
let answerTimer: ReturnType<typeof setTimeout> | undefined
watch(
  () => props.view.requests.map((r) => `${r.id}:${r.status}`).join(),
  (now, before) => {
    if (!before) return
    const was = new Map(before.split(',').map((pair) => pair.split(':') as [string, string]))
    const changed = props.view.requests.find((r) => was.get(r.id) === 'pending' && r.status !== 'pending')
    if (!changed || changed.status === 'cancelled') return
    const ok = changed.status === 'accepted' || changed.status === 'applied'
    answer.value = { ok, text: t(ok ? 'room.request.acceptedNotice' : 'room.request.refusedNotice') }
    clearTimeout(answerTimer)
    answerTimer = setTimeout(() => (answer.value = null), ok ? 2500 : 5000)
  },
)

// A card that arrives in the seat's hands (loot, a purchase, a gift): it turns over on the screen (room-spec §5.2).
const catalogItems = useItems()
const ownedIds = computed(() =>
  props.view.adventurers.flatMap((a) =>
    [...(a.equipped ?? []), ...(a.pack ?? [])].map((i) => ({ id: i.id, item: i.item, owner: a.name })),
  ),
)
const received = ref<{ item?: string | null; owner: string } | null>(null)
watch([names, ownedIds], ([, now], [held, before]) => {
  // Before the departure the cards are dealt again at each choice: nothing received
  if (props.view.preparation) return
  const known = new Set(before.map((i) => i.id))
  // An adventurer just given to the seat brings their cards: none of them is new
  const fresh = now.find((i) => !known.has(i.id) && held.includes(i.owner))
  if (fresh) received.value = { item: fresh.item, owner: fresh.owner }
})
const receivedCard = computed(() =>
  received.value?.item ? catalogItems.data.value?.find((i) => i.id === received.value?.item) : undefined,
)

// A seat without an adventurer may ask for a free one (room-spec §6.2); the GM decides.
const asking = computed(
  () =>
    new Set(
      props.view.requests.flatMap((r) =>
        r.status === 'pending' && r.command.type === 'takeAdventurer' ? [r.command.adventurer] : [],
      ),
    ),
)
const askError = ref<string>()
const askOpen = ref(false)
const askable = computed(() => props.view.party.filter((member) => member.active && !member.mine))
async function askFor(name: string) {
  askError.value = undefined
  try {
    await props.act?.({ type: 'takeAdventurer', adventurer: name })
  } catch (e) {
    askError.value = (e as Error).message
  }
}

// The end of an encounter: what the party won, on one screen (room-spec §5.2)
const recap = ref<{ title: string; lines: string[] } | null>(null)
watch(
  () => props.view.log.at(-1)?.index,
  (last, before) => {
    if (before === undefined || last === undefined) return
    const fresh = props.view.log.filter((l) => l.index > before)
    const end = fresh.find((l) => l.entry.code === 'encounterEnd')
    if (!end) return
    const format = (entry: (typeof fresh)[number]['entry']) =>
      formatLog(
        entry,
        language.value,
        (key, args) => t(key, args),
        (key) => te(key),
      )
    const lines = fresh
      .filter((l) => l.index > end.index)
      .map((l) => format(l.entry))
      .filter((l) => l.tone === 'reward')
      .map((l) => [l.title, l.text].filter(Boolean).join(' '))
    recap.value = { title: format(end.entry).story?.heading ?? format(end.entry).title, lines }
  },
)

// "Your turn!" (room-spec §5.2): full screen and a vibration when one of the seat's adventurers starts playing.
const turn = computed(() => (props.view.encounter ? turnOf(props.view.encounter, names.value) : {}))
const calling = ref<string | null>(null)
watch(
  () => turn.value.playing,
  (playing, before) => {
    if (!playing || playing === before) return
    calling.value = playing
    if (settings.autoFollow && playing !== current.value?.name) emit('follow', playing)
    if (!props.view.impersonated && settings.vibrate) navigator.vibrate?.([200, 100, 200])
  },
)

let touchX: number | undefined
function touchStart(event: TouchEvent) {
  touchX = event.touches[0]?.clientX
}
function touchEnd(event: TouchEvent) {
  const end = event.changedTouches[0]?.clientX
  if (touchX === undefined || end === undefined || Math.abs(end - touchX) < 60 || names.value.length < 2) return
  const next = nextAdventurer(names.value, current.value?.name, end < touchX ? 1 : -1)
  if (next) emit('follow', next)
}
</script>

<template>
  <div class="player" :style="{ '--seat': color }">
    <header>
      <div class="switcher" role="tablist" :aria-label="t('room.player.switcher')">
        <button
          v-for="adventurer in view.adventurers"
          :key="adventurer.name"
          type="button"
          role="tab"
          :aria-selected="adventurer.name === current?.name"
          :class="{ current: adventurer.name === current?.name, defeated: adventurer.defeated }"
          @click="emit('follow', adventurer.name)"
        >
          <img
            v-if="cardOf(adventurer.name)?.image"
            :src="assetUrl(cardOf(adventurer.name)?.image, 160)"
            alt=""
            aria-hidden="true"
          />
          <span>{{ adventurer.name }}</span>
        </button>
      </div>
      <Button
        v-if="act && !view.impersonated && !view.preparation && current && askable.length"
        icon="pi pi-user-plus"
        text
        rounded
        :aria-label="t('room.player.askAnother')"
        :title="t('room.player.askAnother')"
        @click="((askOpen = true), (askError = undefined))"
      />
      <span class="who">
        <span class="dot" aria-hidden="true" />
        {{ view.impersonated ? t('room.player.gm') : view.seat?.displayName }}
      </span>
      <Button
        icon="pi pi-cog"
        text
        rounded
        :aria-label="t('room.player.settings.title')"
        :title="t('room.player.settings.title')"
        @click="settingsOpen = true"
      />
    </header>
    <div v-if="current && !view.preparation" class="strip" :class="hit" :aria-label="t('room.player.status')">
      <span class="health" :title="t('room.player.health')">
        <i class="pi pi-heart-fill" aria-hidden="true" />
        {{ healthLeft }}/{{ current.health ?? '?' }}
        <span class="bar"><span :style="{ width: `${healthRatio * 100}%` }" /></span>
      </span>
      <span class="sp" :title="t('room.player.stamina')">
        <span
          v-for="n in current.staminaMax ?? 0"
          :key="n"
          class="pip"
          :class="{ full: n <= (current.stamina ?? 0) }"
          aria-hidden="true"
        />
        <span class="sr-only">{{ t('room.player.spLeft', { n: current.stamina ?? 0 }) }}</span>
      </span>
      <span v-if="hit" class="delta" aria-hidden="true">{{ delta > 0 ? `+${delta}` : delta }}</span>
      <span v-if="effectsNow.length" class="effects-count" :title="t('room.player.effects')">
        <i class="pi pi-sparkles" aria-hidden="true" /> {{ effectsNow.length }}
      </span>
      <span v-if="view.encounter && !view.encounter.ended" class="round">
        {{ t('room.player.round', { round: view.encounter.round }) }}
      </span>
    </div>
    <Message v-if="answer" :severity="answer.ok ? 'success' : 'error'" class="banner" :closable="false">{{
      answer.text
    }}</Message>
    <Message v-if="status === 'reconnecting'" severity="warn" class="banner">{{
      t('room.player.reconnecting')
    }}</Message>

    <main @touchstart.passive="touchStart" @touchend.passive="touchEnd">
      <ChooseAdventurer
        v-if="view.preparation"
        :view="view"
        :preparation="view.preparation"
        :act="view.impersonated ? undefined : act"
        @follow="(name) => emit('follow', name)"
      />
      <div v-else-if="!current" class="waiting">
        <i class="pi pi-hourglass" aria-hidden="true" />
        <p>{{ t('room.player.waiting') }}</p>
        <p class="muted">{{ pick(view.scenarioTitle, language) }}</p>
        <ul class="others">
          <li v-for="member in view.party" :key="member.name">
            <span>{{ member.name }}</span>
            <Button
              v-if="act && member.active && !member.mine"
              :label="asking.has(member.name) ? t('room.request.waiting') : t('room.player.askFor')"
              size="small"
              :severity="asking.has(member.name) ? 'secondary' : undefined"
              :disabled="asking.has(member.name)"
              @click="askFor(member.name)"
            />
          </li>
        </ul>
        <p v-if="askError" class="error">{{ askError }}</p>
      </div>
      <Transition v-else name="flip" mode="out-in">
        <TurnTab v-if="tab === 'turn'" key="turn" :view="view" :followed="current.name" :act="act" />
        <BoardTab v-else-if="tab === 'board' && view.encounter" key="board" :encounter="view.encounter" />
        <ShopTab
          v-else-if="tab === 'shop' && view.shop"
          key="shop"
          :shop="view.shop"
          :gold="view.gold"
          :adventurer="current.name"
          :requests="view.requests"
          :act="act"
        />
        <div v-else-if="tab === 'journal'" key="journal" class="journal">
          <GameJournal :log="journal" page :highlight="names" />
        </div>
        <HeroTab
          v-else-if="tab === 'hero'"
          :key="`hero-${current.name}`"
          :adventurer="current"
          :card="cardOf(current.name)"
          :requests="view.requests"
          :act="act"
        />
        <EquipmentTab
          v-else
          :key="`equipment-${current.name}`"
          :adventurer="current"
          :gold="view.gold"
          :stash="view.stash"
          :upgrades="view.upgrades"
          :others="others"
          :requests="view.requests"
          :act="act"
          :cancel="cancel"
        />
      </Transition>
    </main>

    <nav v-if="!view.preparation" class="tabs" role="tablist">
      <button type="button" role="tab" :aria-selected="tab === 'hero'" @click="tab = 'hero'">
        <i class="pi pi-user" aria-hidden="true" />
        <span>{{ t('room.player.heroTab') }}</span>
      </button>
      <button type="button" role="tab" :aria-selected="tab === 'equipment'" @click="tab = 'equipment'">
        <i class="pi pi-briefcase" aria-hidden="true" />
        <span>{{ t('room.player.equipmentTab') }}</span>
      </button>
      <button
        v-if="fighting"
        type="button"
        role="tab"
        :aria-selected="tab === 'turn'"
        :class="{ alert: turn.playing }"
        @click="tab = 'turn'"
      >
        <i class="pi pi-bolt" aria-hidden="true" />
        <span>{{ t('room.player.turnTab') }}</span>
      </button>
      <button v-if="hasBoard" type="button" role="tab" :aria-selected="tab === 'board'" @click="tab = 'board'">
        <i class="pi pi-map" aria-hidden="true" />
        <span>{{ t('room.player.boardTab') }}</span>
      </button>
      <button v-if="view.shop" type="button" role="tab" :aria-selected="tab === 'shop'" @click="tab = 'shop'">
        <i class="pi pi-shopping-bag" aria-hidden="true" />
        <span>{{ t('room.shop.tab') }}</span>
      </button>
      <button type="button" role="tab" :aria-selected="tab === 'journal'" @click="tab = 'journal'">
        <i class="pi pi-book" aria-hidden="true" />
        <span>{{ t('room.player.journalTab') }}</span>
      </button>
    </nav>

    <Transition name="call">
      <button v-if="recap && !calling" type="button" class="received recap" @click="recap = null">
        <span>{{ t('room.player.recap') }}</span>
        <strong>{{ recap.title }}</strong>
        <ul v-if="recap.lines.length">
          <li v-for="(line, i) in recap.lines" :key="i">{{ line }}</li>
        </ul>
        <small>{{ t('room.player.tapToClose') }}</small>
      </button>
    </Transition>
    <Dialog v-model:visible="askOpen" modal dismissable-mask :header="t('room.player.askAnother')">
      <ul class="others ask">
        <li v-for="member in askable" :key="member.name">
          <span>{{ member.name }}</span>
          <Button
            :label="asking.has(member.name) ? t('room.request.waiting') : t('room.player.askFor')"
            :severity="asking.has(member.name) ? 'secondary' : undefined"
            :disabled="asking.has(member.name)"
            @click="askFor(member.name)"
          />
        </li>
      </ul>
      <p v-if="askError" class="error">{{ askError }}</p>
    </Dialog>
    <Dialog v-model:visible="settingsOpen" modal dismissable-mask :header="t('room.player.settings.title')">
      <div class="settings">
        <label><ToggleSwitch v-model="settings.vibrate" /> {{ t('room.player.settings.vibrate') }}</label>
        <label><ToggleSwitch v-model="settings.autoFollow" /> {{ t('room.player.settings.autoFollow') }}</label>
        <label><ToggleSwitch v-model="settings.keepAwake" /> {{ t('room.player.settings.keepAwake') }}</label>
        <SelectButton
          v-model="language"
          :options="languages"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          :aria-label="t('app.language')"
        />
      </div>
    </Dialog>
    <Transition name="call">
      <button v-if="received && !calling" type="button" class="received" @click="received = null">
        <span>{{ t('room.player.newItem', { name: received.owner }) }}</span>
        <img v-if="receivedCard?.image" :src="assetUrl(receivedCard.image, 640)" alt="" class="turning" />
        <strong>{{ pick(receivedCard?.name, language) || t('room.player.cardToDraw') }}</strong>
        <small>{{ t('room.player.tapToClose') }}</small>
      </button>
    </Transition>
    <Transition name="call">
      <button v-if="calling" type="button" class="your-turn" @click="calling = null">
        <img v-if="cardOf(calling)?.image" :src="assetUrl(cardOf(calling)?.image, 640)" alt="" aria-hidden="true" />
        <strong>{{ t('room.player.yourTurn', { name: calling }) }}</strong>
        <span>{{ t('room.player.tapToClose') }}</span>
      </button>
    </Transition>
  </div>
</template>

<style scoped>
.journal {
  padding: 0.5rem 0.75rem 1rem;
}
.strip.hurt {
  animation: shake 0.4s;
  background: color-mix(in srgb, #ef4444 30%, var(--p-content-background));
}
.strip.healed {
  background: color-mix(in srgb, #22c55e 30%, var(--p-content-background));
}
.delta {
  font-weight: 700;
}
.strip.hurt .delta {
  color: #ef4444;
}
.strip.healed .delta {
  color: #16a34a;
}
@keyframes shake {
  20%,
  60% {
    transform: translateX(-4px);
  }
  40%,
  80% {
    transform: translateX(4px);
  }
}
.tabs button.alert {
  color: var(--seat);
}
.settings {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.settings label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-height: 3rem;
}
.recap ul {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.others li {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}
.ask {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 15rem;
}
.error {
  color: #ef4444;
}
.received {
  position: fixed;
  inset: 0;
  z-index: 9;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 1.5rem;
  border: 0;
  background: rgb(10 10 10 / 82%);
  color: #fff;
  font: inherit;
  cursor: pointer;
  text-align: center;
}
.received img {
  width: min(70vw, 18rem);
  border-radius: 0.6rem;
  box-shadow: 0 0 2.5rem color-mix(in srgb, var(--seat) 70%, transparent);
}
.received strong {
  font-size: 1.4rem;
}
.turning {
  animation: turn-over 0.7s ease-out;
}
@keyframes turn-over {
  from {
    transform: perspective(800px) rotateY(180deg) scale(0.7);
    opacity: 0;
  }
  to {
    transform: perspective(800px) rotateY(0) scale(1);
    opacity: 1;
  }
}
.your-turn {
  position: fixed;
  inset: 0;
  z-index: 10;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  border: 0;
  background: color-mix(in srgb, var(--seat) 88%, #000);
  color: #fff;
  font: inherit;
  cursor: pointer;
}
.your-turn img {
  width: min(60vw, 16rem);
  border-radius: 0.8rem;
  box-shadow: 0 0.5rem 2rem rgb(0 0 0 / 40%);
}
.your-turn strong {
  font-size: 1.8rem;
  padding: 0 1.5rem;
  text-align: center;
}
.call-enter-active,
.call-leave-active {
  transition:
    opacity 0.25s,
    transform 0.25s;
}
.call-enter-from,
.call-leave-to {
  opacity: 0;
  transform: scale(1.05);
}
@media (prefers-reduced-motion: reduce) {
  .turning {
    animation: none;
  }
  .strip.hurt {
    animation: none;
  }
  .call-enter-from,
  .call-leave-to {
    transform: none;
  }
}
.player {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  max-width: 40rem;
  margin: 0 auto;
}
header {
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: var(--p-content-background);
  border-bottom: 3px solid var(--seat);
}
.switcher {
  display: flex;
  gap: 0.5rem;
  overflow-x: auto;
  flex: 1;
}
.switcher button {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  min-height: 2.75rem;
  padding: 0.25rem 0.6rem 0.25rem 0.25rem;
  border: 2px solid transparent;
  border-radius: 999px;
  background: var(--p-content-hover-background);
  color: inherit;
  font: inherit;
  opacity: 0.7;
}
.switcher button.current {
  border-color: var(--seat);
  opacity: 1;
  font-weight: 600;
}
.switcher button.defeated {
  filter: grayscale(1);
}
.switcher img {
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 50%;
  object-fit: cover;
  /* the art is on the right of the card */
  object-position: 80% 30%;
}
.who {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.85rem;
  white-space: nowrap;
}
.dot {
  width: 0.75rem;
  height: 0.75rem;
  border-radius: 50%;
  background: var(--seat);
}
.strip {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.5rem 1rem;
  font-weight: 600;
}
.health {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex: 1;
}
.health .pi {
  color: #e11d48;
}
.bar {
  flex: 1;
  height: 0.5rem;
  border-radius: 999px;
  background: var(--p-content-hover-background);
  overflow: hidden;
}
.bar span {
  display: block;
  height: 100%;
  background: #e11d48;
  transition: width 0.4s ease;
}
.sp {
  display: flex;
  gap: 0.2rem;
}
.pip {
  width: 0.7rem;
  height: 0.7rem;
  border-radius: 50%;
  border: 2px solid #dc2626;
}
.pip.full {
  background: #dc2626;
}
.banner {
  margin: 0 1rem;
}
main {
  flex: 1;
  padding: 0.75rem 1rem 5.5rem;
}
.waiting {
  text-align: center;
  padding-top: 3rem;
}
.waiting .pi {
  font-size: 2.5rem;
  color: var(--seat);
  animation: pulse 1.6s ease-in-out infinite;
}
.others {
  list-style: none;
  padding: 0;
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.5rem;
}
.others li {
  padding: 0.25rem 0.75rem;
  border-radius: 999px;
  background: var(--p-content-hover-background);
}
.muted {
  color: var(--p-text-muted-color);
}
.tabs {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  justify-content: center;
  background: var(--p-content-background);
  border-top: 1px solid var(--p-content-border-color);
  padding-bottom: env(safe-area-inset-bottom);
}
.tabs button {
  flex: 1;
  max-width: 10rem;
  min-height: 3.5rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.15rem;
  border: 0;
  background: none;
  color: var(--p-text-muted-color);
  font: inherit;
  font-size: 0.8rem;
}
.tabs button[aria-selected='true'] {
  color: var(--seat);
  font-weight: 600;
}
.tabs .pi {
  font-size: 1.25rem;
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}
.flip-enter-active,
.flip-leave-active {
  transition:
    transform 0.25s ease,
    opacity 0.25s ease;
}
.flip-enter-from {
  transform: rotateY(-70deg);
  opacity: 0;
}
.flip-leave-to {
  transform: rotateY(70deg);
  opacity: 0;
}
@keyframes pulse {
  50% {
    opacity: 0.4;
  }
}
@media (prefers-reduced-motion: reduce) {
  .flip-enter-active,
  .flip-leave-active,
  .bar span {
    transition: none;
  }
  .waiting .pi {
    animation: none;
  }
}
</style>
