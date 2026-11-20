<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { VueFlow, useVueFlow, type Connection, type Edge, type EdgeChange, type Node } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import AutoComplete, { type AutoCompleteCompleteEvent, type AutoCompleteOptionSelectEvent } from 'primevue/autocomplete'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftScenario, Position } from '../api/types'
import { captionOf, endingsOf, exitsOf, isEnding, type Exit } from '../lib/graph'
import { searchNodes, type SearchHit } from '../lib/search'
import EndingNode from './EndingNode.vue'
import StepNode from './StepNode.vue'

/**
 * The scenario as a graph: drag a node to place it, pull a link from an exit (right) to a node
 * (left) to branch, drag a link's end elsewhere to change it, or select it and press Delete.
 */
const props = defineProps<{
  scenario: DraftScenario
  positions: Record<string, Position>
  selected: string | null
  issues: Map<string, number>
}>()
const emit = defineEmits<{
  select: [id: string | null]
  move: [id: string, position: Position]
  connect: [source: string, exit: string, target: string | undefined]
}>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const { getSelectedEdges, fitView } = useVueFlow('workshop-graph')

// Find a step by its title, id or texts, then go to it
const query = ref('')
const hits = ref<SearchHit[]>([])
function search(event: AutoCompleteCompleteEvent) {
  hits.value = searchNodes(props.scenario, event.query, language.value)
}
function goTo(event: AutoCompleteOptionSelectEvent) {
  const hit = event.value as SearchHit
  emit('select', hit.id)
  void fitView({ nodes: [hit.id], duration: 400, maxZoom: 1 })
  query.value = ''
}

interface EndingLabel {
  caption: string
  tone: 'victory' | 'defeat' | 'other'
}
function ending(id: string): EndingLabel {
  if (id === '@victory') return { caption: t('workshop.endVictory'), tone: 'victory' }
  if (id === '@defeat') return { caption: t('workshop.endDefeat'), tone: 'defeat' }
  if (id === '@currentStoryRound') return { caption: t('workshop.endStoryRound'), tone: 'other' }
  return { caption: id, tone: 'other' }
}

function exitText(exit: Exit, index: number): string {
  const label = pick(exit.label, language.value)
  if (exit.kind === 'next') return t('workshop.exitNext')
  if (exit.kind === 'option') return label || t('workshop.optionN', { n: index + 1 })
  return `${exit.kind === 'win' ? t('workshop.win') : t('workshop.lose')}${label ? ` · ${label}` : ''}`
}

const nodes = computed<Node[]>(() => [
  ...props.scenario.nodes.map((node) => ({
    id: node.id,
    type: 'step',
    position: props.positions[node.id] ?? { x: 0, y: 0 },
    selected: node.id === props.selected,
    data: {
      type: node.type,
      caption: captionOf(node, language.value),
      start: node.id === props.scenario.start,
      issues: props.issues.get(node.id) ?? 0,
      exits: exitsOf(node).map((exit, i) => ({ ...exit, text: exitText(exit, i) })),
    },
  })),
  ...endingsOf(props.scenario).map((id) => ({
    id,
    type: 'ending',
    position: props.positions[id] ?? { x: 0, y: 0 },
    data: ending(id),
  })),
])

const edges = computed<Edge[]>(() =>
  props.scenario.nodes.flatMap((node) =>
    exitsOf(node)
      .filter((exit) => !!exit.target)
      .map((exit) => ({
        id: `${node.id}|${exit.key}`,
        source: node.id,
        sourceHandle: exit.key,
        target: exit.target!,
        updatable: true,
        class: `exit-${exit.kind}${isEnding(exit.target) ? ' to-ending' : ''}`,
      })),
  ),
)

function onConnect(connection: Connection) {
  if (connection.sourceHandle) emit('connect', connection.source, connection.sourceHandle, connection.target)
}

/** A link's end dragged onto another node. */
function onEdgeUpdate({ edge, connection }: { edge: Edge; connection: Connection }) {
  if (edge.sourceHandle) emit('connect', edge.source, edge.sourceHandle, connection.target)
}

function onEdgesChange(changes: EdgeChange[]) {
  for (const change of changes)
    if (change.type === 'remove' && change.sourceHandle) emit('connect', change.source, change.sourceHandle, undefined)
}

/** Delete removes the selected links (never a node: that is done from its sheet). */
function onKeydown(event: KeyboardEvent) {
  if (event.key !== 'Delete' && event.key !== 'Backspace') return
  if ((event.target as HTMLElement).closest('input, textarea')) return
  for (const edge of getSelectedEdges.value)
    if (edge.sourceHandle) emit('connect', edge.source, edge.sourceHandle, undefined)
}
</script>

<template>
  <div class="graph" role="application" :aria-label="t('workshop.graph')" tabindex="-1" @keydown="onKeydown">
    <div class="search">
      <AutoComplete
        v-model="query"
        :suggestions="hits"
        option-label="caption"
        :placeholder="t('workshop.search.placeholder')"
        :aria-label="t('workshop.search.label')"
        :empty-search-message="t('workshop.search.none')"
        :delay="150"
        size="small"
        @complete="search"
        @option-select="goTo"
      >
        <template #option="{ option }">
          <div class="hit">
            <span>
              <i :class="option.type === 'encounter' ? 'pi pi-bolt' : 'pi pi-book'" aria-hidden="true" />
              {{ option.caption }} <code>{{ option.id }}</code>
            </span>
            <small v-if="option.excerpt">{{ option.excerpt }}</small>
          </div>
        </template>
      </AutoComplete>
    </div>
    <VueFlow
      id="workshop-graph"
      :nodes="nodes"
      :edges="edges"
      :delete-key-code="null"
      :connection-radius="70"
      :min-zoom="0.2"
      :max-zoom="1.6"
      :default-edge-options="{ type: 'smoothstep', markerEnd: 'arrowclosed' }"
      fit-view-on-init
      @connect="onConnect"
      @edge-update="onEdgeUpdate"
      @edges-change="onEdgesChange"
      @node-drag-stop="
        ({ node }) => emit('move', node.id, { x: Math.round(node.position.x), y: Math.round(node.position.y) })
      "
      @node-click="({ node }) => emit('select', node.type === 'step' ? node.id : null)"
      @pane-click="emit('select', null)"
    >
      <template #node-step="stepProps">
        <StepNode :data="stepProps.data" :selected="stepProps.id === selected" />
      </template>
      <template #node-ending="endingProps">
        <EndingNode :data="endingProps.data" />
      </template>
      <Background :gap="24" />
      <Controls :show-interactive="false" />
    </VueFlow>
  </div>
</template>

<style scoped>
.graph {
  position: relative;
  height: 100%;
  min-height: 20rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
  overflow: hidden;
}
.search {
  position: absolute;
  top: 0.5rem;
  left: 0.5rem;
  z-index: 5;
  width: min(20rem, calc(100% - 1rem));
}
.search :deep(.p-autocomplete),
.search :deep(input) {
  width: 100%;
}
.hit {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  max-width: 24rem;
}
.hit code {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
}
.hit small {
  color: var(--p-text-muted-color);
  white-space: normal;
}
.graph :deep(.vue-flow__edge-path) {
  stroke-width: 2.5;
}
.graph :deep(.exit-win .vue-flow__edge-path) {
  stroke: #16a34a;
}
.graph :deep(.exit-lose .vue-flow__edge-path) {
  stroke: #dc2626;
}
.graph :deep(.exit-option .vue-flow__edge-path) {
  stroke: #6d28d9;
}
.graph :deep(.vue-flow__edge.selected .vue-flow__edge-path) {
  stroke-width: 5;
}
.graph :deep(.vue-flow__node) {
  border: none;
  padding: 0;
  background: none;
  width: auto;
}
</style>
