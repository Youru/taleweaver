<script setup lang="ts">
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Chip from 'primevue/chip'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'
import type { Combatant } from '@/features/catalog'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CombatantState, EncounterCommand, EncounterState } from '../api/types'
import { shortId } from '../lib/ids'

const props = defineProps<{
  encounter: EncounterState
  node: ScenarioNode | undefined
  cards: Map<string, Combatant>
  /** A question is pending: only board reports are allowed during an AI question. */
  blocked: boolean
  aiQuestion: boolean
  pending: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const reportsLocked = computed(() => props.pending || (props.blocked && !props.aiQuestion))
const alive = computed(() => props.encounter.combatants.filter((c) => !c.defeated))
const defeated = computed(() => props.encounter.combatants.filter((c) => c.defeated))
const acting = computed(() => props.encounter.activation?.figures[0])

/** Tokens the GM can activate: those the encounter describes and those on the board. */
const tokenKeys = computed(() => {
  const onBoard = props.encounter.tokens.filter((tk) => !tk.removed).map((tk) => tk.key)
  const described = Object.keys(props.node?.tokens ?? {})
  return [...new Set([...described, ...onBoard])].filter((key) => key.includes(':')).sort()
})
const endConditions = computed(() =>
  (props.node?.endConditions ?? []).filter((e) => props.encounter.activeEndConditions.includes(e.id)),
)

function cardName(figure: CombatantState): string {
  const card = props.cards.get(figure.card)
  return card ? pick(card.name, language.value) : shortId(figure.card)
}

function health(figure: CombatantState): string {
  const value = props.cards.get(figure.card)?.stats?.health
  return value === undefined ? '?' : String(value)
}

const amounts = reactive<Record<string, number | null>>({})
const effects = reactive<Record<string, string>>({})

function damage(figure: CombatantState, sign: 1 | -1) {
  const amount = amounts[figure.instance] ?? 0
  if (amount > 0) emit('command', { type: 'damage', instance: figure.instance, amount: sign * amount })
  amounts[figure.instance] = null
}

function addEffect(figure: CombatantState) {
  const effect = effects[figure.instance]?.trim().toLowerCase()
  if (effect) emit('command', { type: 'effect', instance: figure.instance, effect, active: true })
  effects[figure.instance] = ''
}
</script>

<template>
  <section class="encounter" :aria-label="pick(node?.name, language)">
    <header>
      <h2>{{ pick(node?.name, language) }}</h2>
      <Tag
        :value="encounter.round > 0 ? t('play.round', { round: encounter.round }) : t('play.notStarted')"
        severity="secondary"
      />
      <Tag
        :value="t('play.urgency', { urgency: encounter.urgency })"
        :severity="encounter.urgency >= 3 ? 'danger' : 'warn'"
      />
      <Button
        :label="encounter.round > 0 ? t('play.nextTurn') : t('play.startRound')"
        icon="pi pi-step-forward"
        :disabled="blocked || pending || encounter.ended"
        @click="emit('command', { type: 'nextTurn' })"
      />
    </header>

    <div v-if="encounter.round > 0">
      <h3>{{ t('play.initiative') }}</h3>
      <ol class="track">
        <li
          v-for="(card, index) in encounter.initiativeTrack"
          :key="card"
          :class="{ current: index === encounter.currentTurn, done: index < encounter.currentTurn }"
        >
          {{ shortId(card) }}
        </li>
      </ol>
    </div>

    <h3>{{ t('play.combatants') }}</h3>
    <ul class="combatants">
      <li v-for="figure in alive" :key="figure.instance" :class="{ acting: figure.instance === acting }">
        <div class="identity">
          <strong>{{ figure.instance }}</strong>
          <span>{{ cardName(figure) }}</span>
          <Tag v-if="figure.aggro" :value="t('play.aggro')" severity="danger" />
          <span class="hp">{{ figure.damage }} / {{ health(figure) }} {{ t('play.health') }}</span>
        </div>
        <div class="effects">
          <Chip
            v-for="effect in figure.effects"
            :key="effect"
            :label="effect"
            removable
            :remove-icon="'pi pi-times'"
            :aria-label="t('play.removeEffect', { effect })"
            @remove="emit('command', { type: 'effect', instance: figure.instance, effect, active: false })"
          />
        </div>
        <div class="actions">
          <InputNumber
            v-model="amounts[figure.instance]"
            :min="0"
            :max="999"
            :aria-label="`${t('play.damage')} ${figure.instance}`"
            size="small"
            :disabled="reportsLocked"
          />
          <Button :label="t('play.addDamage')" size="small" :disabled="reportsLocked" @click="damage(figure, 1)" />
          <Button
            :label="t('play.heal')"
            size="small"
            severity="secondary"
            :disabled="reportsLocked"
            @click="damage(figure, -1)"
          />
          <Button
            :label="t('play.defeat')"
            size="small"
            severity="danger"
            :disabled="reportsLocked"
            @click="emit('command', { type: 'defeat', instance: figure.instance })"
          />
          <form @submit.prevent="addEffect(figure)">
            <InputText
              v-model="effects[figure.instance]"
              size="small"
              :placeholder="t('play.effect')"
              :aria-label="`${t('play.effect')} ${figure.instance}`"
              :disabled="reportsLocked"
            />
            <Button
              type="submit"
              icon="pi pi-plus"
              size="small"
              text
              :aria-label="t('play.addEffect')"
              :disabled="reportsLocked"
            />
          </form>
        </div>
      </li>
    </ul>
    <p v-if="defeated.length" class="defeated">
      {{ t('play.defeatedCombatants') }} : {{ defeated.map((c) => c.instance).join(', ') }}
    </p>

    <h3>{{ t('play.tokens') }}</h3>
    <div class="tokens">
      <Button
        v-for="key in tokenKeys"
        :key="key"
        :label="key"
        size="small"
        severity="secondary"
        outlined
        :title="pick(node?.tokens[key]?.activation, language)"
        :disabled="reportsLocked"
        @click="emit('command', { type: 'activateToken', token: key })"
      />
    </div>

    <h3>{{ t('play.endConditions') }}</h3>
    <ul class="ends">
      <li v-for="end in endConditions" :key="end.id">
        <Tag
          :value="end.kind === 'win' ? t('play.win') : t('play.lose')"
          :severity="end.kind === 'win' ? 'success' : 'danger'"
        />
        <span>{{ pick(end.text, language) }}</span>
        <Button
          :label="t('play.reached')"
          size="small"
          :disabled="reportsLocked"
          @click="emit('command', { type: 'endCondition', endCondition: end.id })"
        />
      </li>
    </ul>
  </section>
</template>

<style scoped>
header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}
h2 {
  margin: 0;
  font-size: 1.2rem;
  flex: 1;
}
h3 {
  margin: 1rem 0 0.4rem;
  font-size: 0.95rem;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}
.track {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  list-style: none;
  padding: 0;
  margin: 0;
}
.track li {
  padding: 0.15rem 0.5rem;
  border-radius: 1rem;
  border: 1px solid var(--p-content-border-color);
  font-size: 0.85rem;
}
.track .done {
  opacity: 0.45;
}
.track .current {
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
}
.combatants,
.ends {
  list-style: none;
  padding: 0;
  margin: 0;
}
.combatants li {
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  padding: 0.5rem;
  margin-bottom: 0.4rem;
}
.combatants li.acting {
  border-color: var(--p-orange-500);
  box-shadow: 0 0 0 1px var(--p-orange-500);
}
.identity,
.actions,
.actions form,
.effects,
.tokens,
.ends li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.identity .hp {
  margin-left: auto;
  font-variant-numeric: tabular-nums;
}
.effects:empty {
  display: none;
}
.actions {
  margin-top: 0.4rem;
}
.actions :deep(.p-inputnumber input) {
  width: 4rem;
}
.actions :deep(.p-inputtext) {
  width: 7rem;
}
.defeated {
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
}
.ends li {
  margin-bottom: 0.3rem;
}
</style>
