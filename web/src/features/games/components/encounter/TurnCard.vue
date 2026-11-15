<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import type { Combatant } from '@/features/catalog'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Answer, EncounterState, LogEntry, Prompt } from '../../api/types'
import { currentTurn } from '../../lib/currentTurn'
import { formatLog } from '../../lib/formatLog'
import { shortId } from '../../lib/ids'
import PromptPanel from '../PromptPanel.vue'

/**
 * The turn being played, always in view: whose turn it is, what the AI said so far (kept until the
 * next turn, to play it out on the table), and the question waiting for the GM.
 */
const props = defineProps<{
  encounter: EncounterState
  log: LogEntry[]
  prompt: Prompt | undefined
  cards: Map<string, Combatant>
  pending: boolean
}>()
const emit = defineEmits<{ answer: [answer: Answer]; duel: [figure: string] }>()

const { t, te } = useI18n()
const { language } = storeToRefs(usePreferences())

const turn = computed(() => currentTurn(props.encounter, props.log))
const actingCard = computed(() => {
  const instance = turn.value.acting
  const figure = props.encounter.combatants.find((c) => c.instance === instance)
  const card = figure ? props.cards.get(figure.card) : undefined
  return card ? pick(card.name, language.value) : ''
})
const heading = computed(() => {
  const value = turn.value
  switch (value.kind) {
    case 'before':
      return t('play.turnBefore')
    case 'adventurer':
      return t('play.turnAdventurer', { name: value.who ?? '' })
    case 'combatants':
      return t('play.turnCombatants', { name: value.acting ?? '', card: actingCard.value })
    case 'idle':
      return t('play.turnIdle', { card: shortId(value.who ?? '') })
  }
  return ''
})
/** The acting combatant was told to attack or cast: the duel helper is one click away. */
const attacks = computed(
  () =>
    !!turn.value.acting &&
    turn.value.entries.some((e) => e.code === 'aiInstruction' && /\bATK\b|SPELL/.test(e.text?.en ?? '')),
)
const lines = computed(() =>
  turn.value.entries.map((entry) =>
    formatLog(
      entry,
      language.value,
      (key, args) => t(key, args),
      (key) => te(key),
    ),
  ),
)
</script>

<template>
  <section class="turn" :aria-label="t('play.turnCard')">
    <p class="heading" :class="turn.kind">{{ heading }}</p>
    <ol v-if="lines.length" class="lines">
      <li v-for="(line, index) in lines" :key="index" :class="line.tone">
        <strong v-if="line.title">{{ line.title }}</strong>
        <span v-if="line.text">{{ line.text }}</span>
      </li>
    </ol>
    <Button
      v-if="attacks && turn.acting"
      class="duel"
      :label="t('duel.openTurn', { name: turn.acting })"
      icon="pi pi-bolt"
      size="small"
      severity="contrast"
      outlined
      @click="emit('duel', turn.acting)"
    />
    <PromptPanel v-if="prompt" :key="log.length" :prompt="prompt" :pending="pending" @answer="emit('answer', $event)" />
  </section>
</template>

<style scoped>
.turn {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-height: 0;
  overflow-y: auto;
}
.heading {
  margin: 0;
  font-weight: 700;
  font-size: 1.05rem;
}
.heading.adventurer {
  color: var(--p-blue-500);
}
.heading.combatants {
  color: var(--p-orange-600);
}
.duel {
  align-self: flex-start;
}
.lines {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.lines li {
  display: flex;
  flex-direction: column;
  padding: 0.3rem 0.6rem;
  border-left: 3px solid var(--p-content-border-color);
  font-size: 0.92rem;
}
.lines li.ai {
  border-color: var(--p-orange-400);
}
.lines li.alert {
  border-color: var(--p-red-500);
}
.lines li.narrative {
  border-color: var(--p-primary-color);
}
.lines li.reward {
  border-color: var(--p-green-500);
}
.lines li span {
  white-space: pre-line;
}
</style>
