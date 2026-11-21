<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useInitiativeCards } from '@/features/catalog'
import { assetUrl } from '@/shared/api/assets'
import type { EncounterState } from '../../api/types'
import { initiativeTrack } from '../../lib/initiativeTrack'

/**
 * The round's initiative track as the players deal it (rulebook p. 36): the cards from the front to the
 * back, the one playing raised, those that played greyed, and on a group's card its figures in
 * activation order. Shuffled again at the start of every round.
 */
const props = defineProps<{ encounter: EncounterState; large?: boolean }>()

const { t } = useI18n()
const catalog = useInitiativeCards()
const track = computed(() => initiativeTrack(props.encounter, catalog.data.value ?? []))

function describe(card: (typeof track.value)[number]) {
  const text = t('play.trackCard', { order: card.order, label: card.label, state: t(`play.trackState.${card.state}`) })
  const numbers = card.figures.map((f) => f.number).join(', ')
  return numbers ? `${text} (${t('play.trackFigures', { numbers })})` : text
}

// The card playing stays in view when the track is longer than the screen
const list = ref<HTMLElement | null>(null)
watch(
  () => [props.encounter.round, props.encounter.currentTurn],
  async () => {
    await nextTick()
    list.value?.querySelector<HTMLElement>('.current')?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
  },
)
</script>

<template>
  <div class="initiative" :class="{ large }">
    <span class="end" aria-hidden="true">{{ t('play.trackFront') }}</span>
    <ol ref="list" :aria-label="t('play.initiative')">
      <li
        v-for="card in track"
        :key="card.id"
        :class="[card.side, card.state]"
        :aria-current="card.state === 'current' ? 'step' : undefined"
        :aria-label="describe(card)"
        :title="describe(card)"
      >
        <div class="card">
          <img v-if="card.image" :src="assetUrl(card.image)" alt="" loading="lazy" />
          <span v-else class="blank">{{ card.label }}</span>
          <span class="order" aria-hidden="true">{{ card.order }}</span>
        </div>
        <ul v-if="card.figures.length" class="figures" aria-hidden="true">
          <li v-for="figure in card.figures" :key="figure.instance" :class="figure.state">{{ figure.number }}</li>
        </ul>
        <span v-if="card.state === 'current' || card.state === 'next'" class="tag" aria-hidden="true">
          {{ t(`play.trackState.${card.state}`) }}
        </span>
      </li>
    </ol>
    <span class="end" aria-hidden="true">{{ t('play.trackBack') }}</span>
  </div>
</template>

<style scoped>
.initiative {
  --card-w: 3.6rem;
  display: flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
}
.initiative.large {
  --card-w: 5.4rem;
}
.end {
  writing-mode: vertical-rl;
  transform: rotate(180deg);
  font-size: 0.65rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}
ol {
  display: flex;
  gap: 0.45rem;
  list-style: none;
  margin: 0;
  padding: 0.35rem 0.2rem 0.2rem;
  overflow-x: auto;
  min-width: 0;
}
ol > li {
  flex: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.2rem;
  width: var(--card-w);
  transition:
    transform 0.2s,
    opacity 0.2s;
}
.card {
  position: relative;
  width: 100%;
  aspect-ratio: 496 / 727;
  border-radius: 0.35rem;
  overflow: hidden;
  border: 2px solid #1d1712;
  background: #1d1712;
  box-shadow: 0 1px 3px rgb(0 0 0 / 0.35);
}
.opponent .card {
  border-color: #8f1515;
  background: #8f1515;
}
.card img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.blank {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 0.2rem;
  color: #f3e9d8;
  font-size: 0.7rem;
  font-weight: 600;
  text-align: center;
  overflow-wrap: anywhere;
}
.order {
  position: absolute;
  top: 0.15rem;
  left: 0.15rem;
  min-width: 1.1rem;
  height: 1.1rem;
  padding: 0 0.2rem;
  border-radius: 0.55rem;
  background: rgb(0 0 0 / 0.7);
  color: #fff;
  font-size: 0.7rem;
  font-weight: 700;
  line-height: 1.1rem;
  text-align: center;
}
.large .order {
  min-width: 1.4rem;
  height: 1.4rem;
  line-height: 1.4rem;
  font-size: 0.85rem;
}
.played {
  opacity: 0.45;
}
.played .card {
  filter: grayscale(0.85);
}
.current {
  transform: translateY(-0.25rem);
}
.current .card {
  outline: 3px solid var(--p-primary-color);
  outline-offset: 1px;
  box-shadow: 0 4px 12px rgb(0 0 0 / 0.45);
}
.current .order {
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
}
.tag {
  font-size: 0.7rem;
  font-weight: 600;
  line-height: 1;
  color: var(--p-text-muted-color);
}
.current .tag {
  color: var(--p-primary-color);
}
.large .tag {
  font-size: 0.85rem;
}
.figures {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.15rem;
  list-style: none;
  margin: 0;
  padding: 0;
}
.figures li {
  min-width: 1rem;
  height: 1rem;
  border-radius: 50%;
  border: 1px solid var(--p-content-border-color);
  font-size: 0.65rem;
  line-height: 0.9rem;
  text-align: center;
}
.large .figures li {
  min-width: 1.3rem;
  height: 1.3rem;
  font-size: 0.8rem;
  line-height: 1.2rem;
}
.figures .played {
  opacity: 0.45;
}
.figures .acting {
  background: var(--p-primary-color);
  border-color: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
  font-weight: 700;
}
</style>
