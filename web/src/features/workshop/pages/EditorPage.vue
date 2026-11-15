<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import { useConfirm } from 'primevue/useconfirm'
import { StartGameDialog, type GameView } from '@/features/games'
import { useBoardPieces } from '@/features/table'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useDraft, useSaveDraft } from '../api/queries'
import MapEditor from '../components/MapEditor.vue'
import NodeSheet from '../components/NodeSheet.vue'
import ScenarioGraph from '../components/ScenarioGraph.vue'
import ScenarioSheet from '../components/ScenarioSheet.vue'
import { useEditor } from '../lib/editor'

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
const { draft, dirty, selected, positions, report, checking, issuesByNode } = editor

const node = computed(() => draft.value?.nodes.find((n) => n.id === selected.value))

// The map editor of an encounter takes the whole body while open
const pieces = useBoardPieces()
const mapping = ref<string | null>(null)
const mapped = computed(() => draft.value?.nodes.find((n) => n.id === mapping.value))
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
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
    event.preventDefault()
    saveNow()
  }
}
onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
onBeforeRouteLeave(() => !dirty.value || window.confirm(t('workshop.leaveUnsaved')))

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
        <span class="spacer" />
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
        class="mapping"
        :node="mapped"
        :pieces="pieces.data.value"
        @change="(next) => editor.updateNode(mapped!.id, () => next)"
        @close="mapping = null"
      />
      <div v-else class="body">
        <ScenarioGraph
          class="graph"
          :scenario="draft"
          :positions="positions"
          :selected="selected"
          :issues="issuesByNode"
          @select="(nodeId) => (selected = nodeId)"
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
            @map="mapping = node!.id"
            @print="printOut(node!.id)"
          />
          <ScenarioSheet
            v-else
            :scenario="draft"
            :issues="scenarioIssues"
            @change="editor.change"
            @select="(nodeId) => (selected = nodeId)"
          />
        </aside>
      </div>
      <StartGameDialog :scenario="testing" @close="testing = null" @started="started" />
    </template>
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
</style>
