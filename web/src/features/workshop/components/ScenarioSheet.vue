<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { usePreferences } from '@/shared/lib/preferences'
import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftScenario, ScenarioIssue } from '../api/types'
import { captionOf } from '../lib/graph'
import TextField from './TextField.vue'

/** The scenario itself, when no node is selected: its title, its steps, what is left to fix. */
const props = defineProps<{ scenario: DraftScenario; issues: ScenarioIssue[] }>()
const emit = defineEmits<{ change: [scenario: DraftScenario]; select: [id: string] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const title = computed({
  get: () => props.scenario.title,
  set: (value: LocalizedText | undefined) => emit('change', { ...props.scenario, title: value ?? {} }),
})
</script>

<template>
  <section class="sheet" :aria-label="t('workshop.scenarioSheet')">
    <TextField v-model="title" :label="t('workshop.scenarioTitle')" />
    <p class="hint">{{ t('workshop.howTo') }}</p>
    <Message v-for="(issue, i) in issues" :key="i" severity="error" size="small">
      {{ issue.where }} : {{ issue.message }}
    </Message>
    <h3>{{ t('workshop.steps') }}</h3>
    <ul class="steps">
      <li v-for="node in scenario.nodes" :key="node.id">
        <Button
          :label="captionOf(node, language)"
          :icon="node.type === 'encounter' ? 'pi pi-bolt' : 'pi pi-book'"
          size="small"
          text
          @click="emit('select', node.id)"
        />
        <span v-if="node.id === scenario.start" class="start">{{ t('workshop.isStart') }}</span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.sheet {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.hint {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
h3 {
  margin: 0.5rem 0 0;
  font-size: 1rem;
}
.steps {
  list-style: none;
  margin: 0;
  padding: 0;
}
.steps li {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}
.start {
  font-size: 0.75rem;
  color: var(--p-primary-color);
  font-weight: 600;
}
</style>
