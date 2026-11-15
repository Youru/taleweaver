<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Chip from 'primevue/chip'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'
import type { Combatant } from '@/features/catalog'
import type { EncounterCommand, EncounterState } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { BoardLayout, Selection } from '../lib/layout'

/** What can be done with the selected figure, token or hidden area. */
const props = defineProps<{
  selection: Selection
  layout: BoardLayout
  encounter: EncounterState
  node: ScenarioNode | undefined
  cards: Map<string, Combatant>
  /** Reports are refused right now (a question other than an AI step is pending). */
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand]; close: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const adventurer = computed(() =>
  props.selection.kind === 'figure' && props.selection.id.startsWith('adventurer:')
    ? props.selection.id.slice('adventurer:'.length)
    : undefined,
)
const combatant = computed(() =>
  props.selection.kind === 'figure' && !adventurer.value
    ? props.encounter.combatants.find((c) => c.instance === props.selection.id)
    : undefined,
)
const card = computed(() => (combatant.value ? props.cards.get(combatant.value.card) : undefined))
const token = computed(() =>
  props.selection.kind === 'token' ? props.encounter.tokens.find((tk) => tk.id === props.selection.id) : undefined,
)
const tokenRule = computed(() => (token.value ? props.node?.tokens[token.value.key] : undefined))
const area = computed(() =>
  props.selection.kind === 'area'
    ? props.layout.tiles.find((tile) => tile.hidden?.id === props.selection.id)?.hidden
    : undefined,
)

const title = computed(() => {
  if (adventurer.value) return adventurer.value
  if (combatant.value) return `${combatant.value.instance} · ${card.value ? pick(card.value.name, language.value) : ''}`
  if (token.value) return token.value.key
  if (area.value) return pick(area.value.name, language.value) || area.value.id
  return ''
})

const amount = ref<number | null>(null)
const effect = ref('')
watch(
  () => props.selection,
  () => {
    amount.value = null
    effect.value = ''
  },
)

function damage(sign: 1 | -1) {
  if (combatant.value && amount.value) {
    emit('command', { type: 'damage', instance: combatant.value.instance, amount: sign * amount.value })
  }
  amount.value = null
}

function addEffect() {
  const value = effect.value.trim().toLowerCase()
  if (combatant.value && value)
    emit('command', { type: 'effect', instance: combatant.value.instance, effect: value, active: true })
  effect.value = ''
}
</script>

<template>
  <section class="selection" :aria-label="t('table.selection', { name: title })">
    <header>
      <strong>{{ title }}</strong>
      <Tag v-if="adventurer" :value="t('table.adventurer')" severity="info" />
      <Tag v-if="combatant?.aggro" :value="t('play.aggro')" severity="danger" />
      <Button icon="pi pi-times" text rounded size="small" :aria-label="t('table.close')" @click="emit('close')" />
    </header>

    <template v-if="combatant">
      <p class="hp">
        {{ combatant.damage }} / {{ card?.stats?.health ?? '?' }} {{ t('play.health') }}
        <Chip
          v-for="e in combatant.effects"
          :key="e"
          :label="e"
          removable
          :aria-label="t('play.removeEffect', { effect: e })"
          @remove="emit('command', { type: 'effect', instance: combatant.instance, effect: e, active: false })"
        />
      </p>
      <div class="actions">
        <InputNumber
          v-model="amount"
          :min="0"
          :max="999"
          size="small"
          :aria-label="`${t('play.damage')} ${combatant.instance}`"
          :disabled="locked"
        />
        <Button :label="t('play.addDamage')" size="small" :disabled="locked" @click="damage(1)" />
        <Button :label="t('play.heal')" size="small" severity="secondary" :disabled="locked" @click="damage(-1)" />
        <Button
          :label="t('play.defeat')"
          size="small"
          severity="danger"
          :disabled="locked"
          @click="emit('command', { type: 'defeat', instance: combatant.instance })"
        />
        <form @submit.prevent="addEffect">
          <InputText v-model="effect" size="small" :placeholder="t('play.effect')" :disabled="locked" />
          <Button
            type="submit"
            icon="pi pi-plus"
            size="small"
            text
            :aria-label="t('play.addEffect')"
            :disabled="locked"
          />
        </form>
      </div>
    </template>

    <template v-if="selection.kind === 'figure'">
      <p class="hint">{{ t('table.moveHint') }}</p>
    </template>

    <template v-if="token">
      <p v-if="tokenRule?.activation">{{ pick(tokenRule.activation, language) }}</p>
      <Button
        v-if="tokenRule"
        :label="t('table.activate')"
        size="small"
        :disabled="locked"
        @click="emit('command', { type: 'activateToken', token: token.key })"
      />
      <p v-else class="hint">{{ t('table.notActivatable') }}</p>
    </template>

    <template v-if="area">
      <p class="hint">{{ t('table.revealHint') }}</p>
      <Button
        :label="t('table.reveal')"
        icon="pi pi-eye"
        size="small"
        :disabled="locked"
        @click="emit('command', { type: 'revealArea', area: area.id })"
      />
    </template>
  </section>
</template>

<style scoped>
.selection {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
}
header,
.actions,
.actions form,
.hp {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
header strong {
  flex: 1;
}
p {
  margin: 0;
}
.hint {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.actions :deep(.p-inputnumber input) {
  width: 4rem;
}
.actions :deep(.p-inputtext) {
  width: 7rem;
}
</style>
