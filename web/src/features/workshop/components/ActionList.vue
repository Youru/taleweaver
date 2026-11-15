<script setup lang="ts">
import { computed } from 'vue'
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
import { atOf, isSimple, newAction, SIMPLE_ACTIONS, spotOf, type SimpleAction, type Spot } from '../lib/events'
import { mapOf, tokenKey } from '../lib/map'
import JsonField from './JsonField.vue'
import TargetSelect from './TargetSelect.vue'
import TextField from './TextField.vue'

/**
 * The actions of an event (or of a hidden rule, an option…), in order: the usual ones as forms, the
 * others (choices, skill checks, nested branches) as JSON so that nothing is out of reach.
 */
const actions = defineModel<DraftAction[]>({ required: true })
const props = defineProps<{
  scenario: DraftScenario
  node: DraftNode
  pieces: BoardPieces | undefined
  label: string
}>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

function patch(index: number, fields: Record<string, unknown>) {
  actions.value = actions.value.map((a, i) => {
    if (i !== index) return a
    const next: Record<string, unknown> = { ...a, ...fields }
    for (const [k, v] of Object.entries(fields)) if (v === undefined || v === '') delete next[k]
    return next as DraftAction
  })
}
function replace(index: number, action: unknown) {
  if (!action || typeof action !== 'object' || typeof (action as DraftAction).do !== 'string') return
  actions.value = actions.value.map((a, i) => (i === index ? (action as DraftAction) : a))
}
function remove(index: number) {
  actions.value = actions.value.filter((_, i) => i !== index)
}
function shift(index: number, by: number) {
  const list = [...actions.value]
  const [moved] = list.splice(index, 1)
  if (moved) list.splice(index + by, 0, moved)
  actions.value = list
}
function add(kind: SimpleAction | 'json') {
  actions.value = [
    ...actions.value,
    kind === 'json'
      ? { do: 'askChoice', question: { [language.value]: '' }, options: [] }
      : newAction(kind, language.value),
  ]
}

const kinds = computed(() => [
  ...SIMPLE_ACTIONS.map((value) => ({ value, label: t(`workshop.events.do.${value}`) })),
  { value: 'json', label: t('workshop.events.do.json') },
])
const kindLabel = (kind: string) =>
  (SIMPLE_ACTIONS as readonly string[]).includes(kind) ? t(`workshop.events.do.${kind}`) : kind

// What the fields offer, from the encounter and the catalogue
const map = computed(() => mapOf(props.node))
const areaOptions = computed(() =>
  map.value.areas.map((a) => ({ value: a.id, label: pick(a.name, language.value) || a.id })),
)
const mapTokenOptions = computed(() =>
  map.value.tokens.map((tk) => ({ value: tk.id, label: `${tk.id} (${tokenKey(tk.type, tk.color)})` })),
)
const keyOptions = computed(() =>
  [...new Set([...map.value.tokens.map((tk) => tokenKey(tk.type, tk.color)), ...Object.keys(props.node.tokens ?? {})])]
    .sort()
    .map((value) => ({ value, label: value })),
)
const tokenTypeOptions = computed(() =>
  Object.entries(props.pieces?.tokens ?? {})
    .filter(([, p]) => p.category !== 'tracker' && p.category !== 'effect' && p.category !== 'ability')
    .map(([value, p]) => ({ value, label: pick(p.name, language.value) || value }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
const colorsOf = (type: unknown) =>
  Object.keys((typeof type === 'string' && props.pieces?.tokens[type]?.colors) || {}).map((value) => ({
    value,
    label: t(`workshop.map.color.${value}`, value),
  }))
const familyOptions = computed(() =>
  Object.entries(props.pieces?.families ?? {})
    .map(([value, p]) => ({ value, label: pick(p.name, language.value) || value }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
const endOptions = computed(() =>
  (props.node.endConditions ?? []).map((e) => ({
    value: e.id,
    label: `${e.kind === 'lose' ? t('workshop.lose') : t('workshop.win')} · ${pick(e.text, language.value) || e.id}`,
  })),
)
const tierOptions = computed(() =>
  ['mundane', 'common', 'uncommon', 'rare'].map((value) => ({ value, label: t(`workshop.loot.${value}`) })),
)
const spotKinds = computed(() =>
  (['gm', 'on', 'adjacentTo', 'cell'] as const).map((value) => ({ value, label: t(`workshop.events.spot.${value}`) })),
)

function setSpot(index: number, spot: Spot) {
  patch(index, { at: atOf(spot) })
}
function spotKind(index: number, kind: Spot['kind']) {
  const first = keyOptions.value[0]?.value ?? ''
  setSpot(
    index,
    kind === 'cell'
      ? { kind, x: 0, y: 0 }
      : kind === 'on' || kind === 'adjacentTo'
        ? { kind, token: first }
        : { kind: 'gm' },
  )
}
const textOf = (value: unknown) => (value && typeof value === 'object' ? (value as Record<string, string>) : undefined)
const str = (value: unknown) => (typeof value === 'string' ? value : undefined)
const num = (value: unknown) => (typeof value === 'number' ? value : undefined)
</script>

<template>
  <div class="actions">
    <span class="title">{{ label }}</span>
    <p v-if="!actions.length" class="empty">{{ t('workshop.events.noAction') }}</p>
    <ol>
      <li v-for="(action, index) in actions" :key="index">
        <header>
          <strong>{{ kindLabel(action.do) }}</strong>
          <span class="spacer" />
          <Button
            icon="pi pi-arrow-up"
            size="small"
            text
            severity="secondary"
            :disabled="index === 0"
            :aria-label="t('workshop.events.up', { n: index + 1 })"
            @click="shift(index, -1)"
          />
          <Button
            icon="pi pi-arrow-down"
            size="small"
            text
            severity="secondary"
            :disabled="index === actions.length - 1"
            :aria-label="t('workshop.events.down', { n: index + 1 })"
            @click="shift(index, 1)"
          />
          <Button
            icon="pi pi-times"
            size="small"
            text
            severity="secondary"
            :aria-label="t('workshop.events.removeAction', { n: index + 1 })"
            @click="remove(index)"
          />
        </header>

        <template v-if="isSimple(action)">
          <TextField
            v-if="action.do === 'showText'"
            :model-value="textOf(action.text)"
            :label="t('workshop.events.text')"
            long
            :rows="3"
            @update:model-value="(v) => patch(index, { text: v })"
          />

          <Select
            v-else-if="action.do === 'revealArea'"
            :model-value="str(action.area)"
            :options="areaOptions"
            option-label="label"
            option-value="value"
            :placeholder="t('workshop.events.pickArea')"
            :aria-label="t('workshop.events.area')"
            @update:model-value="(v) => patch(index, { area: v })"
          />

          <Select
            v-else-if="action.do === 'removeToken'"
            :model-value="str(action.token)"
            :options="mapTokenOptions"
            option-label="label"
            option-value="value"
            :placeholder="t('workshop.events.pickToken')"
            :aria-label="t('workshop.events.token')"
            @update:model-value="(v) => patch(index, { token: v })"
          />

          <template v-else-if="action.do === 'placeToken' || action.do === 'spawn'">
            <div class="row">
              <Select
                v-if="action.do === 'placeToken'"
                :model-value="str(action.type)"
                :options="tokenTypeOptions"
                option-label="label"
                option-value="value"
                filter
                :placeholder="t('workshop.events.pickTokenType')"
                :aria-label="t('workshop.events.tokenType')"
                @update:model-value="(v) => patch(index, { type: v, color: undefined })"
              />
              <Select
                v-if="action.do === 'placeToken' && colorsOf(action.type).length"
                :model-value="str(action.color)"
                :options="colorsOf(action.type)"
                option-label="label"
                option-value="value"
                :aria-label="t('workshop.map.tokenColor')"
                @update:model-value="(v) => patch(index, { color: v })"
              />
              <Select
                v-if="action.do === 'spawn'"
                :model-value="str(action.family)"
                :options="familyOptions"
                option-label="label"
                option-value="value"
                filter
                :placeholder="t('workshop.events.pickFamily')"
                :aria-label="t('workshop.map.family')"
                @update:model-value="(v) => patch(index, { family: v })"
              />
              <InputNumber
                v-if="action.do === 'spawn'"
                :model-value="num(action.count) ?? 1"
                :min="1"
                size="small"
                input-class="num"
                :aria-label="t('workshop.events.count')"
                @update:model-value="(v) => patch(index, { count: v ?? 1 })"
              />
            </div>
            <div class="row">
              <Select
                :model-value="spotOf(action.at).kind === 'other' ? undefined : spotOf(action.at).kind"
                :options="spotKinds"
                option-label="label"
                option-value="value"
                :placeholder="t('workshop.events.spot.other')"
                :aria-label="t('workshop.events.where')"
                @update:model-value="(v) => spotKind(index, v)"
              />
              <Select
                v-if="spotOf(action.at).kind === 'on' || spotOf(action.at).kind === 'adjacentTo'"
                :model-value="(spotOf(action.at) as { token: string }).token"
                :options="keyOptions"
                option-label="label"
                option-value="value"
                :aria-label="t('workshop.events.spotToken')"
                @update:model-value="(v) => setSpot(index, { kind: spotOf(action.at).kind as 'on', token: v })"
              />
              <template v-else-if="spotOf(action.at).kind === 'cell'">
                <InputNumber
                  :model-value="(spotOf(action.at) as { x: number }).x"
                  :min="0"
                  size="small"
                  input-class="num"
                  :aria-label="t('workshop.map.column')"
                  @update:model-value="
                    (v) =>
                      setSpot(index, { ...(spotOf(action.at) as { kind: 'cell'; x: number; y: number }), x: v ?? 0 })
                  "
                />
                <InputNumber
                  :model-value="(spotOf(action.at) as { y: number }).y"
                  :min="0"
                  size="small"
                  input-class="num"
                  :aria-label="t('workshop.map.row')"
                  @update:model-value="
                    (v) =>
                      setSpot(index, { ...(spotOf(action.at) as { kind: 'cell'; x: number; y: number }), y: v ?? 0 })
                  "
                />
              </template>
            </div>
            <JsonField
              v-if="spotOf(action.at).kind === 'other'"
              :model-value="action.at"
              :label="t('workshop.events.whereJson')"
              :rows="2"
              @update:model-value="(v) => patch(index, { at: v })"
            />
          </template>

          <div v-else-if="action.do === 'addUrgency' || action.do === 'gainGold' || action.do === 'gainXp'" class="row">
            <InputNumber
              :model-value="num(action.amount) ?? 0"
              size="small"
              input-class="num"
              :aria-label="t('workshop.events.amount')"
              @update:model-value="(v) => patch(index, { amount: v ?? 0 })"
            />
            <label v-if="action.do === 'gainXp'" class="inline">
              <ToggleSwitch
                :model-value="action.each === true"
                @update:model-value="(v) => patch(index, { each: v || undefined })"
              />
              {{ t('workshop.events.each') }}
            </label>
          </div>

          <div v-else-if="action.do === 'gainItem'" class="row">
            <MultiSelect
              :model-value="(action.tiers as string[] | undefined) ?? []"
              :options="tierOptions"
              option-label="label"
              option-value="value"
              :aria-label="t('workshop.events.tiers')"
              :placeholder="t('workshop.events.tiers')"
              @update:model-value="(v) => patch(index, { tiers: v })"
            />
            <InputText
              :model-value="str(action.type) ?? ''"
              size="small"
              :aria-label="t('workshop.events.itemType')"
              :placeholder="t('workshop.events.itemType')"
              @update:model-value="(v) => patch(index, { type: v })"
            />
            <InputNumber
              :model-value="num(action.count) ?? 1"
              :min="1"
              size="small"
              input-class="num"
              :aria-label="t('workshop.events.count')"
              @update:model-value="(v) => patch(index, { count: v ?? 1 })"
            />
          </div>

          <InputText
            v-else-if="action.do === 'setFlag'"
            :model-value="str(action.flag) ?? ''"
            size="small"
            :aria-label="t('workshop.events.flag')"
            :placeholder="t('workshop.events.flag')"
            @update:model-value="(v) => patch(index, { flag: v })"
          />

          <Select
            v-else-if="action.do === 'endEncounter' || action.do === 'addEndCondition'"
            :model-value="str(action.endCondition)"
            :options="endOptions"
            option-label="label"
            option-value="value"
            :placeholder="t('workshop.events.pickEnd')"
            :aria-label="t('workshop.events.endCondition')"
            @update:model-value="(v) => patch(index, { endCondition: v })"
          />

          <TargetSelect
            v-else-if="action.do === 'goToNode'"
            :model-value="str(action.node)"
            :scenario="scenario"
            :from="node.id"
            :label="t('workshop.events.goTo')"
            @update:model-value="(v) => patch(index, { node: v })"
          />

          <p v-else-if="action.do === 'restore'" class="empty">{{ t('workshop.events.restoreHelp') }}</p>
        </template>
        <JsonField
          v-else
          :model-value="action"
          :label="t('workshop.events.json')"
          @update:model-value="(v) => replace(index, v)"
        />
      </li>
    </ol>
    <Select
      :key="actions.length"
      :model-value="null"
      :options="kinds"
      option-label="label"
      option-value="value"
      :placeholder="t('workshop.events.addAction')"
      :aria-label="t('workshop.events.addActionTo', { label })"
      class="add"
      @update:model-value="(v) => v && add(v)"
    />
  </div>
</template>

<style scoped>
.actions {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
ol {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
li {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.4rem 0.5rem;
  border-radius: 6px;
  background: var(--p-content-hover-background);
}
header {
  display: flex;
  align-items: center;
  font-size: 0.85rem;
}
.spacer {
  flex: 1;
}
.row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  align-items: center;
}
.row :deep(.num) {
  width: 5rem;
}
.inline {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.8rem;
}
.empty {
  margin: 0;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.add {
  align-self: flex-start;
}
</style>
