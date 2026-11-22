<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import type { BoardPieces } from '@/features/table'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftAction, DraftNode, DraftScenario } from '../api/types'
import { activatableKeys, CARD_SIZES, hiddenCardCondition, TRIGGERS, triggerTokens, type DraftEvent, type DraftTrigger } from '../lib/events'
import { hiddenRulesOf } from '../lib/hiddenRules'
import { mapOf } from '../lib/map'
import ActionList from './ActionList.vue'
import JsonField from './JsonField.vue'
import TextField from './TextField.vue'

/**
 * One event of the encounter: when it fires (trigger), whether once only, the hidden text the GM reads
 * aloud, and what happens (actions). Conditions are kept, edited as JSON.
 */
const props = defineProps<{
  event: DraftEvent
  scenario: DraftScenario
  node: DraftNode
  pieces: BoardPieces | undefined
  /** Set off by a token: the trigger is that of the token, not chosen here. */
  ofToken?: boolean
}>()
const emit = defineEmits<{ change: [event: DraftEvent]; remove: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

function patch(fields: Partial<DraftEvent>) {
  const next: Record<string, unknown> = { ...props.event, ...fields }
  for (const [k, v] of Object.entries(fields)) if (v === undefined) delete next[k]
  emit('change', next as DraftEvent)
}
function patchTrigger(fields: Partial<DraftTrigger>) {
  const next: Record<string, unknown> = { ...props.event.trigger, ...fields }
  for (const [k, v] of Object.entries(fields)) if (v === undefined || v === null || v === '') delete next[k]
  patch({ trigger: next as DraftTrigger })
}

const cardSize = ref<string>('medium')
const cardCode = ref('')
const sizeOptions = computed(() => CARD_SIZES.map((value) => ({ value, label: t(`workshop.events.size.${value}`) })))
function requireCard() {
  patch({ conditions: [...(props.event.conditions ?? []), hiddenCardCondition(cardSize.value, cardCode.value)] })
  cardCode.value = ''
}

const triggerOptions = computed(() =>
  Object.keys(TRIGGERS).map((value) => ({ value, label: t(`workshop.events.on.${value}`) })),
)
const parameter = computed(() => TRIGGERS[props.event.trigger.on] ?? null)
const keyOptions = computed(() =>
  activatableKeys(props.node, (type) => props.pieces?.tokens[type]?.category).map((value) => ({ value, label: value })),
)
const familyOptions = computed(() =>
  Object.entries(props.pieces?.families ?? {})
    .map(([value, p]) => ({ value, label: pick(p.name, language.value) || value }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
const areaOptions = computed(() =>
  mapOf(props.node).areas.map((a) => ({ value: a.id, label: pick(a.name, language.value) || a.id })),
)
const ruleOptions = computed(() =>
  hiddenRulesOf(props.node).map((r) => ({ value: r.id, label: pick(r.name, language.value) || r.id })),
)
const actions = computed({
  get: () => props.event.actions ?? [],
  set: (value: DraftAction[]) => patch({ actions: value }),
})
</script>

<template>
  <article class="event" :aria-label="t('workshop.events.event', { id: event.id })">
    <header>
      <code>{{ event.id }}</code>
      <span class="spacer" />
      <label class="inline">
        <ToggleSwitch :model-value="event.once === true" @update:model-value="(v) => patch({ once: v || undefined })" />
        {{ t('workshop.events.once') }}
      </label>
      <Button
        icon="pi pi-trash"
        size="small"
        text
        severity="danger"
        :aria-label="t('workshop.events.remove', { id: event.id })"
        @click="emit('remove')"
      />
    </header>

    <div v-if="!ofToken" class="row">
      <Select
        :model-value="event.trigger.on"
        :options="triggerOptions"
        option-label="label"
        option-value="value"
        :aria-label="t('workshop.events.trigger')"
        @update:model-value="(v) => patch({ trigger: { on: v } })"
      />
      <MultiSelect
        v-if="parameter === 'token'"
        :model-value="triggerTokens(event.trigger)"
        :options="keyOptions"
        option-label="label"
        option-value="value"
        :placeholder="t('workshop.events.pickToken')"
        :aria-label="t('workshop.events.triggerTokens')"
        @update:model-value="(v: string[]) => patchTrigger({ token: v.length === 1 ? v[0] : v })"
      />
      <InputNumber
        v-else-if="parameter === 'round'"
        :model-value="event.trigger.round"
        :min="1"
        show-buttons
        size="small"
        input-class="num"
        :placeholder="t('workshop.events.everyRound')"
        :aria-label="t('workshop.events.round')"
        @update:model-value="(v) => patchTrigger({ round: v ?? undefined })"
      />
      <Select
        v-else-if="parameter === 'family'"
        :model-value="event.trigger.family"
        :options="familyOptions"
        option-label="label"
        option-value="value"
        show-clear
        filter
        :placeholder="t('workshop.events.anyFamily')"
        :aria-label="t('workshop.map.family')"
        @update:model-value="(v) => patchTrigger({ family: v ?? undefined })"
      />
      <Select
        v-else-if="parameter === 'area'"
        :model-value="event.trigger.area"
        :options="areaOptions"
        option-label="label"
        option-value="value"
        show-clear
        :placeholder="t('workshop.events.anyArea')"
        :aria-label="t('workshop.events.area')"
        @update:model-value="(v) => patchTrigger({ area: v ?? undefined })"
      />
      <Select
        v-else-if="parameter === 'rule'"
        :model-value="event.trigger.rule"
        :options="ruleOptions"
        option-label="label"
        option-value="value"
        show-clear
        :placeholder="t('workshop.events.anyRule')"
        :aria-label="t('workshop.events.rule')"
        @update:model-value="(v) => patchTrigger({ rule: v ?? undefined })"
      />
    </div>

    <TextField
      :model-value="event.hiddenText"
      :label="t('workshop.events.hiddenText')"
      long
      :rows="3"
      @update:model-value="(v) => patch({ hiddenText: v })"
    />
    <ActionList
      v-model="actions"
      :scenario="scenario"
      :node="node"
      :pieces="pieces"
      :label="t('workshop.events.actionsOf', { id: event.id })"
    />
    <JsonField
      v-if="event.conditions?.length"
      :model-value="event.conditions"
      :label="t('workshop.events.conditions')"
      :rows="2"
      @update:model-value="(v) => patch({ conditions: Array.isArray(v) && v.length ? v : undefined })"
    />
    <details>
      <summary>{{ t('workshop.events.onlyIfRevealed') }}</summary>
      <div class="row">
        <Select
          v-model="cardSize"
          :options="sizeOptions"
          option-label="label"
          option-value="value"
          :aria-label="t('workshop.events.cardSize')"
        />
        <InputText
          v-model="cardCode"
          size="small"
          :aria-label="t('workshop.events.cardCode')"
          :placeholder="t('workshop.events.cardCode')"
        />
        <Button :label="t('workshop.events.addCondition')" size="small" text :disabled="!cardCode.trim()" @click="requireCard" />
      </div>
    </details>
  </article>
</template>

<style scoped>
.event {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.6rem 0.7rem;
  border: 1px solid var(--p-content-border-color);
  border-left: 4px solid #7c3aed;
  border-radius: var(--p-content-border-radius);
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
.row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}
.row :deep(.num) {
  width: 4rem;
}
.inline {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.8rem;
}
</style>
