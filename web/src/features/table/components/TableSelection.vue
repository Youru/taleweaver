<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Chip from 'primevue/chip'
import InputGroup from 'primevue/inputgroup'
import InputNumber from 'primevue/inputnumber'
import Select, { type SelectChangeEvent } from 'primevue/select'
import Tag from 'primevue/tag'
import { useEffects, type Combatant } from '@/features/catalog'
import type { AdventurerState, EncounterCommand, EncounterState } from '@/features/games'
import { ruleOfToken, useReference } from '@/features/reference'
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
  /** The party: an adventurer's Health and SP. */
  party?: AdventurerState[]
}>()
const emit = defineEmits<{ command: [command: EncounterCommand]; close: []; duel: [figure: string] }>()

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
const ally = computed(() =>
  props.selection.kind === 'figure'
    ? props.encounter.allies?.find((a) => `ally:${a.id}` === props.selection.id)
    : undefined,
)
/** Who shares this Esper's Damage, and may spend a Summon token on it (rulebook p. 75). */
const summoner = computed(() =>
  ally.value?.esper ? props.party?.find((a) => a.name === ally.value?.owner) : undefined,
)
const fromOpponent = ref(true)
const useToken = ref(false)
const card = computed(() => {
  const id = combatant.value?.card ?? ally.value?.card
  return id ? props.cards.get(id) : undefined
})
/** The selected adventurer's sheet: Damage, maximum HP, SP. */
const member = computed(() => props.party?.find((a) => a.name === adventurer.value))
const token = computed(() =>
  props.selection.kind === 'token' ? props.encounter.tokens.find((tk) => tk.id === props.selection.id) : undefined,
)
const tokenRule = computed(() => (token.value ? props.node?.tokens[token.value.key] : undefined))
const reference = useReference()
/** The rulebook's line on the selected token (terrain, Plank…). */
const tokenHelp = computed(() =>
  reference.data.value ? ruleOfToken(reference.data.value, token.value?.type) : undefined,
)
const area = computed(() =>
  props.selection.kind === 'area'
    ? props.layout.tiles.find((tile) => tile.hidden?.id === props.selection.id)?.hidden
    : undefined,
)

const title = computed(() => {
  if (adventurer.value) return adventurer.value
  if (ally.value) return pick(ally.value.name, language.value) || ally.value.id
  if (combatant.value) return combatant.value.instance
  if (token.value) return token.value.key
  if (area.value) return pick(area.value.name, language.value) || area.value.id
  return ''
})

/** Health printed on the card (an adventurer's: with their equipment), and the share of it already taken (for the bar). */
const health = computed(() => {
  if (member.value) return member.value.health || undefined
  if (ally.value) return ally.value.health ?? undefined
  const value = combatant.value?.health ?? card.value?.stats?.health
  return typeof value === 'number' ? value : undefined
})
const damageTaken = computed(() => combatant.value?.damage ?? ally.value?.damage ?? member.value?.damage ?? 0)
const taken = computed(() => (health.value ? Math.min(100, (damageTaken.value / health.value) * 100) : 0))
/** Who takes the Damage: a combatant, or "adventurer:<name>". */
const vitalsTarget = computed(() =>
  combatant.value ? combatant.value.instance : member.value || ally.value ? props.selection.id : undefined,
)
/** Who holds SP: an adventurer, or an ally. */
const spHolder = computed(() => {
  const held = member.value ?? ally.value
  if (!held) return undefined
  return {
    id: member.value ? member.value.name : props.selection.id,
    stamina: held.stamina ?? 0,
    max: held.staminaMax ?? 5,
    defeated: !!held.defeated,
  }
})
const defeated = computed(() => combatant.value?.defeated ?? ally.value?.defeated ?? member.value?.defeated ?? false)

function stamina(by: number) {
  if (!spHolder.value) return
  const value = Math.max(0, Math.min(spHolder.value.max, spHolder.value.stamina + by))
  emit('command', { type: 'stamina', adventurer: spHolder.value.id, value })
}

const amount = ref<number | null>(null)
const effect = ref<string | null>('')
watch(
  () => props.selection,
  () => {
    amount.value = null
    effect.value = ''
    fromOpponent.value = true
    useToken.value = false
  },
)

function damage(sign: 1 | -1) {
  if (vitalsTarget.value && amount.value) {
    const shared =
      sign === 1 && summoner.value
        ? { fromOpponent: fromOpponent.value, tokens: useToken.value ? 1 : undefined }
        : {}
    emit('command', { type: 'damage', instance: vitalsTarget.value, amount: sign * amount.value, ...shared })
    useToken.value = false
  }
  amount.value = null
}

/** Who the Effects go on (a combatant still in play, or an adventurer), and those it has. */
const effectTarget = computed(() =>
  combatant.value && !combatant.value.defeated
    ? combatant.value.instance
    : (adventurer.value && !member.value?.defeated) || (ally.value && !ally.value.defeated)
      ? props.selection.id
      : undefined,
)
const figureEffects = computed(
  () => combatant.value?.effects ?? ally.value?.effects ?? props.encounter.adventurers.find((a) => a.name === adventurer.value)?.effects ?? [],
)

/** The Effects of the rules, for the list; a home-made one can still be typed. */
const effects = useEffects()
const effectById = computed(() => new Map((effects.data.value ?? []).map((e) => [e.id, e])))
const effectOptions = computed(() =>
  (effects.data.value ?? [])
    .filter((e) => !figureEffects.value.includes(e.id))
    .map((e) => ({
      value: e.id,
      label: pick(e.name, language.value),
      text: pick(e.text, language.value),
      polarity: e.polarity,
      status: e.condition === 'status',
    })),
)
/** "Poison", "poison" or a home-made effect, as the engine keeps it (the catalogue id when known). */
function effectId(typed: string): string {
  const value = typed.trim().toLowerCase()
  const known = (effects.data.value ?? []).find(
    (e) => e.id === value || pick(e.name, language.value).toLowerCase() === value,
  )
  return known?.id ?? value
}
const effectLabel = (id: string) => {
  const known = effectById.value.get(id)
  return known ? pick(known.name, language.value) : id
}
const effectTitle = (id: string) => pick(effectById.value.get(id)?.text, language.value)
const effectClass = (id: string) => `effect-${effectById.value.get(id)?.polarity ?? 'other'}`

function addEffect() {
  const value = effectId(effect.value ?? '')
  if (effectTarget.value && value)
    emit('command', { type: 'effect', instance: effectTarget.value, effect: value, active: true })
  effect.value = ''
}

/** Picked in the list: added at once (typing waits for Enter or +). */
function pickEffect(event: SelectChangeEvent) {
  if (event.originalEvent?.type !== 'input' && effectById.value.has(String(event.value))) addEffect()
}
</script>

<template>
  <section class="selection" :aria-label="t('table.selection', { name: title })">
    <header>
      <span v-if="combatant" class="number" aria-hidden="true">{{ combatant.number || '·' }}</span>
      <span v-else-if="adventurer || ally" class="number adventurer" aria-hidden="true"><i class="pi pi-user" /></span>
      <span class="who">
        <strong>{{ title }}</strong>
        <small v-if="ally">
          {{ ally.owner ? t('allies.of', { name: ally.owner }) : t('allies.ofParty') }}
          <template v-if="ally.force"> · {{ t('allies.force', { n: ally.force }) }}</template>
        </small>
        <small v-else-if="card">{{ pick(card.name, language) }}</small>
        <small v-else-if="adventurer">{{ t('table.adventurer') }}</small>
      </span>
      <Tag v-if="combatant?.aggro" :value="t('play.aggro')" severity="danger" />
      <Tag v-if="member?.defeated" :value="t('play.adventurerDefeatedTag')" severity="secondary" />
      <Button icon="pi pi-times" text rounded size="small" :aria-label="t('table.close')" @click="emit('close')" />
    </header>

    <p v-if="tokenHelp" class="rule-help">
      <strong>{{ pick(tokenHelp.name, language) }}</strong> : {{ pick(tokenHelp.text, language) }}
    </p>
    <template v-if="vitalsTarget">
      <div class="vitals">
        <span class="hp-text">{{ damageTaken }} / {{ health ?? '?' }} {{ t('play.health') }}</span>
        <span class="hp-bar" aria-hidden="true"><span :style="{ width: `${taken}%` }" /></span>
      </div>

      <div v-if="spHolder" class="stamina">
        <span class="sp-text">{{ t('play.stamina', { sp: spHolder.stamina, max: spHolder.max }) }}</span>
        <span class="sp-dots" aria-hidden="true">
          <span v-for="i in spHolder.max" :key="i" :class="{ on: i <= spHolder.stamina }" />
        </span>
        <Button
          icon="pi pi-minus"
          text
          rounded
          size="small"
          :aria-label="t('play.spendStamina')"
          :disabled="locked || spHolder.defeated || !spHolder.stamina"
          @click="stamina(-1)"
        />
        <Button
          icon="pi pi-plus"
          text
          rounded
          size="small"
          :aria-label="t('play.gainStamina')"
          :disabled="locked || spHolder.defeated || spHolder.stamina >= spHolder.max"
          @click="stamina(1)"
        />
      </div>

      <div v-if="ally && summoner && !ally.defeated" class="esper">
        <label>
          <Checkbox v-model="fromOpponent" binary />
          {{ t('allies.fromOpponent', { name: summoner.name }) }}
        </label>
        <label v-if="summoner.summonTokens">
          <Checkbox v-model="useToken" binary />
          {{ t('allies.useToken', { n: summoner.summonTokens }) }}
        </label>
        <Button
          :label="t('allies.dismiss')"
          size="small"
          severity="secondary"
          text
          :disabled="locked"
          @click="emit('command', { type: 'dismissAlly', ally: selection.id })"
        />
        <Button
          :label="t('allies.dismissInstead')"
          size="small"
          severity="secondary"
          text
          :disabled="locked"
          @click="emit('command', { type: 'dismissAlly', ally: selection.id, ignoreDamage: true })"
        />
      </div>
      <InputGroup class="damage">
        <InputNumber
          v-model="amount"
          :min="0"
          :max="999"
          size="small"
          :placeholder="t('play.damage')"
          :aria-label="`${t('play.damage')} ${title}`"
          :disabled="locked"
          @keydown.enter="damage(1)"
        />
        <Button
          :label="t('play.addDamage')"
          icon="pi pi-minus-circle"
          size="small"
          severity="warn"
          :disabled="locked || defeated"
          @click="damage(1)"
        />
        <Button
          :label="t('play.heal')"
          icon="pi pi-heart"
          size="small"
          severity="secondary"
          :disabled="locked"
          @click="damage(-1)"
        />
      </InputGroup>
    </template>

    <template v-if="effectTarget">
      <div class="effects">
        <Chip
          v-for="e in figureEffects"
          :key="e"
          :label="effectLabel(e)"
          :title="effectTitle(e)"
          :class="effectClass(e)"
          removable
          :aria-label="t('play.removeEffect', { effect: e })"
          @remove="emit('command', { type: 'effect', instance: effectTarget!, effect: e, active: false })"
        />
        <form @submit.prevent="addEffect">
          <InputGroup>
            <Select
              v-model="effect"
              :options="effectOptions"
              option-label="label"
              option-value="value"
              editable
              size="small"
              :placeholder="t('play.effect')"
              :aria-label="t('play.effect')"
              :disabled="locked"
              @change="pickEffect"
              @keydown.enter.prevent="addEffect"
            >
              <template #option="{ option }">
                <span class="effect-option">
                  <span>
                    <strong>{{ option.label }}</strong>
                    <Tag
                      :value="t(option.polarity === 'positive' ? 'play.positive' : 'play.negative')"
                      :severity="option.polarity === 'positive' ? 'success' : 'danger'"
                    />
                    <Tag v-if="option.status" :value="t('play.statusEffect')" severity="secondary" />
                  </span>
                  <small>{{ option.text }}</small>
                </span>
              </template>
            </Select>
            <Button
              type="submit"
              icon="pi pi-plus"
              size="small"
              severity="secondary"
              :aria-label="t('play.addEffect')"
              :disabled="locked || !effect?.trim()"
            />
          </InputGroup>
        </form>
      </div>
    </template>

    <template v-if="selection.kind === 'figure'">
      <div class="footer">
        <Button
          v-if="adventurer || (ally && !ally.defeated) || (combatant && !combatant.defeated)"
          :label="t('duel.open')"
          icon="pi pi-bolt"
          size="small"
          severity="contrast"
          @click="emit('duel', selection.id)"
        />
        <Button
          v-if="vitalsTarget && !defeated"
          :label="t('play.defeat')"
          icon="pi pi-times-circle"
          size="small"
          severity="danger"
          outlined
          :disabled="locked"
          @click="emit('command', { type: 'defeat', instance: vitalsTarget! })"
        />
      </div>
      <p class="hint"><i class="pi pi-arrows-alt" aria-hidden="true" /> {{ t('table.moveHint') }}</p>
    </template>

    <template v-if="token">
      <p v-if="tokenRule?.activation">{{ pick(tokenRule.activation, language) }}</p>
      <Button
        v-if="tokenRule"
        class="start"
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
        class="start"
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
.esper {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.85rem;
}
.esper label {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  width: 100%;
}
.rule-help {
  margin: 0.4rem 0;
  font-size: 0.9rem;
}
.selection {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-left: 3px solid var(--p-yellow-400);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
}
header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.number {
  display: grid;
  place-items: center;
  flex: none;
  width: 1.8rem;
  height: 1.8rem;
  border-radius: 50%;
  background: var(--p-red-600);
  color: #fff;
  font-weight: 700;
  font-size: 0.9rem;
}
.number.adventurer {
  background: var(--p-sky-600);
}
.who {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  line-height: 1.2;
}
.who small {
  color: var(--p-text-muted-color);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.vitals {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.hp-text {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}
.hp-bar {
  height: 0.45rem;
  border-radius: 1rem;
  background: var(--p-content-border-color);
  overflow: hidden;
}
.stamina {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}
.sp-text {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}
.sp-dots {
  display: inline-flex;
  gap: 0.2rem;
  margin-inline-end: auto;
}
.sp-dots span {
  width: 0.7rem;
  height: 0.7rem;
  border-radius: 50%;
  background: var(--p-content-border-color);
}
.sp-dots span.on {
  background: var(--p-red-500);
}
.hp-bar span {
  display: block;
  height: 100%;
  background: var(--p-red-500);
  transition: width 0.2s;
}
.damage :deep(.p-inputnumber input) {
  width: 4.5rem;
}
.effects {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
}
.effect-option {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  max-width: 22rem;
  white-space: normal;
}
.effect-option > span {
  display: flex;
  gap: 0.35rem;
  align-items: center;
}
.effect-option small {
  color: var(--p-text-muted-color);
}
.effect-positive {
  background: var(--p-green-100);
  color: var(--p-green-800);
}
.effect-negative {
  background: var(--p-red-100);
  color: var(--p-red-800);
}
.effects form {
  flex: 1 1 10rem;
}
.footer {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.start {
  align-self: flex-start;
}
p {
  margin: 0;
}
.hint {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
</style>
