<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Select from 'primevue/select'
import type { BoardPieces } from '@/features/table'
import type { DraftNode, DraftScenario } from '../api/types'
import {
  activatableKeys,
  addEvent,
  addTokenEvent,
  eventsOf,
  eventsOfToken,
  removeEvent,
  setEvent,
  setTokenRule,
  TOKEN_MODES,
  TRIGGERS,
  tokenRulesOf,
  type DraftTokenRule,
} from '../lib/events'
import EventCard from './EventCard.vue'
import TextField from './TextField.vue'

/**
 * The tokens of an encounter (how each is activated, what it reveals) or its other events (round,
 * defeat, area revealed…), as the engine plays them.
 */
const props = defineProps<{
  scenario: DraftScenario
  node: DraftNode
  pieces: BoardPieces | undefined
  show: 'tokens' | 'events'
}>()
const emit = defineEmits<{ change: [node: DraftNode] }>()

const { t } = useI18n()

const keys = computed(() => activatableKeys(props.node, (type) => props.pieces?.tokens[type]?.category))
const rules = computed(() => tokenRulesOf(props.node))
const modeOptions = computed(() => {
  const used = Object.values(rules.value)
    .map((r) => r.mode)
    .filter((m): m is string => !!m)
  return [...new Set([...TOKEN_MODES, ...used])].map((value) => ({
    value,
    label: (TOKEN_MODES as readonly string[]).includes(value) ? t(`workshop.events.mode.${value}`) : value,
  }))
})
function patchRule(key: string, fields: Partial<DraftTokenRule>) {
  const next: Record<string, unknown> = { ...rules.value[key], ...fields }
  for (const [k, v] of Object.entries(fields)) if (v === undefined) delete next[k]
  emit('change', setTokenRule(props.node, key, next as DraftTokenRule))
}

/** The events no token sets off. */
const others = computed(() => {
  const ofTokens = new Set(keys.value.flatMap((k) => eventsOfToken(props.node, k).map((e) => e.id)))
  return eventsOf(props.node).filter((e) => !ofTokens.has(e.id))
})
const newTrigger = computed(() =>
  Object.keys(TRIGGERS)
    .filter((on) => on !== 'tokenActivated')
    .map((value) => ({ value, label: t(`workshop.events.on.${value}`) })),
)
</script>

<template>
  <div class="rules">
    <template v-if="show === 'tokens'">
      <p class="help">{{ t('workshop.events.tokensHelp') }}</p>
      <p v-if="!keys.length" class="help">{{ t('workshop.events.noToken') }}</p>
      <section v-for="key in keys" :key="key" class="token" :aria-label="t('workshop.events.tokenRule', { key })">
        <h4>{{ key }}</h4>
        <div class="field">
          <span class="label">{{ t('workshop.events.modeLabel') }}</span>
          <Select
            :aria-label="t('workshop.events.modeLabel')"
            :model-value="rules[key]?.mode"
            :options="modeOptions"
            option-label="label"
            option-value="value"
            show-clear
            :placeholder="t('workshop.events.noMode')"
            @update:model-value="(v) => patchRule(key, { mode: v ?? undefined })"
          />
        </div>
        <TextField
          :model-value="rules[key]?.activation"
          :label="t('workshop.events.activation')"
          @update:model-value="(v) => patchRule(key, { activation: v })"
        />
        <EventCard
          v-for="event in eventsOfToken(node, key)"
          :key="event.id"
          :event="event"
          :scenario="scenario"
          :node="node"
          :pieces="pieces"
          of-token
          @change="(next) => emit('change', setEvent(node, event.id, next))"
          @remove="emit('change', removeEvent(node, event.id))"
        />
        <Button
          :label="t('workshop.events.addTokenEvent')"
          icon="pi pi-plus"
          size="small"
          text
          @click="emit('change', addTokenEvent(node, key).node)"
        />
      </section>
    </template>

    <template v-else>
      <p class="help">{{ t('workshop.events.eventsHelp') }}</p>
      <EventCard
        v-for="event in others"
        :key="event.id"
        :event="event"
        :scenario="scenario"
        :node="node"
        :pieces="pieces"
        @change="(next) => emit('change', setEvent(node, event.id, next))"
        @remove="emit('change', removeEvent(node, event.id))"
      />
      <Select
        :key="others.length"
        :model-value="null"
        :options="newTrigger"
        option-label="label"
        option-value="value"
        :placeholder="t('workshop.events.addEvent')"
        :aria-label="t('workshop.events.addEvent')"
        class="add"
        @update:model-value="(v) => v && emit('change', addEvent(node, v).node)"
      />
    </template>
  </div>
</template>

<style scoped>
.rules {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.help {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.token {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.6rem 0.7rem;
  border: 1px solid var(--p-content-border-color);
  border-left: 4px solid #0891b2;
  border-radius: var(--p-content-border-radius);
}
h4 {
  margin: 0;
  font-family: ui-monospace, Consolas, monospace;
  font-size: 0.9rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.field .label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.add {
  align-self: flex-start;
}
</style>
