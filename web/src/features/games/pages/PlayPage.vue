<script setup lang="ts">
import { computed, nextTick, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import { useAiAdvice } from '@/features/ai'
import { useCombatants } from '@/features/catalog'
import { DuelDialog } from '@/features/duel'
import { useScenario } from '@/features/scenarios'
import { boardLayout, TableSelection, TableView, useBoardPieces, type Selection } from '@/features/table'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useGame, useGameCommand } from '../api/queries'
import type { Answer, Cell, EncounterCommand } from '../api/types'
import CombatantList from '../components/encounter/CombatantList.vue'
import EncounterBar from '../components/encounter/EncounterBar.vue'
import EncounterGoals from '../components/encounter/EncounterGoals.vue'
import TurnCard from '../components/encounter/TurnCard.vue'
import GameJournal from '../components/GameJournal.vue'
import PromptPanel from '../components/PromptPanel.vue'
import { encounterOver as isEncounterOver, storyPage } from '../lib/storyPage'

/**
 * The GM's view. In an encounter: the table in the middle, and beside it the turn being played
 * (always in view) above tabs for the combatants, the journal and the encounter's goals.
 * Between encounters: the story, read like a book, with its question.
 */
const props = defineProps<{ id: string }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const game = useGame(() => props.id)
const state = computed(() => game.data.value?.state)
const encounter = computed(() => state.value?.encounter)
// Between encounters: the page being read (the whole journal is in its own tab)
const storyTab = ref('page')
const page = computed(() => storyPage(state.value?.log ?? []))
/** An end condition was reached: what follows (grade, reward, choice) is the story again, not the table. */
const encounterOver = computed(() => !!encounter.value && isEncounterOver(state.value?.log ?? []))
const scenario = useScenario(() => game.data.value?.scenario)
const node = computed(() => scenario.data.value?.nodes.find((n) => n.id === encounter.value?.node))
const cards = useCombatants(() => encounter.value?.combatants.map((c) => c.card) ?? [])

const command = useGameCommand(() => props.id)

// The AI helper reads the board as the table draws it.
const pieces = useBoardPieces()
const layout = computed(() =>
  encounter.value ? boardLayout(node.value, encounter.value, pieces.data.value) : undefined,
)
/** Figures on the table (placed, not under the fog). */
const shownFigures = computed(() => (layout.value ? new Set(layout.value.figures.map((f) => f.id)) : undefined))
const ai = useAiAdvice(
  () => props.id,
  layout,
  encounter,
  cards,
  () => state.value?.log ?? [],
  () => state.value?.prompt,
)
const commandError = computed(() => {
  const error = command.error.value
  return error instanceof ApiError ? error.message : error ? String(error) : null
})

function answer(value: Answer) {
  command.mutate({ kind: 'answer', answer: value })
}

function encounterCommand(value: EncounterCommand) {
  command.mutate({ kind: 'encounter', command: value })
}

/** Board reports (damage, tokens…) are refused while a question other than an AI step is pending. */
const reportsLocked = computed(
  () => command.isPending.value || (!!state.value?.prompt && state.value.prompt.kind !== 'aiCondition'),
)

const selected = ref<Selection | null>(null)
const tab = ref('combatants')
// What is picked on the table opens its actions under the combatants, scrolled into view
const selectionPanel = useTemplateRef<{ $el: HTMLElement }>('selectionPanel')
watch(selected, async (now) => {
  if (!now) return
  tab.value = 'combatants'
  await nextTick()
  selectionPanel.value?.$el.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' })
})
/** The duel window, the figure it opened with, and the target the AI picked if any. */
const duel = ref<string | null>(null)
const duelTarget = ref<string | null>(null)
const duelSpell = ref(false)
const dueling = ref(false)

function openDuel(figure: string, target?: string, spell = false) {
  duel.value = figure
  duelTarget.value = target ?? null
  duelSpell.value = spell
  dueling.value = true
}

/** "@victory" and "@defeat", the workshop's endings, in words; any other ending as it is. */
function endingLabel(ending: string | undefined) {
  if (ending === '@victory') return t('play.endVictory')
  if (ending === '@defeat') return t('play.endDefeat')
  return ending ?? ''
}

function moveFigure(figure: string, to: Cell) {
  encounterCommand({ type: 'move', figure, to })
}
</script>

<template>
  <main class="play">
    <div v-if="game.isPending.value" class="center">
      <ProgressSpinner :aria-label="t('common.loading')" />
    </div>
    <Message v-else-if="game.isError.value" severity="error">
      {{ t('common.error', { message: game.error.value?.message ?? '' }) }}
    </Message>
    <template v-else-if="state && game.data.value">
      <header class="top">
        <h1>{{ pick(scenario.data.value?.title, language) || game.data.value.scenario }}</h1>
        <span>{{ t('play.gold', { gold: state.campaign.gold }) }}</span>
        <span v-for="adventurer in state.campaign.party" :key="adventurer.name">
          {{ adventurer.name }} · {{ t('play.xp', { xp: adventurer.xp }) }}
        </span>
        <RouterLink v-if="encounter" :to="{ name: 'table', params: { id } }" target="_blank">
          <Button icon="pi pi-external-link" :label="t('table.openTable')" severity="secondary" text />
        </RouterLink>
        <Button
          icon="pi pi-undo"
          :label="t('play.undo')"
          severity="secondary"
          text
          :disabled="!game.data.value.canUndo || command.isPending.value"
          @click="command.mutate({ kind: 'undo' })"
        />
      </header>
      <Message v-if="commandError" severity="error" closable @close="command.reset()">{{ commandError }}</Message>

      <!-- Encounter: table in the middle, turn and tabs beside it -->
      <template v-if="encounter && !encounterOver">
        <EncounterBar
          :encounter="encounter"
          :node="node"
          :disabled="!!state.prompt || command.isPending.value"
          @next-turn="encounterCommand({ type: 'nextTurn' })"
        />
        <div class="encounter">
          <TableView
            v-model:selected="selected"
            class="table"
            :node="node"
            :encounter="encounter"
            :cards="cards"
            :locked="reportsLocked"
            :hint="ai.hint.value"
            selection-outside
            @command="encounterCommand"
            @duel="openDuel"
          />
          <aside class="side">
            <TurnCard
              class="turn"
              :encounter="encounter"
              :log="state.log"
              :prompt="state.prompt"
              :cards="cards"
              :pending="command.isPending.value"
              :suggestion="ai.condition.value"
              :advice="ai.advice.value"
              @answer="answer"
              @duel="openDuel"
              @move="moveFigure"
            />
            <Tabs v-model:value="tab" class="tabs">
              <TabList>
                <Tab value="combatants">{{ t('play.tabCombatants') }}</Tab>
                <Tab value="journal">{{ t('play.journal') }}</Tab>
                <Tab value="goals">{{ t('play.tabEncounter') }}</Tab>
              </TabList>
              <TabPanels>
                <TabPanel value="combatants">
                  <CombatantList
                    :encounter="encounter"
                    :cards="cards"
                    :shown="shownFigures"
                    :selected="selected?.kind === 'figure' ? selected.id : undefined"
                    @select="selected = { kind: 'figure', id: $event }"
                  />
                  <TableSelection
                    v-if="selected && layout"
                    ref="selectionPanel"
                    class="selection"
                    :selection="selected"
                    :layout="layout"
                    :encounter="encounter"
                    :node="node"
                    :cards="cards"
                    :locked="reportsLocked"
                    @command="encounterCommand"
                    @duel="openDuel"
                    @close="selected = null"
                  />
                </TabPanel>
                <TabPanel value="journal">
                  <GameJournal :log="state.log" class="journal" />
                </TabPanel>
                <TabPanel value="goals">
                  <EncounterGoals
                    :encounter="encounter"
                    :node="node"
                    :locked="reportsLocked"
                    @command="encounterCommand"
                  />
                </TabPanel>
              </TabPanels>
            </Tabs>
          </aside>
        </div>
        <DuelDialog
          v-model:open="dueling"
          :game="id"
          :encounter="encounter"
          :node="node"
          :cards="cards"
          :from="duel"
          :to="duelTarget"
          :spell="duelSpell"
          :locked="reportsLocked"
          @command="encounterCommand"
        />
      </template>

      <!-- Between encounters: the story -->
      <div v-else class="story">
        <Tabs v-model:value="storyTab" class="tabs">
          <TabList>
            <Tab value="page">{{ t('play.story') }}</Tab>
            <Tab value="journal">{{ t('play.journal') }}</Tab>
          </TabList>
          <TabPanels>
            <TabPanel value="page" class="page-panel">
              <GameJournal :log="page" page class="journal" />
            </TabPanel>
            <TabPanel value="journal" class="page-panel">
              <GameJournal :log="state.log" class="journal" />
            </TabPanel>
          </TabPanels>
        </Tabs>
        <PromptPanel
          v-if="state.prompt"
          :key="state.log.length"
          :prompt="state.prompt"
          :pending="command.isPending.value"
          @answer="answer"
        />
        <Message v-if="state.status === 'Finished'" severity="success">
          {{ t('play.finished', { ending: endingLabel(state.ending) }) }}
        </Message>
      </div>
    </template>
  </main>
</template>

<style scoped>
.play {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  height: calc(100vh - 4.5rem);
  padding: 0 1rem 1rem;
}
.center {
  display: grid;
  place-items: center;
  flex: 1;
}
.top {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem 1rem;
}
.top h1 {
  margin: 0;
  font-size: 1.3rem;
  flex: 1;
}
.encounter {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(22rem, 28rem);
  gap: 1rem;
  flex: 1;
  min-height: 0;
}
.table {
  min-height: 0;
}
.side {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 0;
}
.turn {
  flex: 0 1 auto;
  max-height: 50%;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
.tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.tabs :deep(.p-tabpanels) {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 0.75rem 0 0;
}
.tabs :deep(.p-tablist-tab-list) {
  background: transparent;
}
.journal {
  height: 100%;
}
.selection {
  margin-top: 0.75rem;
}
.story {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
  width: min(52rem, 100%);
  margin: 0 auto;
}
.story .tabs :deep(.p-tabpanels) {
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
/* PrimeVue's panel does not carry this page's scope attribute: reached through :deep */
.story :deep(.page-panel) {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.story .journal {
  flex: 1;
  min-height: 0;
}
@media (max-width: 1000px) {
  .play {
    height: auto;
  }
  .encounter {
    grid-template-columns: 1fr;
  }
  .table {
    /* Boards are wider than tall: no more height than the width needs. */
    height: min(60vh, 80vw);
  }
  .turn {
    max-height: none;
  }
  .tabs :deep(.p-tabpanels) {
    max-height: 60vh;
  }
}
</style>
