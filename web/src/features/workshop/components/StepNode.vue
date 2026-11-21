<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Handle, Position } from '@vue-flow/core'
import type { Exit } from '../lib/graph'

/** A scene or an encounter on the graph: where it comes in (left), and one handle per exit (right). */
defineProps<{
  data: {
    type: string
    caption: string
    start: boolean
    issues: number
    exits: (Exit & { text: string })[]
  }
  selected?: boolean
}>()

const { t } = useI18n()
</script>

<template>
  <div class="step" :class="[data.type, { selected, faulty: data.issues > 0 }]">
    <Handle type="target" :position="Position.Left" class="in" />
    <header>
      <i :class="data.type === 'encounter' ? 'pi pi-bolt' : data.type === 'storyRound' ? 'pi pi-shop' : 'pi pi-book'" aria-hidden="true" />
      <span class="kind">{{
        data.type === 'encounter' ? t('workshop.encounter') : data.type === 'storyRound' ? t('workshop.storyRound') : t('workshop.scene')
      }}</span>
      <span v-if="data.start" class="badge start">{{ t('workshop.startBadge') }}</span>
      <span v-if="data.issues" class="badge issues" :title="t('workshop.issues', data.issues)">
        <i class="pi pi-exclamation-triangle" /> {{ data.issues }}
      </span>
    </header>
    <strong class="caption">{{ data.caption }}</strong>
    <ul class="exits">
      <li v-for="exit in data.exits" :key="exit.key" :class="exit.kind">
        <span class="label">{{ exit.text }}</span>
        <Handle :id="exit.key" type="source" :position="Position.Right" class="out" />
      </li>
    </ul>
  </div>
</template>

<style scoped>
.step {
  width: 15rem;
  padding: 0.55rem 0.7rem 0.45rem;
  border-radius: 10px;
  border: 2px solid var(--p-content-border-color);
  background: var(--p-content-background);
  color: var(--p-text-color);
  box-shadow: 0 2px 8px rgb(0 0 0 / 0.12);
  font-size: 0.8rem;
}
.step.encounter {
  border-color: #b45309;
}
.step.scene {
  border-color: #6d28d9;
}
.step.selected {
  box-shadow:
    0 0 0 3px var(--p-primary-color),
    0 2px 8px rgb(0 0 0 / 0.2);
}
.step.faulty {
  border-style: dashed;
}
header {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: 0.68rem;
}
.encounter header i {
  color: #b45309;
}
.scene header i {
  color: #6d28d9;
}
.badge {
  border-radius: 1rem;
  padding: 0 0.4rem;
  font-weight: 700;
  text-transform: none;
  letter-spacing: 0;
}
.badge.start {
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
}
.badge.issues {
  margin-left: auto;
  background: #fee2e2;
  color: #b91c1c;
}
.caption {
  display: block;
  margin: 0.2rem 0 0.35rem;
  font-size: 0.95rem;
  line-height: 1.2;
}
.exits {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}
.exits li {
  position: relative;
  padding: 0.15rem 0.5rem 0.15rem 0.45rem;
  border-radius: 6px;
  background: var(--p-content-hover-background, rgb(0 0 0 / 0.04));
  border-left: 3px solid #a3a3a3;
}
.exits li.win {
  border-left-color: #16a34a;
}
.exits li.lose {
  border-left-color: #dc2626;
}
.exits li.option {
  border-left-color: #6d28d9;
}
.label {
  display: block;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  padding-right: 0.4rem;
}
.out,
.in {
  width: 12px;
  height: 12px;
  background: var(--p-primary-color);
  border: 2px solid var(--p-content-background);
}
.out {
  right: -15px;
}
</style>
