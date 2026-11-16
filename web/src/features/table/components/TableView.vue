<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import type { TableHint } from '@/features/ai'
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
  /** The AI's plan for the acting figure, drawn on the board. */
  hint?: TableHint
  /** The table the players see: combatants still to place are never named (they would tell what is coming). */
  forPlayers?: boolean
  /** The page shows the selected piece's actions elsewhere (beside the table): none over the board. */
  selectionOutside?: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand]; duel: [figure: string] }>()

/** Full screen: the whole table (board, selection, figures to place), e.g. on a tablet. */
const root = useTemplateRef<HTMLElement>('root')
const fullScreen = ref(false)
const canFullScreen = typeof document !== 'undefined' && !!document.fullscreenEnabled
function syncFullScreen() {
  fullScreen.value = !!root.value && document.fullscreenElement === root.value
}
onMounted(() => document.addEventListener('fullscreenchange', syncFullScreen))
onBeforeUnmount(() => document.removeEventListener('fullscreenchange', syncFullScreen))
async function toggleFullScreen() {
  if (document.fullscreenElement) await document.exitFullscreen()
  else await root.value?.requestFullscreen()
}

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const pieces = useBoardPieces()
const layout = computed(() => boardLayout(props.node, props.encounter, pieces.data.value))

/** What is selected on the table; shared with the page (e.g. a combatant picked in a list). */
const selected = defineModel<Selection | null>('selected', { default: null })
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

/**
 * Figures to place shown under the board: the adventurers and the acting combatant; the other
 * combatants only when the GM asks (never on the players' table), so as not to tell what is coming.
 */
const showHidden = ref(false)
const hiddenUnplaced = computed(() =>
  layout.value.unplacedFigures.filter((f) => f.kind === 'combatant' && f.id !== props.encounter.activation?.figures[0]),
)
const toPlace = computed(() =>
  layout.value.unplacedFigures.filter(
    (f) => !hiddenUnplaced.value.includes(f) || (showHidden.value && !props.forPlayers),
  ),
)

const selectedUnplaced = computed(() =>
  selected.value?.kind === 'figure' ? layout.value.unplacedFigures.find((f) => f.id === selected.value?.id) : undefined,
)

const canvas = useTemplateRef<InstanceType<typeof BoardCanvas>>('canvas')

/** A figure "to place" dragged from its button onto the board (works with a finger too). */
const placing = ref<{ id: string; name: string; x: number; y: number; moved: boolean } | null>(null)

function startPlacing(event: PointerEvent, figure: { id: string; name: string }) {
  if (event.pointerType === 'mouse' && event.button !== 0) return
  event.preventDefault()
  const start = { x: event.clientX, y: event.clientY }
  placing.value = { ...figure, ...start, moved: false }
  const onMove = (e: PointerEvent) => {
    if (!placing.value) return
    const moved = placing.value.moved || Math.hypot(e.clientX - start.x, e.clientY - start.y) > 6
    placing.value = { ...placing.value, x: e.clientX, y: e.clientY, moved }
  }
  const onUp = (e: PointerEvent) => {
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    const current = placing.value
    placing.value = null
    if (!current) return
    const cell = current.moved ? canvas.value?.cellAtClient(e.clientX, e.clientY) : null
    selected.value = { kind: 'figure', id: current.id }
    if (cell) move(current.id, cell)
  }
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

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
  <div ref="root" class="table">
    <div class="stage">
      <Skeleton v-if="pieces.isPending.value" height="100%" />
      <Message v-else-if="pieces.isError.value" severity="warn">{{ t('table.unavailable') }}</Message>
      <BoardCanvas
        v-else
        ref="canvas"
        class="board"
        :layout="layout"
        :selected="selected"
        :hint="hint"
        :label="t('table.title', { name: pick(node?.name, language) })"
        @select="selected = $event"
        @move="move"
        @tap="tap"
      >
        <template #tools>
          <Button
            v-if="canFullScreen"
            :icon="fullScreen ? 'pi pi-window-minimize' : 'pi pi-window-maximize'"
            severity="secondary"
            text
            rounded
            size="small"
            :aria-label="fullScreen ? t('table.exitFullScreen') : t('table.fullScreen')"
            :title="fullScreen ? t('table.exitFullScreen') : t('table.fullScreen')"
            @click="toggleFullScreen"
          />
        </template>
      </BoardCanvas>
      <TableSelection
        v-if="selected && !selectionOutside"
        class="overlay"
        :selection="selected"
        :layout="layout"
        :encounter="encounter"
        :node="node"
        :cards="cards"
        :locked="locked"
        @command="emit('command', $event)"
        @duel="emit('duel', $event)"
        @close="selected = null"
      />
    </div>

    <div class="below">
      <p v-if="selectedUnplaced" class="hint">{{ t('table.tapToPlace', { name: selectedUnplaced.name }) }}</p>
      <div v-if="toPlace.length || (hiddenUnplaced.length && !forPlayers)" class="unplaced">
        <span v-if="toPlace.length">{{ t('table.unplacedFigures') }}</span>
        <Button
          v-for="figure in toPlace"
          :key="figure.id"
          :label="figure.name"
          size="small"
          :severity="figure.kind === 'adventurer' ? 'info' : 'warn'"
          :outlined="selected?.id !== figure.id"
          class="to-place"
          @pointerdown="startPlacing($event, figure)"
          @keydown.enter="selected = { kind: 'figure', id: figure.id }"
        />
        <Button
          v-if="hiddenUnplaced.length && !forPlayers"
          :label="showHidden ? t('table.hideHidden') : t('table.showHidden')"
          :icon="showHidden ? 'pi pi-eye-slash' : 'pi pi-eye'"
          size="small"
          severity="secondary"
          text
          @click="showHidden = !showHidden"
        />
      </div>
      <div v-if="layout.unplacedTokens.length" class="unplaced">
        <span>{{ t('table.unplacedTokens') }}</span>
        <Tag v-for="tk in layout.unplacedTokens" :key="tk.id" :value="tk.key" severity="info" />
      </div>
    </div>
    <div
      v-if="placing?.moved"
      class="ghost"
      :style="{ left: `${placing.x}px`, top: `${placing.y}px` }"
      aria-hidden="true"
    >
      {{ placing.name }}
    </div>
  </div>
</template>

<style scoped>
.table:fullscreen {
  padding: 0.75rem;
  background: var(--p-content-background);
}
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
.to-place {
  touch-action: none;
  cursor: grab;
}
.ghost {
  position: fixed;
  z-index: 10;
  transform: translate(-50%, -50%);
  padding: 0.3rem 0.7rem;
  border-radius: 1rem;
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
  font-weight: 600;
  pointer-events: none;
  box-shadow: 0 4px 12px rgb(0 0 0 / 0.35);
}
.hint {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
