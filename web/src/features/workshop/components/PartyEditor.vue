<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import { useAdventurers, useDisciplines, useItems } from '@/features/catalog'
import { adventurerId } from '@/features/games'
import { pick, type LocalizedText } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftScenario } from '../api/types'
import { nextKitId, partyOf, withParty, withStart, type DraftKit } from '../lib/party'
import { text } from '../lib/graph'
import TextField from './TextField.vue'

/** The scenario's rules of departure (roadmap §5.2): who, with what budget and which kits. */
const props = defineProps<{ scenario: DraftScenario }>()
const emit = defineEmits<{ change: [scenario: DraftScenario] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const adventurers = useAdventurers()
const items = useItems()
const disciplines = useDisciplines()

const party = computed(() => partyOf(props.scenario))
const start = computed(() => party.value.start ?? {})
const kits = computed(() => start.value.kits ?? [])

const cards = computed(() => adventurers.data.value ?? [])
const asIds = (entries: string[] | null | undefined) => (entries ?? []).map((e) => adventurerId(cards.value, e))
/** The catalogue's, and what the scenario writes otherwise (a pattern, another pack's), so that nothing is lost. */
const adventurerOptions = computed(() => {
  const known = cards.value.map((a) => ({ value: a.id, label: pick(a.name, language.value) }))
  const written = [...asIds(party.value.allowedAdventurers), ...asIds(party.value.requiredAdventurers)]
  const others = [...new Set(written)].filter((v) => !known.some((k) => k.value === v))
  return [...known, ...others.map((value) => ({ value, label: value }))]
})
const nameOfCard = (card: { id: string; name: LocalizedText }) => pick(card.name, language.value) || card.id
const itemOptions = computed(() =>
  (items.data.value ?? [])
    .map((i) => ({ value: i.id, label: nameOfCard(i) }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
const disciplineOptions = computed(() =>
  (disciplines.data.value ?? [])
    .map((d) => ({ value: d.id, label: nameOfCard(d) }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
const labelOf = (options: { value: string; label: string }[], id: string) =>
  options.find((o) => o.value === id)?.label ?? id

const tiers = computed(() =>
  ['mundane', 'common', 'uncommon', 'rare'].map((value) => ({ value, label: t(`workshop.loot.${value}`) })),
)
const trees = ['assemblage', 'cruor', 'martial', 'sanctus', 'subterfuge']

const numbers = computed(() => [
  { key: 'size' as const, min: 1, max: 6 },
  { key: 'minSize' as const, min: 1, max: party.value.size ?? 6 },
  { key: 'startingGold' as const, min: 0, max: 9999 },
  { key: 'startingXp' as const, min: 0, max: 999 },
])

const setKits = (next: DraftKit[]) => emit('change', withStart(props.scenario, { kits: next }))
const setKit = (index: number, change: Partial<DraftKit>) =>
  setKits(kits.value.map((kit, i) => (i === index ? { ...kit, ...change } : kit)))

function addKit() {
  const name = text(language.value, t('workshop.party.newKit', { n: kits.value.length + 1 }))
  setKits([...kits.value, { id: nextKitId(kits.value), name }])
}
</script>

<template>
  <details class="party">
    <summary>{{ t('workshop.party.title') }}</summary>
    <p class="hint">{{ t('workshop.party.hint') }}</p>

    <div class="grid">
      <label v-for="field in numbers" :key="field.key">
        <span>{{ t(`workshop.party.${field.key}`) }}</span>
        <InputNumber
          :model-value="party[field.key] ?? null"
          :min="field.min"
          :max="field.max"
          :use-grouping="false"
          size="small"
          fluid
          @update:model-value="(value) => emit('change', withParty(scenario, { [field.key]: value }))"
        />
      </label>
    </div>

    <label>
      <span>{{ t('workshop.party.allowed') }}</span>
      <MultiSelect
        :model-value="asIds(party.allowedAdventurers)"
        :options="adventurerOptions"
        option-label="label"
        option-value="value"
        :placeholder="t('workshop.party.everybody')"
        fluid
        @update:model-value="(value: string[]) => emit('change', withParty(scenario, { allowedAdventurers: value }))"
      />
    </label>
    <label>
      <span>{{ t('workshop.party.required') }}</span>
      <MultiSelect
        :model-value="asIds(party.requiredAdventurers)"
        :options="adventurerOptions"
        option-label="label"
        option-value="value"
        :selection-limit="party.size ?? 6"
        :placeholder="t('workshop.party.nobody')"
        fluid
        @update:model-value="(value: string[]) => emit('change', withParty(scenario, { requiredAdventurers: value }))"
      />
    </label>
    <label class="toggle">
      <ToggleSwitch
        :model-value="party.custom ?? !party.allowedAdventurers?.length"
        @update:model-value="(custom: boolean) => emit('change', withParty(scenario, { custom }))"
      />
      {{ t('workshop.party.custom') }}
    </label>

    <label class="toggle">
      <ToggleSwitch
        :model-value="!!start.items"
        @update:model-value="
          (on: boolean) => emit('change', withStart(scenario, { items: on ? { tiers: ['mundane'] } : null }))
        "
      />
      {{ t('workshop.party.shop') }}
    </label>
    <div v-if="start.items" class="grid">
      <label>
        <span>{{ t('workshop.party.tiers') }}</span>
        <MultiSelect
          :model-value="start.items.tiers ?? []"
          :options="tiers"
          option-label="label"
          option-value="value"
          fluid
          @update:model-value="
            (value: string[]) => emit('change', withStart(scenario, { items: { ...start.items, tiers: value } }))
          "
        />
      </label>
      <label>
        <span>{{ t('workshop.party.maxItems') }}</span>
        <InputNumber
          :model-value="start.items.max ?? null"
          :min="1"
          :max="20"
          size="small"
          fluid
          @update:model-value="(max) => emit('change', withStart(scenario, { items: { ...start.items, max } }))"
        />
      </label>
    </div>

    <div v-if="party.startingXp" class="grid">
      <label>
        <span>{{ t('workshop.party.trees') }}</span>
        <MultiSelect
          :model-value="start.disciplines?.trees ?? []"
          :options="trees"
          :placeholder="t('workshop.party.allTrees')"
          fluid
          @update:model-value="
            (value: string[]) =>
              emit('change', withStart(scenario, { disciplines: { ...start.disciplines, trees: value } }))
          "
        />
      </label>
      <label>
        <span>{{ t('workshop.party.maxLevel') }}</span>
        <InputNumber
          :model-value="start.disciplines?.maxLevel ?? null"
          :min="1"
          :max="4"
          size="small"
          fluid
          @update:model-value="
            (maxLevel) => emit('change', withStart(scenario, { disciplines: { ...start.disciplines, maxLevel } }))
          "
        />
      </label>
    </div>

    <h4>{{ t('workshop.party.kits') }}</h4>
    <fieldset v-for="(kit, index) in kits" :key="kit.id">
      <div class="kit-head">
        <TextField
          :model-value="kit.name"
          :label="t('workshop.party.kitName')"
          @update:model-value="(name) => setKit(index, { name: name ?? {} })"
        />
        <Button
          icon="pi pi-trash"
          severity="danger"
          text
          :aria-label="t('workshop.party.removeKit')"
          @click="setKits(kits.filter((_, i) => i !== index))"
        />
      </div>
      <Select
        :model-value="kit.adventurer ? adventurerId(cards, kit.adventurer) : null"
        :options="adventurerOptions"
        option-label="label"
        option-value="value"
        :placeholder="t('workshop.party.anybody')"
        :aria-label="t('workshop.party.kitFor')"
        show-clear
        fluid
        @update:model-value="(value: string | null) => setKit(index, { adventurer: value ?? undefined })"
      />
      <ul>
        <li v-for="(id, i) in kit.items ?? []" :key="`${i}:${id}`">
          <span>{{ labelOf(itemOptions, id) }}</span>
          <Button
            icon="pi pi-times"
            size="small"
            severity="secondary"
            text
            :aria-label="t('workshop.party.removeCard', { name: labelOf(itemOptions, id) })"
            @click="setKit(index, { items: kit.items?.filter((_, n) => n !== i) })"
          />
        </li>
      </ul>
      <!-- A list and not checkboxes: a kit may hold a card twice -->
      <Select
        :model-value="null"
        :options="itemOptions"
        option-label="label"
        option-value="value"
        :placeholder="t('workshop.party.addItem')"
        filter
        fluid
        @update:model-value="(id: string | null) => id && setKit(index, { items: [...(kit.items ?? []), id] })"
      />
      <MultiSelect
        :model-value="kit.disciplines ?? []"
        :options="disciplineOptions"
        option-label="label"
        option-value="value"
        :placeholder="t('workshop.party.kitDisciplines')"
        filter
        fluid
        @update:model-value="(value: string[]) => setKit(index, { disciplines: value })"
      />
    </fieldset>
    <Button
      icon="pi pi-plus"
      :label="t('workshop.party.addKit')"
      size="small"
      severity="secondary"
      outlined
      @click="addKit"
    />
  </details>
</template>

<style scoped>
summary {
  cursor: pointer;
  font-weight: 600;
}
.party > * + * {
  margin-top: 0.6rem;
}
label {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  min-width: 0;
  font-size: 0.85rem;
}
label.toggle {
  flex-direction: row;
  align-items: center;
  gap: 0.5rem;
}
.grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.5rem;
}
.hint {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
h4 {
  margin-bottom: 0;
}
fieldset {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  min-width: 0;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
.kit-head {
  display: flex;
  align-items: flex-end;
}
.kit-head > :first-child {
  flex: 1;
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
}
li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.85rem;
}
</style>
