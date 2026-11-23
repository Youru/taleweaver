<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Message from 'primevue/message'
import Select from 'primevue/select'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import { useConfirm } from 'primevue/useconfirm'
import { StartGameDialog, type GameView } from '@/features/games'
import { useBoardPieces } from '@/features/table'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useDraft, useSaveDraft } from '../api/queries'
import type { DraftNode } from '../api/types'
import MapEditor from '../components/MapEditor.vue'
import NodeSheet from '../components/NodeSheet.vue'
import ScenarioGraph from '../components/ScenarioGraph.vue'
import ScenarioSheet from '../components/ScenarioSheet.vue'
import { chapterCaption, chaptersOf } from '../lib/chapters'
import { diagramAsNode, diagramsOf, underOf, updateDiagram, withDiagramBoard } from '../lib/diagrams'
import { useEditor } from '../lib/editor'
import { mapOf, setupOf } from '../lib/map'

/**
 * The workshop's editor: the scenario as a graph in the middle (steps and branches), the sheet of the
 * selected step beside it, the checks as the author types, and "Test" to play it right away.
 */
const props = defineProps<{ name: string }>()

const { t } = useI18n()
const router = useRouter()
const confirm = useConfirm()
const { language } = storeToRefs(usePreferences())
const id = computed(() => `homebrew:scenario/${props.name}`)
const saved = useDraft(id)
const editor = useEditor(saved.data, language)
const {
  draft,
  dirty,
  selected,
  chapter,
  view,
  positions,
  portalSpots,
  report,
  checking,
  issuesByNode,
  canUndo,
  canRedo,
} = editor

// With chapters: the one the graph shows, or the whole scenario
const ALL = '*'
const chapterOptions = computed(() => [
  { id: ALL, label: t('workshop.chapters.all') },
  ...chaptersOf(draft.value ?? { id: '', title: {}, packs: [], start: '', nodes: [] }).map((c) => ({
    id: c.id,
    label: chapterCaption(c, language.value),
  })),
])
const shownChapter = computed({
  get: () => chapter.value ?? ALL,
  set: (value: string) => (chapter.value = value === ALL ? null : value),
})
/** Steps with errors outside the chapter shown: the first one is a click away. */
const elsewhere = computed(() => {
  const shown = new Set(view.value?.scenario.nodes.map((n) => n.id))
  const ids = new Set(draft.value?.nodes.map((n) => n.id))
  return [...issuesByNode.value.keys()].filter((id) => ids.has(id) && !shown.has(id))
})

const node = computed(() => draft.value?.nodes.find((n) => n.id === selected.value))

// The map editor of an encounter takes the whole body while open
const pieces = useBoardPieces()
// …or of one of its diagrams, laid over the encounter's board (shown faded)
const mapping = ref<{ node: string; diagram?: string } | null>(null)
const mappedNode = computed(() => draft.value?.nodes.find((n) => n.id === mapping.value?.node))
const mappedDiagram = computed(() =>
  mappedNode.value && mapping.value?.diagram
    ? diagramsOf(mappedNode.value).find((d) => d.id === mapping.value?.diagram)
    : undefined,
)
const mapped = computed(() =>
  mappedNode.value && mappedDiagram.value ? diagramAsNode(mappedNode.value, mappedDiagram.value) : mappedNode.value,
)
const mappedUnder = computed(() => {
  const owner = mappedNode.value
  const diagram = mappedDiagram.value
  if (!owner || !diagram) return undefined
  return [
    { map: mapOf(owner), setup: setupOf(owner) },
    ...underOf(owner, diagram).map((map) => ({ map, setup: { combatants: [] } })),
  ]
})
function changeMapped(next: DraftNode) {
  const owner = mappedNode.value
  if (!owner) return
  const diagram = mappedDiagram.value
  if (diagram) editor.updateNode(owner.id, (n) => updateDiagram(n, diagram.id, (d) => withDiagramBoard(d, next)))
  else editor.updateNode(owner.id, () => next)
}
const issuesOf = (nodeId: string) => (report.value?.errors ?? []).filter((i) => i.where.split('/')[0] === nodeId)
const warningsOf = (nodeId: string) => (report.value?.warnings ?? []).filter((i) => i.where.split('/')[0] === nodeId)
const warningCount = computed(() => report.value?.warnings.length ?? 0)
const scenarioIssues = computed(() =>
  (report.value?.errors ?? []).filter((i) => !draft.value?.nodes.some((n) => n.id === i.where.split('/')[0])),
)
const errorCount = computed(() => report.value?.errors.length ?? 0)

const labels = computed(() => ({
  scene: t('workshop.newScene'),
  encounter: t('workshop.newEncounter'),
  storyRound: t('workshop.newStoryRound'),
  win: t('workshop.defaultWin'),
  lose: t('workshop.defaultLose'),
}))

const save = useSaveDraft()
const saveError = computed(() => {
  const error = save.error.value
  return error instanceof ApiError ? error.message : error ? String(error) : null
})
function saveNow(then?: () => void) {
  if (!draft.value) return
  save.mutate(draft.value, {
    onSuccess: (result) => {
      editor.markSaved(result)
      then?.()
    },
  })
}

function onKeydown(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey)) return
  const key = event.key.toLowerCase()
  if (key === 's') {
    event.preventDefault()
    saveNow()
    return
  }
  // In a text field, the browser undoes the typing itself
  const target = event.target
  if (target instanceof Element && target.closest('input, textarea, [contenteditable="true"]')) return
  if (key === 'z' && !event.shiftKey) {
    event.preventDefault()
    editor.undo()
  } else if (key === 'y' || (key === 'z' && event.shiftKey)) {
    event.preventDefault()
    editor.redo()
  }
}
onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
onBeforeRouteLeave(
  () =>
    !dirty.value ||
    new Promise<boolean>((resolve) =>
      confirm.require({
        message: t('workshop.leaveUnsaved'),
        acceptProps: { label: t('workshop.leave') },
        accept: () => resolve(true),
        reject: () => resolve(false),
        onHide: () => resolve(false),
      }),
    ),
)

function removeSelected() {
  const target = node.value
  if (!target) return
  confirm.require({
    message: t('workshop.removeConfirm', { name: pick(target.name ?? target.title, language.value) || target.id }),
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', text: true },
    accept: () => editor.remove(target.id),
  })
}

/**
 * The step as it will be printed, beside its sheet (roadmap §2): the print page of the saved copy in a frame,
 * saved first, then again each time the GM asks to refresh.
 */
const preview = ref<{ node: string; version: number } | null>(null)
function showPreview(node: string) {
  const open = () => (preview.value = { node, version: (preview.value?.version ?? 0) + 1 })
  if (dirty.value) saveNow(open)
  else open()
}
const previewUrl = computed(() =>
  preview.value
    ? router.resolve({
        name: 'workshop-print',
        query: { scenario: id.value, node: preview.value.node, embed: '1', v: String(preview.value.version) },
      }).href
    : undefined,
)

/** The scenario on paper (saved first: the print page reads the saved copy). */
function printOut(node?: string) {
  const go = () =>
    void router.push({ name: 'workshop-print', query: node ? { scenario: id.value, node } : { scenario: id.value } })
  if (dirty.value) saveNow(go)
  else go()
}

// "Test": save, then start a game of this scenario
const testing = ref<{ id: string; title: string } | null>(null)
function test() {
  saveNow(() => (testing.value = { id: id.value, title: pick(draft.value?.title, language.value) }))
}
function started(game: GameView) {
  testing.value = null
  void router.push({ name: 'play', params: { id: game.id } })
}
</script>

<template>
  <main class="editor">
    <div v-if="saved.isPending.value" class="center"><ProgressSpinner :aria-label="t('common.loading')" /></div>
    <Message v-else-if="saved.isError.value" severity="error">
      {{ t('common.error', { message: saved.error.value?.message ?? '' }) }}
    </Message>
    <template v-else-if="draft">
      <header class="bar">
        <RouterLink :to="{ name: 'workshop' }" class="back">
          <i class="pi pi-arrow-left" aria-hidden="true" /> {{ t('workshop.title') }}
        </RouterLink>
        <h1>{{ pick(draft.title, language) || draft.id }}</h1>
        <Tag v-if="checking" :value="t('workshop.checking')" severity="secondary" />
        <Tag
          v-else-if="report"
          :value="errorCount ? t('workshop.issues', errorCount) : t('workshop.noIssue')"
          :severity="errorCount ? 'danger' : 'success'"
          :icon="errorCount ? 'pi pi-exclamation-triangle' : 'pi pi-check'"
        />
        <Tag
          v-if="!checking && warningCount"
          :value="t('workshop.warnings', warningCount)"
          severity="warn"
          icon="pi pi-info-circle"
          :title="t('workshop.warningsHint')"
        />
        <Tag
          v-if="!checking && chapter && elsewhere.length"
          :value="`${elsewhere.length} ${t('workshop.chapters.issuesElsewhere', elsewhere.length)}`"
          severity="danger"
          icon="pi pi-directions"
          class="elsewhere"
          role="button"
          tabindex="0"
          @click="editor.reveal(elsewhere[0]!)"
          @keydown.enter="editor.reveal(elsewhere[0]!)"
        />
        <span class="spacer" />
        <Select
          v-if="chapterOptions.length > 1"
          v-model="shownChapter"
          :options="chapterOptions"
          option-label="label"
          option-value="id"
          size="small"
          :aria-label="t('workshop.chapters.show')"
          class="chapter-select"
        />
        <Button
          icon="pi pi-undo"
          severity="secondary"
          text
          :disabled="!canUndo"
          :aria-label="t('workshop.undo')"
          :title="t('workshop.undo')"
          @click="editor.undo()"
        />
        <Button
          icon="pi pi-refresh"
          severity="secondary"
          text
          :disabled="!canRedo"
          :aria-label="t('workshop.redo')"
          :title="t('workshop.redo')"
          @click="editor.redo()"
        />
        <Button
          :label="t('workshop.addScene')"
          icon="pi pi-book"
          severity="secondary"
          outlined
          @click="editor.add('scene', labels)"
        />
        <Button
          :label="t('workshop.addEncounter')"
          icon="pi pi-bolt"
          severity="secondary"
          outlined
          @click="editor.add('encounter', labels)"
        />
        <Button
          :label="t('workshop.addStoryRound')"
          icon="pi pi-shop"
          severity="secondary"
          outlined
          @click="editor.add('storyRound', labels)"
        />
        <Button
          :label="dirty ? t('workshop.save') : t('workshop.saved')"
          icon="pi pi-save"
          :disabled="!dirty"
          :loading="save.isPending.value"
          @click="saveNow()"
        />
        <Button
          :label="t('workshop.print.print')"
          icon="pi pi-print"
          severity="secondary"
          outlined
          @click="printOut()"
        />
        <Button
          :label="t('workshop.test')"
          icon="pi pi-play"
          severity="contrast"
          :disabled="errorCount > 0 || save.isPending.value"
          :title="errorCount ? t('workshop.fixFirst') : undefined"
          @click="test"
        />
      </header>
      <Message v-if="saveError" severity="error" closable @close="save.reset()">{{ saveError }}</Message>

      <MapEditor
        v-if="mapped"
        :key="mapped.id"
        class="mapping"
        :node="mapped"
        :pieces="pieces.data.value"
        :under="mappedUnder"
        @change="changeMapped"
        @close="mapping = null"
      />
      <div v-else class="body">
        <ScenarioGraph
          class="graph"
          :chapter="chapter"
          :scenario="view?.scenario ?? draft"
          :whole="draft"
          :portals="view?.portals"
          :portal-positions="portalSpots"
          :positions="positions"
          :selected="selected"
          :issues="issuesByNode"
          @select="(nodeId) => (selected = nodeId)"
          @open="editor.reveal"
          @move="editor.move"
          @connect="editor.connect"
        />
        <aside class="side">
          <NodeSheet
            v-if="node"
            :key="node.id"
            :scenario="draft"
            :node="node"
            :issues="issuesOf(node.id)"
            :warnings="warningsOf(node.id)"
            :pieces="pieces.data.value"
            @change="(next) => editor.updateNode(node!.id, () => next)"
            @remove="removeSelected"
            @start="editor.setStart(node!.id)"
            @map="(diagram) => (mapping = { node: node!.id, diagram })"
            @print="printOut(node!.id)"
            @preview="showPreview(node!.id)"
          />
          <ScenarioSheet
            v-else
            :scenario="draft"
            :issues="scenarioIssues"
            :chapter="chapter"
            @change="editor.change"
            @select="editor.reveal"
            @show="(id) => (chapter = id)"
          />
        </aside>
      </div>
      <StartGameDialog :scenario="testing" @close="testing = null" @started="started" />
    </template>
    <Drawer
      :visible="!!preview"
      position="right"
      :modal="false"
      :header="t('workshop.preview.title')"
      class="print-preview"
      :style="{ width: 'min(52rem, 55vw)' }"
      @update:visible="(v) => !v && (preview = null)"
    >
      <Button
        :label="t('workshop.preview.refresh')"
        icon="pi pi-refresh"
        size="small"
        severity="secondary"
        :loading="save.isPending.value"
        @click="preview && showPreview(preview.node)"
      />
      <p class="hint">{{ t('workshop.preview.hint') }}</p>
      <iframe v-if="previewUrl" :key="previewUrl" :src="previewUrl" :title="t('workshop.preview.title')" />
    </Drawer>
  </main>
</template>

<style scoped>
.editor {
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
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
}
.back {
  color: var(--p-text-muted-color);
  text-decoration: none;
  font-size: 0.9rem;
}
h1 {
  margin: 0;
  font-size: 1.3rem;
}
.spacer {
  flex: 1;
}
.chapter-select {
  min-width: 12rem;
}
.elsewhere {
  cursor: pointer;
}
.body {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(22rem, 30rem);
  gap: 1rem;
  flex: 1;
  min-height: 0;
}
.graph {
  min-height: 0;
}
.mapping {
  flex: 1;
  min-height: 0;
}
.side {
  overflow-y: auto;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
@media (max-width: 1000px) {
  .editor {
    height: auto;
  }
  .body {
    grid-template-columns: 1fr;
  }
  .graph {
    height: 60vh;
  }
}
.print-preview iframe {
  width: 100%;
  height: calc(100% - 4rem);
  border: 1px solid var(--p-content-border-color);
  background: #fff;
}
.print-preview .hint {
  margin: 0.4rem 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
