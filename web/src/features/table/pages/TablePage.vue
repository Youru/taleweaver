<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useAiAdvice } from '@/features/ai'
import { useCombatants } from '@/features/catalog'
import { DuelDialog } from '@/features/duel'
import {
  arrivedCombatants,
  cardsInPlay,
  CountersBar,
  EncounterBar,
  PauseScreen,
  useGame,
  useGameCommand,
  urgencyMax,
  type EncounterCommand,
} from '@/features/games'
import { useScenario } from '@/features/scenarios'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useBoardPieces } from '../api/queries'
import TableView from '../components/TableView.vue'
import { boardLayout } from '../lib/layout'

/** The table alone, e.g. on a tablet in the middle of the game: it follows the game played from the GM's screen. */
const props = defineProps<{ id: string }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const game = useGame(() => props.id, { refreshEvery: 2000 })
const state = computed(() => game.data.value?.state)
const encounter = computed(() => state.value?.encounter)
const arrived = computed(() => arrivedCombatants(state.value?.log))
const scenario = useScenario(() => game.data.value?.state.bounty?.scenario ?? game.data.value?.scenario)
const node = computed(() => scenario.data.value?.nodes.find((n) => n.id === encounter.value?.node))
const cards = useCombatants(() =>
  encounter.value ? cardsInPlay(encounter.value, state.value?.campaign.party ?? []) : [],
)
const command = useGameCommand(() => props.id)
// The AI's plan for the acting figure, drawn on the board as on the GM's screen.
const pieces = useBoardPieces()
const ai = useAiAdvice(
  () => props.id,
  () => (encounter.value ? boardLayout(node.value, encounter.value, pieces.data.value) : undefined),
  encounter,
  cards,
  () => state.value?.log ?? [],
  () => state.value?.prompt,
)
const locked = computed(
  () => command.isPending.value || (!!state.value?.prompt && state.value.prompt.kind !== 'aiCondition'),
)
const commandError = computed(() => {
  const error = command.error.value
  return error instanceof ApiError ? error.message : error ? String(error) : null
})

/** The duel window, and the figure it opened with. */
const duel = ref<string | null>(null)
const dueling = ref(false)

function openDuel(figure: string) {
  duel.value = figure
  dueling.value = true
}

// The last blow may end the encounter before the duel closes
watch(encounter, (now) => {
  if (!now) dueling.value = false
})

function run(value: EncounterCommand) {
  command.mutate({ kind: 'encounter', command: value })
}
</script>

<template>
  <PauseScreen v-if="game.data.value?.table.paused" />
  <main class="table-page">
    <div v-if="game.isPending.value" class="center">
      <ProgressSpinner :aria-label="t('common.loading')" />
    </div>
    <Message v-else-if="game.isError.value" severity="error">
      {{ t('common.error', { message: game.error.value?.message ?? '' }) }}
    </Message>
    <template v-else>
      <header>
        <h1>{{ pick(scenario.data.value?.title, language) }}</h1>
        <RouterLink :to="{ name: 'play', params: { id } }">
          <Button :label="t('table.gmView')" icon="pi pi-book" severity="secondary" text />
        </RouterLink>
      </header>
      <EncounterBar
        v-if="encounter"
        :urgency-max="urgencyMax(state?.options)"
        :encounter="encounter"
        :node="node"
        readonly
        movable
        :busy="command.isPending.value"
        @move-card="(card, by) => run({ type: 'moveInitiative', card, by })"
      />
      <CountersBar v-if="encounter" :counters="state?.campaign.counters" />
      <Message v-if="commandError" severity="error" closable @close="command.reset()">{{ commandError }}</Message>
      <TableView
        v-if="encounter"
        class="view"
        :node="node"
        :encounter="encounter"
        :cards="cards"
        :locked="locked"
        :hint="ai.hint.value"
        :arrived="arrived"
        :party="state?.campaign.party"
        for-players
        @command="run"
        @duel="openDuel"
      >
        <template #head>
          <EncounterBar :urgency-max="urgencyMax(state?.options)" :encounter="encounter" :node="node" readonly />
          <CountersBar :counters="state?.campaign.counters" />
        </template>
      </TableView>
      <Message v-else severity="info">{{ t('table.noEncounter') }}</Message>
      <DuelDialog
        v-if="encounter"
        v-model:open="dueling"
        :game="id"
        :encounter="encounter"
        :node="node"
        :cards="cards"
        :from="duel"
        :party="state?.campaign.party"
        :log="state?.log ?? []"
        :error="commandError"
        :locked="locked"
        :hint="ai.hint.value"
        @command="run"
      />
    </template>
  </main>
</template>

<style scoped>
.table-page {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  height: calc(100vh - 4.5rem);
  padding: 0 1rem 1rem;
}
.center {
  display: grid;
  place-items: center;
  flex: 1;
}
header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}
h1 {
  margin: 0;
  font-size: 1.3rem;
  flex: 1;
}
.view {
  flex: 1;
}
</style>
