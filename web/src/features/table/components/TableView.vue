<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import type { Combatant } from '@/features/catalog'
import type { Cell, EncounterCommand, EncounterState } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useBoardPieces } from '../api/queries'
import { boardLayout, type Selection } from '../lib/layout'
import BoardCanvas from './BoardCanvas.vue'
import TableSelection from './TableSelection.vue'

/**
 * The table: the board, what is selected on it and what to do with it. Used alone (table page)
 * or beside the GM's panels (play page). It only speaks in encounter commands.
 */
const props = defineProps<{
  node: ScenarioNode | undefined
  encounter: EncounterState
  cards: Map<string, Combatant>
  /** Reports are refused right now (a question other than an AI step is pending). Moves are always allowed. */
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const pieces = useBoardPieces()
const layout = computed(() => boardLayout(props.node, props.encounter, pieces.data.value))

const selected = ref<Selection | null>(null)
// Forget a selection that left the board (defeated, token removed, area revealed).
watch(layout, (now) => {
  const s = selected.value
  if (!s) return
  const still =
    s.kind === 'figure'
      ? now.figures.some((f) => f.id === s.id) || now.unplacedFigures.some((f) => f.id === s.id)
      : s.kind === 'token'
        ? now.tokens.some((tk) => tk.id === s.id)
        : now.tiles.some((tile) => tile.hidden?.id === s.id)
  if (!still) selected.value = null
})

const selectedUnplaced = computed(() =>
  selected.value?.kind === 'figure' ? layout.value.unplacedFigures.find((f) => f.id === selected.value?.id) : undefined,
)

function move(figure: string, to: Cell) {
  emit('command', { type: 'move', figure, to })
}

/** A tap on an empty square moves the selected figure there (the touch-friendly way to move). */
function tap(cell: Cell) {
  if (selected.value?.kind === 'figure') move(selected.value.id, cell)
  else selected.value = null
}
</script>

<template>
  <div class="table">
    <div class="stage">
      <Skeleton v-if="pieces.isPending.value" height="100%" />
      <Message v-else-if="pieces.isError.value" severity="warn">{{ t('table.unavailable') }}</Message>
      <BoardCanvas
        v-else
        class="board"
        :layout="layout"
        :selected="selected"
        :label="t('table.title', { name: pick(node?.name, language) })"
        @select="selected = $event"
        @move="move"
        @tap="tap"
      />
      <TableSelection
        v-if="selected"
        class="overlay"
        :selection="selected"
        :layout="layout"
        :encounter="encounter"
        :node="node"
        :cards="cards"
        :locked="locked"
        @command="emit('command', $event)"
        @close="selected = null"
      />
    </div>

    <div class="below">
      <p v-if="selectedUnplaced" class="hint">{{ t('table.tapToPlace', { name: selectedUnplaced.name }) }}</p>
      <div v-if="layout.unplacedFigures.length" class="unplaced">
        <span>{{ t('table.unplacedFigures') }}</span>
        <Button
          v-for="figure in layout.unplacedFigures"
          :key="figure.id"
          :label="figure.name"
          size="small"
          :severity="figure.kind === 'adventurer' ? 'info' : 'warn'"
          :outlined="selected?.id !== figure.id"
          @click="selected = { kind: 'figure', id: figure.id }"
        />
      </div>
      <div v-if="layout.unplacedTokens.length" class="unplaced">
        <span>{{ t('table.unplacedTokens') }}</span>
        <Tag v-for="tk in layout.unplacedTokens" :key="tk.id" :value="tk.key" severity="info" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.table {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-height: 0;
}
.stage {
  position: relative;
  flex: 1;
  min-height: 12rem;
}
.board {
  height: 100%;
}
/* Over the board, so that selecting never resizes it under the GM's finger. */
.overlay {
  position: absolute;
  left: 0.5rem;
  bottom: 0.5rem;
  max-width: min(28rem, calc(100% - 4.5rem));
  max-height: calc(100% - 1rem);
  overflow-y: auto;
  box-shadow: 0 4px 16px rgb(0 0 0 / 0.35);
}
.below {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.unplaced {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  align-items: center;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.hint {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
