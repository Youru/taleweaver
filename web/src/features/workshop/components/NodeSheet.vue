<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import Message from 'primevue/message'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import type { BoardPieces } from '@/features/table'
import { pick, type LocalizedText } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftEnd, DraftNode, DraftRule, DraftScenario, ScenarioIssue } from '../api/types'
import { choiceOf, exitsOf, setChoice, setExit, setText, text, withChoice } from '../lib/graph'
import { mapOf, setupOf } from '../lib/map'
import BoardRules from './BoardRules.vue'
import { eventsOf } from '../lib/events'
import TargetSelect from './TargetSelect.vue'
import TextField from './TextField.vue'

/**
 * The sheet of a scene (title, text read aloud, what comes next or the players' choice) or of an
 * encounter (name, introduction, loot level, special rules, end conditions and where they lead).
 */
const props = defineProps<{
  scenario: DraftScenario
  node: DraftNode
  issues: ScenarioIssue[]
  warnings?: ScenarioIssue[]
  pieces?: BoardPieces
}>()
const emit = defineEmits<{ change: [node: DraftNode]; remove: []; start: []; map: []; print: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

function patch(fields: Partial<DraftNode>) {
  emit('change', { ...props.node, ...fields })
}
const field = <K extends keyof DraftNode>(key: K) =>
  computed({
    get: () => props.node[key],
    set: (value: DraftNode[K]) => patch({ [key]: value } as Partial<DraftNode>),
  })

const tab = ref('sheet')
const title = field('title')
const name = field('name')
const narrative = field('narrative')
const intro = field('intro')
const exits = computed(() => exitsOf(props.node))

// Scene: one next, or a choice of the players
const choice = computed(() => choiceOf(props.node))
const choosing = computed({
  get: () => !!choice.value,
  set: (on: boolean) =>
    emit(
      'change',
      setChoice(props.node, on, {
        question: text(language.value, t('workshop.defaultQuestion')),
        option: (n) => text(language.value, t('workshop.optionN', { n })),
      }),
    ),
})
const question = computed({
  get: () => choice.value?.question,
  set: (value: LocalizedText | undefined) =>
    emit(
      'change',
      withChoice(props.node, (c) => ({ ...c, question: value })),
    ),
})
function optionLabel(index: number) {
  return pick(choice.value?.options?.[index]?.label, language.value)
}
function setOptionLabel(index: number, value: string) {
  emit(
    'change',
    withChoice(props.node, (c) => ({
      ...c,
      options: (c.options ?? []).map((o, i) =>
        i === index ? { ...o, label: setText(o.label, language.value, value) } : o,
      ),
    })),
  )
}
function addOption() {
  emit(
    'change',
    withChoice(props.node, (c) => ({
      ...c,
      options: [
        ...(c.options ?? []),
        { label: text(language.value, t('workshop.optionN', { n: (c.options?.length ?? 0) + 1 })), actions: [] },
      ],
    })),
  )
}
function removeOption(index: number) {
  emit(
    'change',
    withChoice(props.node, (c) => ({ ...c, options: (c.options ?? []).filter((_, i) => i !== index) })),
  )
}
function lead(key: string, target: string | undefined) {
  emit('change', setExit(props.node, key, target))
}
const targetOf = (key: string) => exits.value.find((e) => e.key === key)?.target

// Encounter
const lootLevels = ['mundane', 'common', 'uncommon', 'rare'].map((value) => ({
  value,
  label: t(`workshop.loot.${value}`),
}))
/** The location types met in the books; any other value of the file stays offered as it is. */
const locations = computed(() =>
  [...new Set(['wilderness', 'underground', ...(props.node.locationType ? [props.node.locationType] : [])])].map(
    (value) => ({
      value,
      label: value === 'wilderness' || value === 'underground' ? t(`workshop.location.${value}`) : value,
    }),
  ),
)
const ends = computed(() => props.node.endConditions ?? [])
function patchEnd(index: number, fields: Partial<DraftEnd>) {
  patch({ endConditions: ends.value.map((e, i) => (i === index ? { ...e, ...fields } : e)) })
}
function addEnd(kind: 'win' | 'lose') {
  const used = new Set(ends.value.map((e) => e.id))
  let n = 1
  while (used.has(`${kind}-${n}`)) n++
  patch({
    endConditions: [
      ...ends.value,
      { id: `${kind}-${n}`, kind, text: text(language.value, ''), next: kind === 'win' ? '@victory' : '@defeat' },
    ],
  })
}
const kinds = computed(() => [
  { value: 'win', label: t('workshop.win') },
  { value: 'lose', label: t('workshop.lose') },
])
const rules = computed(() => props.node.specialRules ?? [])
function patchRule(index: number, fields: Partial<DraftRule>) {
  patch({ specialRules: rules.value.map((r, i) => (i === index ? { ...r, ...fields } : r)) })
}
function addRule() {
  const used = new Set(rules.value.map((r) => r.id))
  let n = 1
  while (used.has(`rule-${n}`)) n++
  patch({
    specialRules: [...rules.value, { id: `rule-${n}`, name: text(language.value, ''), text: text(language.value, '') }],
  })
}
/** The board at a glance. */
const board = computed(() => ({ map: mapOf(props.node), setup: setupOf(props.node) }))
/** What the encounter keeps from the file that this sheet does not edit yet. */
const untouched = computed(() =>
  ['diagrams', 'hiddenRules'].filter((key) => {
    const value = props.node[key]
    return Array.isArray(value) ? value.length > 0 : !!value && Object.keys(value).length > 0
  }),
)
</script>

<template>
  <section class="sheet" :aria-label="t('workshop.sheetOf', { id: node.id })">
    <header>
      <span class="kind" :class="node.type">
        <i :class="node.type === 'encounter' ? 'pi pi-bolt' : 'pi pi-book'" aria-hidden="true" />
        {{ node.type === 'encounter' ? t('workshop.encounter') : t('workshop.scene') }}
      </span>
      <code>{{ node.id }}</code>
      <span class="spacer" />
      <Button
        v-if="scenario.start !== node.id"
        :label="t('workshop.makeStart')"
        icon="pi pi-play"
        size="small"
        text
        @click="emit('start')"
      />
      <span v-else class="start">{{ t('workshop.isStart') }}</span>
      <Button
        :aria-label="t('workshop.removeNode')"
        :title="t('workshop.removeNode')"
        icon="pi pi-trash"
        size="small"
        severity="danger"
        text
        @click="emit('remove')"
      />
    </header>

    <Message v-for="(issue, i) in issues" :key="i" severity="error" size="small">{{ issue.message }}</Message>
    <Message v-for="(issue, i) in warnings ?? []" :key="`w${i}`" severity="warn" size="small">{{
      issue.message
    }}</Message>

    <!-- Scene -->
    <template v-if="node.type !== 'encounter'">
      <TextField v-model="title" :label="t('workshop.sceneTitle')" />
      <TextField v-model="narrative" :label="t('workshop.narrative')" long :rows="8" />
      <div class="toggle">
        <ToggleSwitch v-model="choosing" input-id="choosing" />
        <label for="choosing">{{ t('workshop.choice') }}</label>
      </div>
      <template v-if="choosing">
        <TextField v-model="question" :label="t('workshop.question')" />
        <ol class="options">
          <li v-for="(exit, index) in exits" :key="exit.key">
            <InputText
              :model-value="optionLabel(index)"
              :aria-label="t('workshop.optionLabel', { n: index + 1 })"
              @update:model-value="(v) => setOptionLabel(index, v ?? '')"
            />
            <TargetSelect
              :model-value="exit.target"
              :scenario="scenario"
              :from="node.id"
              :label="t('workshop.optionTarget', { n: index + 1 })"
              @update:model-value="(v) => lead(exit.key, v)"
            />
            <Button
              icon="pi pi-times"
              size="small"
              severity="secondary"
              text
              :aria-label="t('workshop.removeOption', { n: index + 1 })"
              :disabled="exits.length <= 1"
              @click="removeOption(index)"
            />
          </li>
        </ol>
        <Button :label="t('workshop.addOption')" icon="pi pi-plus" size="small" text @click="addOption" />
      </template>
      <div v-else class="field">
        <span class="label">{{ t('workshop.next') }}</span>
        <TargetSelect
          :model-value="targetOf('next')"
          :scenario="scenario"
          :from="node.id"
          :label="t('workshop.next')"
          @update:model-value="(v) => lead('next', v)"
        />
      </div>
    </template>

    <!-- Encounter: its sheet, its tokens, its events -->
    <Tabs v-else v-model:value="tab">
      <TabList>
        <Tab value="sheet">{{ t('workshop.events.tabSheet') }}</Tab>
        <Tab value="tokens">{{ t('workshop.events.tabTokens') }}</Tab>
        <Tab value="events">{{ t('workshop.events.tabEvents', { n: eventsOf(node).length }) }}</Tab>
      </TabList>
      <TabPanels class="panels">
        <TabPanel value="tokens">
          <BoardRules
            :scenario="scenario"
            :node="node"
            :pieces="pieces"
            show="tokens"
            @change="(n) => emit('change', n)"
          />
        </TabPanel>
        <TabPanel value="events">
          <BoardRules
            :scenario="scenario"
            :node="node"
            :pieces="pieces"
            show="events"
            @change="(n) => emit('change', n)"
          />
        </TabPanel>
        <TabPanel value="sheet" class="encounter">
          <TextField v-model="name" :label="t('workshop.name')" />
          <TextField v-model="intro" :label="t('workshop.encounterIntro')" long />
          <div class="board">
            <i class="pi pi-map" aria-hidden="true" />
            <span>
              {{ t('workshop.map.countTiles', board.map.tiles.length) }} ·
              {{ t('workshop.map.countTokens', board.map.tokens.length) }} ·
              {{ t('workshop.map.countEnemies', board.setup.combatants.length) }}
            </span>
            <Button :label="t('workshop.map.open')" icon="pi pi-pencil" size="small" @click="emit('map')" />
            <Button
              icon="pi pi-print"
              size="small"
              severity="secondary"
              text
              :aria-label="t('workshop.print.encounterPage')"
              :title="t('workshop.print.encounterPage')"
              @click="emit('print')"
            />
          </div>
          <div class="row">
            <div class="field">
              <label for="loot">{{ t('workshop.lootLevel') }}</label>
              <Select
                input-id="loot"
                :model-value="node.lootLevel"
                :options="lootLevels"
                option-label="label"
                option-value="value"
                @update:model-value="(v) => patch({ lootLevel: v })"
              />
            </div>
            <div class="field">
              <label for="location">{{ t('workshop.locationType') }}</label>
              <Select
                input-id="location"
                :model-value="node.locationType"
                :options="locations"
                option-label="label"
                option-value="value"
                show-clear
                @update:model-value="(v) => patch({ locationType: v ?? undefined })"
              />
            </div>
          </div>

          <h3>{{ t('workshop.endConditions') }}</h3>
          <ol class="ends">
            <li v-for="(end, index) in ends" :key="end.id" :class="end.kind">
              <div class="end-head">
                <SelectButton
                  :model-value="end.kind"
                  :options="kinds"
                  option-label="label"
                  option-value="value"
                  :allow-empty="false"
                  :aria-label="t('workshop.endKind', { n: index + 1 })"
                  size="small"
                  @update:model-value="(v) => patchEnd(index, { kind: v })"
                />
                <Button
                  icon="pi pi-times"
                  size="small"
                  severity="secondary"
                  text
                  :aria-label="t('workshop.removeEnd', { n: index + 1 })"
                  @click="patch({ endConditions: ends.filter((_, i) => i !== index) })"
                />
              </div>
              <TextField
                :model-value="end.text"
                :label="t('workshop.endText')"
                @update:model-value="(v) => patchEnd(index, { text: v })"
              />
              <TextField
                :model-value="end.reward"
                :label="t('workshop.reward')"
                @update:model-value="(v) => patchEnd(index, { reward: v })"
              />
              <div class="field">
                <span class="label">{{ t('workshop.leadsTo') }}</span>
                <TargetSelect
                  :model-value="end.next"
                  :scenario="scenario"
                  :from="node.id"
                  :label="t('workshop.endTarget', { n: index + 1 })"
                  @update:model-value="(v) => patchEnd(index, { next: v })"
                />
              </div>
            </li>
          </ol>
          <div class="adds">
            <Button :label="t('workshop.addWin')" icon="pi pi-plus" size="small" text @click="addEnd('win')" />
            <Button :label="t('workshop.addLose')" icon="pi pi-plus" size="small" text @click="addEnd('lose')" />
          </div>

          <h3>{{ t('workshop.specialRules') }}</h3>
          <ol class="rules">
            <li v-for="(rule, index) in rules" :key="rule.id">
              <TextField
                :model-value="rule.name"
                :label="t('workshop.ruleName')"
                @update:model-value="(v) => patchRule(index, { name: v ?? {} })"
              />
              <TextField
                :model-value="rule.text"
                :label="t('workshop.ruleText')"
                long
                :rows="3"
                @update:model-value="(v) => patchRule(index, { text: v ?? {} })"
              />
              <Button
                :label="t('workshop.removeRule')"
                icon="pi pi-times"
                size="small"
                severity="secondary"
                text
                @click="patch({ specialRules: rules.filter((_, i) => i !== index) })"
              />
            </li>
          </ol>
          <Button :label="t('workshop.addRule')" icon="pi pi-plus" size="small" text @click="addRule" />

          <p v-if="untouched.length" class="later">
            <i class="pi pi-info-circle" aria-hidden="true" />
            {{ t('workshop.keptFromFile', { list: untouched.join(', ') }) }}
          </p>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </section>
</template>

<style scoped>
.sheet {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.kind {
  font-weight: 700;
  text-transform: uppercase;
  font-size: 0.75rem;
  letter-spacing: 0.05em;
}
.kind.encounter {
  color: #b45309;
}
.kind.scene {
  color: #6d28d9;
}
code {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
}
.spacer {
  flex: 1;
}
.start {
  font-size: 0.8rem;
  color: var(--p-primary-color);
  font-weight: 600;
}
.toggle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}
.field label,
.field .label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.row {
  display: flex;
  gap: 0.75rem;
}
h3 {
  margin: 0.5rem 0 0;
  font-size: 1rem;
}
ol {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
.options li {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 0.4rem;
  align-items: center;
}
.ends li,
.rules li {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.6rem 0.7rem;
  border-radius: var(--p-content-border-radius);
  border: 1px solid var(--p-content-border-color);
  border-left: 4px solid #a3a3a3;
}
.ends li.win {
  border-left-color: #16a34a;
}
.ends li.lose {
  border-left-color: #dc2626;
}
.end-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.adds {
  display: flex;
  gap: 0.5rem;
}
.panels {
  padding: 0.75rem 0 0;
}
.encounter {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.board {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.7rem;
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-hover-background);
  font-size: 0.85rem;
}
.board span {
  flex: 1;
}
.later {
  margin: 0.5rem 0 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
