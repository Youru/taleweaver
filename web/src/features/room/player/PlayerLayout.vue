<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Message from 'primevue/message'
import { useAdventurers } from '@/features/catalog'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { HubStatus } from '../api/roomHub'
import type { PlayerView } from '../api/types'
import { nextAdventurer, seatColors } from '../lib/room'
import EquipmentTab from './EquipmentTab.vue'
import HeroTab from './HeroTab.vue'

/**
 * The phone (room-spec §5): portrait, one column, the adventurer switcher and the live status on top, the tabs
 * at the bottom where the thumb is. A swipe on the content changes adventurer.
 */
const props = defineProps<{ view: PlayerView; followed: string | undefined; status?: HubStatus }>()
const emit = defineEmits<{ follow: [name: string] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const cards = useAdventurers()

const tab = ref<'hero' | 'equipment'>('hero')
const names = computed(() => props.view.adventurers.map((a) => a.name))
const current = computed(
  () => props.view.adventurers.find((a) => a.name === props.followed) ?? props.view.adventurers[0],
)
const cardOf = (name: string) => cards.data.value?.find((c) => pick(c.name, 'en') === name || c.id === name)
const color = computed(() => seatColors[props.view.seat?.color ?? ''] ?? 'var(--p-primary-color)')
const healthLeft = computed(() => (current.value?.health ?? 0) - (current.value?.damage ?? 0))
const healthRatio = computed(() => (current.value?.health ? Math.max(0, healthLeft.value) / current.value.health : 0))

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
    <div v-if="current" class="strip" :aria-label="t('room.player.status')">
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
        <HeroTab
          v-if="tab === 'hero'"
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
    </nav>
  </div>
</template>

<style scoped>
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
