<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { EncounterState } from '../../api/types'
import { shortId } from '../../lib/ids'

/** The encounter at a glance: round, Urgency, initiative track, and the button that moves the game on. */
defineProps<{
  encounter: EncounterState
  node: ScenarioNode | undefined
  /** No next turn: a question is pending, a command is running, or the encounter is over. */
  disabled?: boolean
  /** Read-only (the table alone shows it without the button). */
  readonly?: boolean
}>()
const emit = defineEmits<{ nextTurn: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
</script>

<template>
  <section class="bar" :aria-label="pick(node?.name, language)">
    <h2>{{ pick(node?.name, language) }}</h2>
    <span class="round">{{
      encounter.round > 0 ? t('play.round', { round: encounter.round }) : t('play.notStarted')
    }}</span>
    <span
      class="urgency"
      role="img"
      :aria-label="t('play.urgency', { urgency: encounter.urgency })"
      :title="t('play.urgency', { urgency: encounter.urgency })"
    >
      <span v-for="n in 4" :key="n" class="pip" :class="{ on: n <= encounter.urgency, last: n === 4 }" />
    </span>
    <ol v-if="encounter.round > 0" class="track" :aria-label="t('play.initiative')">
      <li
        v-for="(card, index) in encounter.initiativeTrack"
        :key="card"
        :class="{
          current: index === encounter.currentTurn,
          done: index < encounter.currentTurn,
          adventurer: card.startsWith('adventurer:'),
        }"
        :aria-current="index === encounter.currentTurn ? 'step' : undefined"
      >
        {{ shortId(card) }}
      </li>
    </ol>
    <Button
      v-if="!readonly"
      class="next"
      :label="encounter.round > 0 ? t('play.nextTurn') : t('play.startRound')"
      icon="pi pi-step-forward"
      :disabled="disabled || encounter.ended"
      @click="emit('nextTurn')"
    />
  </section>
</template>

<style scoped>
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.9rem;
}
h2 {
  margin: 0;
  font-size: 1.2rem;
}
.round {
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.urgency {
  display: inline-flex;
  gap: 0.25rem;
}
.pip {
  width: 0.8rem;
  height: 0.8rem;
  border-radius: 50%;
  border: 2px solid var(--p-orange-400);
}
.pip.on {
  background: var(--p-orange-500);
}
.pip.last {
  border-color: var(--p-red-500);
}
.pip.last.on {
  background: var(--p-red-600);
}
.track {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  list-style: none;
  padding: 0;
  margin: 0;
  flex: 1;
}
.track li {
  padding: 0.1rem 0.5rem;
  border-radius: 1rem;
  border: 1px solid var(--p-content-border-color);
  font-size: 0.8rem;
}
.track li.adventurer {
  border-color: var(--p-blue-400);
}
.track .done {
  opacity: 0.45;
}
.track .current {
  background: var(--p-primary-color);
  border-color: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
  font-weight: 600;
}
.next {
  margin-left: auto;
}
</style>
