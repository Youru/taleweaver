<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import type { Cell } from '@/features/games'
import type { Advice, Candidate } from '../lib/instructions'

/** What the board suggests for one AI instruction: whom to target, where to move. */
const props = defineProps<{
  advice: Advice[]
  /** The acting figure: its name and where it stands now. */
  name: string
  position: Cell | undefined
}>()
const emit = defineEmits<{ duel: [target: string, spell: boolean]; move: [to: Cell] }>()

const { t } = useI18n()

const list = (candidates: Candidate[]) => candidates.map((c) => `${c.name} (${c.range})`).join(', ')
const at = (a: Cell, b: Cell | undefined) => !!b && a.x === b.x && a.y === b.y
/** The buttons of an Attack: its target, or every candidate when the GM settles it (at most four). */
function choices(a: Extract<Advice, { kind: 'attack' }>): Candidate[] {
  return (a.target ? [a.target] : a.candidates).slice(0, 4)
}
</script>

<template>
  <ul class="advice" :aria-label="t('ai.advice')">
    <li v-for="(a, index) in props.advice" :key="index">
      <template v-if="a.kind === 'attack'">
        <i class="pi" :class="a.spell ? 'pi-sparkles' : 'pi-bolt'" aria-hidden="true" />
        <span v-if="!a.candidates.length">{{ t('ai.noTarget', { range: a.range }) }}</span>
        <span v-else-if="a.target">{{ t('ai.target', { name: a.target.name, n: a.target.range }) }}</span>
        <span v-else-if="a.criterion">
          {{ t('ai.targetCriterion', { criterion: a.criterion, list: list(a.candidates) }) }}
        </span>
        <span v-else>{{ t('ai.targetsEach', { list: list(a.candidates) }) }}</span>
        <span v-if="a.candidates.length" class="buttons">
          <Button
            v-for="c in choices(a)"
            :key="c.id"
            :label="t('ai.resolve', { name: c.name })"
            size="small"
            text
            @click="emit('duel', c.id, a.spell)"
          />
        </span>
      </template>
      <template v-else-if="a.kind === 'move'">
        <i class="pi pi-directions" aria-hidden="true" />
        <span>
          {{ a.toward ? t('ai.moveToward', { n: a.cost, name: a.toward }) : t('ai.move', { n: a.cost }) }}
          <em v-if="a.criterion">· {{ t('ai.moveCriterion') }}</em>
        </span>
        <span class="buttons">
          <span v-if="at(a.to, position)" class="done"><i class="pi pi-check" /> {{ t('ai.moved') }}</span>
          <Button v-else :label="t('ai.applyMove', { name })" size="small" text @click="emit('move', a.to)" />
        </span>
      </template>
      <template v-else>
        <i class="pi pi-stop-circle" aria-hidden="true" />
        <span>{{ t('ai.stay') }}</span>
      </template>
    </li>
  </ul>
</template>

<style scoped>
.advice {
  list-style: none;
  margin: 0.25rem 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.advice li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.1rem 0.4rem;
}
.buttons {
  display: inline-flex;
  flex-wrap: wrap;
}
.buttons :deep(.p-button) {
  padding: 0.1rem 0.4rem;
}
.done {
  color: var(--p-green-600);
}
</style>
