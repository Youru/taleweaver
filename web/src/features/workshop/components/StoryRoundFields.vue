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
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftNode, DraftScenario, DraftStoryExit } from '../api/types'
import { setText } from '../lib/graph'
import { storeOf, type DraftStore } from '../lib/storyRound'
import TargetSelect from './TargetSelect.vue'
import TextField from './TextField.vue'

/**
 * The sheet of a Story Round (rulebook p. 13): its page, its Store (lines of tiers and types, the upgrades
 * unlocked, random Unique Items), its Side Quests, the Bounty difficulty it allows and where to Venture Forth.
 */
const props = defineProps<{ node: DraftNode; scenario: DraftScenario }>()
const emit = defineEmits<{ change: [node: DraftNode] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const update = (fields: Partial<DraftNode>) => emit('change', { ...props.node, ...fields })
const model = <K extends keyof DraftNode>(key: K) =>
  computed({ get: () => props.node[key], set: (value: DraftNode[K]) => update({ [key]: value } as Partial<DraftNode>) })
const name = model('name')
const narrative = model('narrative')

const tiers = ['mundane', 'common', 'uncommon', 'rare'].map((value) => ({ value, label: t(`workshop.loot.${value}`) }))
const types = ['weapon', 'armor', 'core', 'accessory', 'relic', 'consumable'].map((value) => ({
  value,
  label: t(`inventory.types.${value}`),
}))

const store = computed(() => storeOf(props.node))
const setStore = (fields: Partial<DraftStore>) => update({ store: { ...store.value, ...fields } })
function setLine(index: number, fields: { tiers?: string[]; types?: string[] }) {
  setStore({ items: store.value.items.map((line, i) => (i === index ? { ...line, ...fields } : line)) })
}
const uniqueOn = computed({
  get: () => !!store.value.unique,
  set: (on: boolean) => setStore({ unique: on ? { tier: 'mundane', count: 1 } : undefined }),
})

type ListKey = 'sideQuests' | 'ventureForth'
const list = (key: ListKey) => (props.node[key] as DraftStoryExit[] | undefined) ?? []
function setExit(key: ListKey, index: number, fields: Partial<DraftStoryExit>) {
  update({ [key]: list(key).map((exit, i) => (i === index ? { ...exit, ...fields } : exit)) })
}
function addExit(key: ListKey) {
  update({ [key]: [...list(key), { label: setText(undefined, language.value, ''), node: '' }] })
}
function removeExit(key: ListKey, index: number) {
  update({ [key]: list(key).filter((_, i) => i !== index) })
}
/** A Side Quest's id: the name remembered once attempted, made from its label unless the author gave one. */
const slug = (label: string) =>
  label
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
</script>

<template>
  <div class="story-round">
    <TextField v-model="name" :label="t('workshop.storyRoundSheet.name')" />
    <TextField v-model="narrative" :label="t('workshop.narrative')" long :rows="6" />

    <fieldset>
      <legend>{{ t('workshop.storyRoundSheet.store') }}</legend>
      <div v-for="(line, i) in store.items" :key="i" class="row">
        <MultiSelect
          :model-value="line.tiers ?? []"
          :options="tiers"
          option-label="label"
          option-value="value"
          :placeholder="t('workshop.storyRoundSheet.anyTier')"
          :aria-label="t('workshop.storyRoundSheet.lineTiers', { n: i + 1 })"
          size="small"
          @update:model-value="setLine(i, { tiers: $event })"
        />
        <MultiSelect
          :model-value="line.types ?? []"
          :options="types"
          option-label="label"
          option-value="value"
          :placeholder="t('workshop.storyRoundSheet.anyType')"
          :aria-label="t('workshop.storyRoundSheet.lineTypes', { n: i + 1 })"
          size="small"
          @update:model-value="setLine(i, { types: $event })"
        />
        <Button
          icon="pi pi-times"
          size="small"
          text
          severity="secondary"
          :aria-label="t('workshop.storyRoundSheet.removeLine', { n: i + 1 })"
          @click="setStore({ items: store.items.filter((_, j) => j !== i) })"
        />
      </div>
      <Button
        :label="t('workshop.storyRoundSheet.addLine')"
        icon="pi pi-plus"
        size="small"
        text
        @click="setStore({ items: [...store.items, { tiers: [], types: [] }] })"
      />
      <div class="toggle">
        <ToggleSwitch
          :model-value="!!store.upgrades"
          input-id="sr-upgrades"
          @update:model-value="setStore({ upgrades: $event || undefined })"
        />
        <label for="sr-upgrades">{{ t('workshop.storyRoundSheet.upgrades') }}</label>
      </div>
      <div class="toggle">
        <ToggleSwitch v-model="uniqueOn" input-id="sr-unique" />
        <label for="sr-unique">{{ t('workshop.storyRoundSheet.unique') }}</label>
        <template v-if="store.unique">
          <InputNumber
            :model-value="store.unique.count"
            :min="1"
            :max="5"
            size="small"
            input-class="count"
            :aria-label="t('workshop.storyRoundSheet.uniqueCount')"
            @update:model-value="setStore({ unique: { ...store.unique!, count: $event ?? 1 } })"
          />
          <Select
            :model-value="store.unique.tier"
            :options="tiers"
            option-label="label"
            option-value="value"
            size="small"
            :aria-label="t('workshop.storyRoundSheet.uniqueTier')"
            @update:model-value="setStore({ unique: { ...store.unique!, tier: $event } })"
          />
        </template>
      </div>
    </fieldset>

    <fieldset v-for="key in ['sideQuests', 'ventureForth'] as const" :key="key">
      <legend>{{ t(`workshop.storyRoundSheet.${key}`) }}</legend>
      <p class="hint">{{ t(`workshop.storyRoundSheet.${key}Hint`) }}</p>
      <div v-for="(exit, i) in list(key)" :key="i" class="row">
        <InputText
          :model-value="pick(exit.label, language)"
          size="small"
          :aria-label="t('workshop.storyRoundSheet.exitLabel', { n: i + 1 })"
          @update:model-value="
            (v) =>
              setExit(key, i, {
                label: setText(exit.label, language, v ?? ''),
                ...(key === 'sideQuests' ? { id: slug(v ?? '') || undefined } : {}),
              })
          "
        />
        <TargetSelect
          :model-value="exit.node || undefined"
          :scenario="scenario"
          :from="node.id"
          :label="t('workshop.storyRoundSheet.exitTarget', { n: i + 1 })"
          @update:model-value="(v) => setExit(key, i, { node: v ?? '' })"
        />
        <InputNumber
          :model-value="exit.page ?? null"
          :min="1"
          size="small"
          input-class="count"
          :placeholder="t('workshop.storyRoundSheet.page')"
          :aria-label="t('workshop.storyRoundSheet.exitPage', { n: i + 1 })"
          @update:model-value="setExit(key, i, { page: $event ?? undefined })"
        />
        <Button
          icon="pi pi-times"
          size="small"
          text
          severity="secondary"
          :aria-label="t('workshop.storyRoundSheet.removeExit', { n: i + 1 })"
          @click="removeExit(key, i)"
        />
      </div>
      <Button
        :label="t(`workshop.storyRoundSheet.add_${key}`)"
        icon="pi pi-plus"
        size="small"
        text
        @click="addExit(key)"
      />
    </fieldset>

    <div class="field">
      <label for="sr-bounty">{{ t('workshop.storyRoundSheet.bounty') }}</label>
      <InputNumber
        :model-value="(node.bountyDifficulty as number | undefined) ?? null"
        input-id="sr-bounty"
        :min="1"
        :max="5"
        show-buttons
        size="small"
        :placeholder="t('workshop.storyRoundSheet.noBounty')"
        @update:model-value="update({ bountyDifficulty: $event ?? undefined })"
      />
    </div>
  </div>
</template>

<style scoped>
.story-round {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
fieldset {
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  padding: 0.5rem 0.75rem;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
legend {
  font-weight: 600;
  padding: 0 0.3rem;
}
.row,
.toggle {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.row > :deep(.p-multiselect),
.row > :deep(.p-inputtext) {
  flex: 1;
  min-width: 8rem;
}
.hint {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
:deep(.count) {
  width: 4rem;
}
</style>
