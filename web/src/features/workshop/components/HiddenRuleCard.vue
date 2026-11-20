<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Select from 'primevue/select'
import type { BoardPieces } from '@/features/table'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftAction, DraftNode, DraftScenario } from '../api/types'
import { hiddenRulesOf, type DraftHiddenRule } from '../lib/hiddenRules'
import ActionList from './ActionList.vue'
import TextField from './TextField.vue'

/**
 * One Curiously Hidden Rule: its name and text (printed under the red filter), the rule it is a part of,
 * and what happens when the GM reads it.
 */
const props = defineProps<{
  rule: DraftHiddenRule
  scenario: DraftScenario
  node: DraftNode
  pieces: BoardPieces | undefined
}>()
const emit = defineEmits<{ change: [fields: Partial<DraftHiddenRule>]; remove: []; addPart: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const parents = computed(() =>
  hiddenRulesOf(props.node)
    .filter((r) => r.id !== props.rule.id && r.partOf !== props.rule.id)
    .map((r) => ({ value: r.id, label: pick(r.name, language.value) || r.id })),
)
const actions = computed({
  get: () => props.rule.actions ?? [],
  set: (value: DraftAction[]) => emit('change', { actions: value }),
})
</script>

<template>
  <article class="rule" :class="{ part: rule.partOf }" :aria-label="t('workshop.hiddenRules.rule', { id: rule.id })">
    <header>
      <code>{{ rule.id }}</code>
      <span class="spacer" />
      <Button
        v-if="!rule.partOf"
        :label="t('workshop.hiddenRules.addPart')"
        icon="pi pi-plus"
        size="small"
        text
        @click="emit('addPart')"
      />
      <Button
        icon="pi pi-trash"
        size="small"
        text
        severity="danger"
        :aria-label="t('workshop.hiddenRules.remove', { id: rule.id })"
        @click="emit('remove')"
      />
    </header>
    <TextField
      :model-value="rule.name"
      :label="t('workshop.ruleName')"
      @update:model-value="(v) => emit('change', { name: v })"
    />
    <div v-if="parents.length" class="field">
      <label :for="`part-of-${rule.id}`">{{ t('workshop.hiddenRules.partOf') }}</label>
      <Select
        :input-id="`part-of-${rule.id}`"
        :model-value="rule.partOf"
        :options="parents"
        option-label="label"
        option-value="value"
        show-clear
        :placeholder="t('workshop.hiddenRules.ownRule')"
        @update:model-value="(v) => emit('change', { partOf: v ?? undefined })"
      />
    </div>
    <TextField
      :model-value="rule.text"
      :label="t('workshop.ruleText')"
      long
      :rows="3"
      @update:model-value="(v) => emit('change', { text: v })"
    />
    <ActionList
      v-model="actions"
      :scenario="scenario"
      :node="node"
      :pieces="pieces"
      :label="t('workshop.hiddenRules.actionsOf', { id: rule.id })"
    />
  </article>
</template>

<style scoped>
.rule {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.6rem 0.7rem;
  border: 1px solid var(--p-content-border-color);
  border-left: 4px solid #b91c1c;
  border-radius: var(--p-content-border-radius);
}
.rule.part {
  margin-left: 1.25rem;
}
header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
code {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
}
.spacer {
  flex: 1;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.field label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
</style>
