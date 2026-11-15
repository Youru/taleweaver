<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useCombatants } from '@/features/catalog'
import { DuelPanel } from '@/features/duel'
import { EncounterBar, useGame, useGameCommand, type EncounterCommand } from '@/features/games'
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

/** The figure a duel was opened with, shown in a side drawer over the table. */
const duel = ref<string | null>(null)

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
        <h1>{{ pick(scenario.data.value?.title, language) }}</h1>
        <RouterLink :to="{ name: 'play', params: { id } }">
          <Button :label="t('table.gmView')" icon="pi pi-book" severity="secondary" text />
        </RouterLink>
      </header>
      <EncounterBar v-if="encounter" :encounter="encounter" :node="node" readonly />
      <Message v-if="commandError" severity="error" closable @close="command.reset()">{{ commandError }}</Message>
      <TableView
        v-if="encounter"
        class="view"
        :node="node"
        :encounter="encounter"
        :cards="cards"
        :locked="locked"
        @command="run"
        @duel="duel = $event"
      />
      <Message v-else severity="info">{{ t('table.noEncounter') }}</Message>
      <Drawer
        :visible="!!duel && !!encounter"
        position="right"
        :show-close-icon="false"
        class="duel-drawer"
        @update:visible="(open: boolean) => !open && (duel = null)"
      >
        <DuelPanel
          v-if="duel && encounter"
          :game="id"
          :encounter="encounter"
          :cards="cards"
          :from="duel"
          :locked="locked"
          @command="run"
          @close="duel = null"
        />
      </Drawer>
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
:global(.duel-drawer) {
  width: min(30rem, 100vw);
}
.view {
  flex: 1;
}
</style>
