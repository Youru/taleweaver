<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Message from 'primevue/message'
import { useAdventurers } from '@/features/catalog'
import { GameJournal } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { HubStatus } from '../api/roomHub'
import type { PlayerView } from '../api/types'
import { nextAdventurer, seatColors, turnOf } from '../lib/room'
import EquipmentTab from './EquipmentTab.vue'
import HeroTab from './HeroTab.vue'
import TurnTab from './TurnTab.vue'

/**
 * The phone (room-spec §5): portrait, one column, the adventurer switcher and the live status on top, the tabs
 * at the bottom where the thumb is. A swipe on the content changes adventurer.
 */
const props = defineProps<{ view: PlayerView; followed: string | undefined; status?: HubStatus }>()
const emit = defineEmits<{ follow: [name: string] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const cards = useAdventurers()

type Tab = 'hero' | 'equipment' | 'turn' | 'journal'
const chosen = ref<Tab>('hero')
const fighting = computed(() => !!props.view.encounter && !props.view.encounter.ended)
/** The Tour tab exists only during an encounter: outside one, the hero's sheet shows instead. */
const tab = computed<Tab>({
  get: () => (chosen.value === 'turn' && !fighting.value ? 'hero' : chosen.value),
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

// "Your turn!" (room-spec §5.2): full screen and a vibration when one of the seat's adventurers starts playing.
const turn = computed(() => (props.view.encounter ? turnOf(props.view.encounter, names.value) : {}))
const calling = ref<string | null>(null)
watch(
  () => turn.value.playing,
  (playing, before) => {
    if (!playing || playing === before) return
    calling.value = playing
    if (playing !== current.value?.name) emit('follow', playing)
    if (!props.view.impersonated) navigator.vibrate?.([200, 100, 200])
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
            :src="assetUrl(cardOf(adventurer.name)?.image)"
            alt=""
            aria-hidden="true"
          />
          <span>{{ adventurer.name }}</span>
        </button>
      </div>
      <span class="who">
        <span class="dot" aria-hidden="true" />
        {{ view.impersonated ? t('room.player.gm') : view.seat?.displayName }}
      </span>
    </header>
    <div v-if="current" class="strip" :class="hit" :aria-label="t('room.player.status')">
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
    <Message v-if="status === 'reconnecting'" severity="warn" class="banner">{{
      t('room.player.reconnecting')
    }}</Message>

    <main @touchstart.passive="touchStart" @touchend.passive="touchEnd">
      <div v-if="!current" class="waiting">
        <i class="pi pi-hourglass" aria-hidden="true" />
        <p>{{ t('room.player.waiting') }}</p>
        <p class="muted">{{ pick(view.scenarioTitle, language) }}</p>
        <ul class="others">
          <li v-for="member in view.party" :key="member.name">{{ member.name }}</li>
        </ul>
      </div>
      <Transition v-else name="flip" mode="out-in">
        <TurnTab v-if="tab === 'turn'" key="turn" :view="view" :followed="current.name" />
        <div v-else-if="tab === 'journal'" key="journal" class="journal">
          <GameJournal :log="journal" page />
        </div>
        <HeroTab
          v-else-if="tab === 'hero'"
          :key="`hero-${current.name}`"
          :adventurer="current"
          :card="cardOf(current.name)"
        />
        <EquipmentTab
          v-else
          :key="`equipment-${current.name}`"
          :adventurer="current"
          :gold="view.gold"
          :stash="view.stash"
        />
      </Transition>
    </main>

    <nav class="tabs" role="tablist">
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
      <button type="button" role="tab" :aria-selected="tab === 'journal'" @click="tab = 'journal'">
        <i class="pi pi-book" aria-hidden="true" />
        <span>{{ t('room.player.journalTab') }}</span>
      </button>
    </nav>

    <Transition name="call">
      <button v-if="calling" type="button" class="your-turn" @click="calling = null">
        <img v-if="cardOf(calling)?.image" :src="assetUrl(cardOf(calling)?.image)" alt="" aria-hidden="true" />
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
