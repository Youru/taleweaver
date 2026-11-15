<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import { useCombatants } from '@/features/catalog'
import { useGame, useGameCommand, type EncounterCommand } from '@/features/games'
import { useScenario } from '@/features/scenarios'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import TableView from '../components/TableView.vue'

/** The table alone, e.g. on a tablet in the middle of the game: it follows the game played from the GM's screen. */
const props = defineProps<{ id: string }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const game = useGame(() => props.id, { refreshEvery: 2000 })
const state = computed(() => game.data.value?.state)
const encounter = computed(() => state.value?.encounter)
const scenario = useScenario(() => game.data.value?.scenario)
const node = computed(() => scenario.data.value?.nodes.find((n) => n.id === encounter.value?.node))
const cards = useCombatants(() => encounter.value?.combatants.map((c) => c.card) ?? [])
const command = useGameCommand(() => props.id)
const locked = computed(
  () => command.isPending.value || (!!state.value?.prompt && state.value.prompt.kind !== 'aiCondition'),
)
const commandError = computed(() => {
  const error = command.error.value
  return error instanceof ApiError ? error.message : error ? String(error) : null
})

function run(value: EncounterCommand) {
  command.mutate({ kind: 'encounter', command: value })
}
</script>

<template>
  <main class="table-page">
    <div v-if="game.isPending.value" class="center">
      <ProgressSpinner :aria-label="t('common.loading')" />
    </div>
    <Message v-else-if="game.isError.value" severity="error">
      {{ t('common.error', { message: game.error.value?.message ?? '' }) }}
    </Message>
    <template v-else>
      <header>
        <h1>{{ pick(node?.name, language) || pick(scenario.data.value?.title, language) }}</h1>
        <template v-if="encounter">
          <Tag
            :value="encounter.round > 0 ? t('play.round', { round: encounter.round }) : t('play.notStarted')"
            severity="secondary"
          />
          <Tag
            :value="t('play.urgency', { urgency: encounter.urgency })"
            :severity="encounter.urgency >= 3 ? 'danger' : 'warn'"
          />
          <Tag v-if="encounter.activation" :value="t('table.acting', { name: encounter.activation.figures[0] })" />
        </template>
        <RouterLink :to="{ name: 'play', params: { id } }">
          <Button :label="t('table.gmView')" icon="pi pi-book" severity="secondary" text />
        </RouterLink>
      </header>
      <Message v-if="commandError" severity="error" closable @close="command.reset()">{{ commandError }}</Message>
      <TableView
        v-if="encounter"
        class="view"
        :node="node"
        :encounter="encounter"
        :cards="cards"
        :locked="locked"
        @command="run"
      />
      <Message v-else severity="info">{{ t('table.noEncounter') }}</Message>
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
