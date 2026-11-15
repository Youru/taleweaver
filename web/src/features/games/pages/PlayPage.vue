<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { BoardMap } from '@/features/board'
import { useCombatants } from '@/features/catalog'
import { useScenario } from '@/features/scenarios'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useGame, useGameCommand } from '../api/queries'
import type { Answer, EncounterCommand } from '../api/types'
import EncounterPanel from '../components/EncounterPanel.vue'
import GameJournal from '../components/GameJournal.vue'
import PromptPanel from '../components/PromptPanel.vue'

const props = defineProps<{ id: string }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const game = useGame(() => props.id)
const state = computed(() => game.data.value?.state)
const scenario = useScenario(() => game.data.value?.scenario)
const node = computed(() => scenario.data.value?.nodes.find((n) => n.id === state.value?.encounter?.node))
const cards = useCombatants(() => state.value?.encounter?.combatants.map((c) => c.card) ?? [])

const command = useGameCommand(() => props.id)
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
      <header class="bar">
        <h1>{{ pick(scenario.data.value?.title, language) || game.data.value.scenario }}</h1>
        <span>{{ t('play.gold', { gold: state.campaign.gold }) }}</span>
        <span v-for="adventurer in state.campaign.party" :key="adventurer.name">
          {{ adventurer.name }} · {{ t('play.xp', { xp: adventurer.xp }) }}
        </span>
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

      <div class="columns">
        <div class="left">
          <BoardMap
            v-if="state.encounter"
            :node="node"
            :encounter="state.encounter"
            :cards="cards"
            :locked="reportsLocked"
            @activate-token="(token) => encounterCommand({ type: 'activateToken', token })"
          />
          <GameJournal :log="state.log" class="journal" />
        </div>
        <div class="right">
          <PromptPanel
            v-if="state.prompt"
            :key="state.log.length"
            :prompt="state.prompt"
            :pending="command.isPending.value"
            @answer="answer"
          />
          <Message v-if="state.status === 'Finished'" severity="success">{{
            t('play.finished', { ending: state.ending ?? '' })
          }}</Message>
          <EncounterPanel
            v-if="state.encounter"
            :encounter="state.encounter"
            :node="node"
            :cards="cards"
            :blocked="!!state.prompt"
            :ai-question="state.prompt?.kind === 'aiCondition'"
            :pending="command.isPending.value"
            @command="encounterCommand"
          />
        </div>
      </div>
    </template>
  </main>
</template>

<style scoped>
.play {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  height: calc(100vh - 4.5rem);
  padding: 0 1rem 1rem;
}
.center {
  display: grid;
  place-items: center;
  flex: 1;
}
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1rem;
}
.bar h1 {
  margin: 0;
  font-size: 1.4rem;
  flex: 1;
}
.columns {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 1rem;
  flex: 1;
  min-height: 0;
}
.left,
.right {
  min-height: 0;
  overflow-y: auto;
}
.left {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.journal {
  flex: 1 0 14rem;
}
.right {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
@media (max-width: 900px) {
  .play {
    height: auto;
  }
  .columns {
    grid-template-columns: 1fr;
  }
  .right {
    order: -1;
  }
  .left {
    max-height: 60vh;
  }
}
</style>
