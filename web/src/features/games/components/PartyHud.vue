<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Popover from 'primevue/popover'
import { figureArt, type BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import type { AdventurerState } from '../api/types'

/** The party at a glance: portrait and Damage bar. The rest is one touch away. */
const props = defineProps<{
  party: AdventurerState[]
  pieces?: BoardPieces
  gold: number
  /** Whom the game waits for, in words, and the adventurer it is (none: the GM). */
  waiting: string | null
  waitedName?: string | null
  /** Between encounters the XP matters: shown without a touch. */
  showXp?: boolean
}>()

const { t } = useI18n()
const details = new Map<string, { toggle: (event: Event) => void }>()

function art(name: string) {
  const { image, portrait } = figureArt(props.pieces, name.toLowerCase())
  return image ? { url: assetUrl(image), portrait } : undefined
}
function share(adventurer: AdventurerState) {
  return adventurer.health ? Math.min(1, (adventurer.damage ?? 0) / adventurer.health) : 0
}
function health(adventurer: AdventurerState) {
  return adventurer.health ? `${adventurer.damage ?? 0}/${adventurer.health}` : ''
}
function conditions(adventurer: AdventurerState) {
  return [
    adventurer.defeated && t('play.adventurerDefeatedTag'),
    adventurer.injured && t('storyRound.injured'),
    adventurer.unselectable && t('storyRound.unselectable'),
  ].filter((text): text is string => !!text)
}
function describe(adventurer: AdventurerState) {
  const damage = adventurer.health ? `${health(adventurer)} ${t('play.health')}` : ''
  const parts = [adventurer.name, damage, t('play.xp', { xp: adventurer.xp }), ...conditions(adventurer)]
  return parts.filter(Boolean).join(' · ')
}
</script>

<template>
  <div class="hud">
    <ul :aria-label="t('play.party')">
      <li v-for="adventurer in party" :key="adventurer.name">
        <button
          type="button"
          class="member"
          :class="{ out: conditions(adventurer).length, waited: adventurer.name === waitedName }"
          :aria-label="describe(adventurer)"
          :title="describe(adventurer)"
          aria-haspopup="dialog"
          @click="details.get(adventurer.name)?.toggle($event)"
        >
          <span class="face" :class="{ standee: art(adventurer.name)?.portrait === false }">
            <img v-if="art(adventurer.name)" :src="art(adventurer.name)!.url" alt="" />
            <template v-else>{{ adventurer.name.charAt(0) }}</template>
          </span>
          <span class="text">
            <span class="name">{{ adventurer.name }}</span>
            <span class="figures">
              <i v-if="conditions(adventurer).length" class="pi pi-ban" aria-hidden="true" />
              {{ health(adventurer) }}
              <span v-if="showXp" class="xp">{{ t('play.xp', { xp: adventurer.xp }) }}</span>
            </span>
            <span v-if="adventurer.health" class="damage">
              <span :class="{ high: share(adventurer) >= 0.75 }" :style="{ width: `${share(adventurer) * 100}%` }" />
            </span>
          </span>
        </button>
        <Popover :ref="(el) => el && details.set(adventurer.name, el as never)">
          <dl class="detail">
            <dt>{{ adventurer.name }}</dt>
            <dd v-if="adventurer.health">{{ health(adventurer) }} {{ t('play.health') }}</dd>
            <dd>{{ t('play.xp', { xp: adventurer.xp }) }}</dd>
            <dd v-for="condition in conditions(adventurer)" :key="condition">{{ condition }}</dd>
          </dl>
        </Popover>
      </li>
    </ul>
    <span class="gold"><i class="pi pi-circle-fill" aria-hidden="true" />{{ t('play.gold', { gold }) }}</span>
    <span
      v-if="waiting"
      class="waiting"
      role="status"
      :aria-label="t('comfort.waiting', { who: waiting })"
      :title="t('comfort.waiting', { who: waiting })"
    >
      <i class="pi pi-hourglass" aria-hidden="true" />{{ waiting }}
    </span>
  </div>
</template>

<style scoped>
.hud,
ul,
.gold,
.waiting {
  display: flex;
  align-items: center;
}
.hud {
  flex-wrap: wrap;
  gap: 0.35rem 1rem;
  min-width: 0;
}
ul {
  flex-wrap: wrap;
  gap: 0.35rem;
  margin: 0;
  padding: 0;
  list-style: none;
}
.member {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.15rem 0.6rem 0.15rem 0.15rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 2rem;
  background: var(--p-content-background);
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.member.waited {
  border-color: var(--p-primary-color);
  box-shadow: 0 0 0 1px var(--p-primary-color);
}
.member:focus-visible {
  outline: 2px solid var(--p-primary-color);
  outline-offset: 2px;
}
.out .face,
.out .name {
  opacity: 0.5;
  filter: grayscale(1);
}
.face {
  display: grid;
  place-items: center;
  flex: none;
  width: 2.3rem;
  height: 2.3rem;
  border-radius: 50%;
  overflow: hidden;
  background: var(--p-surface-300);
  font-weight: 700;
}
.face img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
/* Only the left part of a standee fits a disc */
.face.standee img {
  object-position: left top;
}
.text {
  display: grid;
  gap: 0.15rem;
  min-width: 3.2rem;
  text-align: left;
  font-size: 0.75rem;
  line-height: 1;
}
.name {
  font-weight: 700;
}
.figures {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}
.xp {
  margin-left: 0.3rem;
  color: var(--p-text-muted-color);
}
.damage {
  height: 0.25rem;
  border-radius: 1rem;
  overflow: hidden;
  background: var(--p-surface-300);
}
.damage > span {
  display: block;
  height: 100%;
  background: var(--p-orange-500);
}
.damage > .high {
  background: var(--p-red-600);
}
.gold,
.waiting {
  gap: 0.3rem;
  font-size: 0.9rem;
  font-weight: 600;
  white-space: nowrap;
}
.gold i {
  color: #b8860b;
}
.waiting {
  color: var(--p-text-muted-color);
}
.detail,
.detail dd {
  margin: 0;
}
.detail dt {
  font-weight: 700;
}
/* Room for the party and the main button on one line */
@media (max-width: 1500px) {
  .name {
    display: none;
  }
}
</style>
