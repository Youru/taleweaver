import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import type { Language } from '@/shared/lib/localized'
import type { DraftNode, DraftScenario, Position, ScenarioReport } from '../api/types'
import { workshopApi } from '../api/workshopApi'
import { freshId, layout, newEncounter, newScene, removeNode, setExit } from './graph'

/** Labels the new nodes get, in the author's language. */
export interface NewNodeLabels {
  scene: string
  encounter: string
  win: string
  lose: string
}

/**
 * The scenario being edited: a copy of the saved one, changed by the sheets and the graph, checked
 * by the API as the author types (debounced), and saved on demand.
 */
export function useEditor(saved: Ref<DraftScenario | undefined>, language: Ref<Language>) {
  const draft = ref<DraftScenario>()
  const dirty = ref(false)
  const selected = ref<string | null>(null)

  watch(
    saved,
    (value) => {
      // A plain deep copy (structuredClone refuses the reactive proxies of the query cache).
      if (value && !dirty.value) draft.value = JSON.parse(JSON.stringify(value)) as DraftScenario
    },
    { immediate: true },
  )

  function change(next: DraftScenario) {
    draft.value = next
    dirty.value = true
  }

  function updateNode(id: string, update: (node: DraftNode) => DraftNode) {
    const d = draft.value
    if (d) change({ ...d, nodes: d.nodes.map((n) => (n.id === id ? update(n) : n)) })
  }

  /** Every node keeps a position once the author touched the graph: the layout no longer moves them. */
  const positions = computed(() => (draft.value ? layout(draft.value) : {}))

  function move(id: string, position: Position) {
    const d = draft.value
    if (!d) return
    change({ ...d, editor: { ...d.editor, positions: { ...positions.value, [id]: position } } })
  }

  function add(type: 'scene' | 'encounter', labels: NewNodeLabels, at?: Position) {
    const d = draft.value
    if (!d) return
    const id = freshId(d, type)
    const node =
      type === 'scene'
        ? newScene(id, language.value, labels.scene)
        : newEncounter(id, language.value, { name: labels.encounter, win: labels.win, lose: labels.lose })
    const spot = at ?? { x: Math.max(0, ...Object.values(positions.value).map((p) => p.x)) + 300, y: 0 }
    change({
      ...d,
      nodes: [...d.nodes, node],
      start: d.start || id,
      editor: { ...d.editor, positions: { ...positions.value, [id]: spot } },
    })
    selected.value = id
  }

  function connect(source: string, exit: string, target: string | undefined) {
    updateNode(source, (node) => setExit(node, exit, target))
  }

  function remove(id: string) {
    const d = draft.value
    if (!d) return
    change(removeNode(d, id))
    if (selected.value === id) selected.value = null
  }

  function setStart(id: string) {
    const d = draft.value
    if (d) change({ ...d, start: id })
  }

  // The checks, as the author types
  const report = ref<ScenarioReport>()
  const checking = ref(false)
  let timer: ReturnType<typeof setTimeout> | undefined
  let pending: AbortController | undefined
  watch(
    draft,
    (value) => {
      clearTimeout(timer)
      if (!value) return
      timer = setTimeout(async () => {
        pending?.abort()
        pending = new AbortController()
        checking.value = true
        try {
          report.value = await workshopApi.validate(value, pending.signal)
        } catch (error) {
          if ((error as Error).name !== 'AbortError')
            report.value = {
              errors: [{ code: 'Scenarios.Invalid', where: 'nodes', message: String(error) }],
              warnings: [],
            }
        } finally {
          checking.value = false
        }
      }, 400)
    },
    { immediate: true },
  )
  onBeforeUnmount(() => {
    clearTimeout(timer)
    pending?.abort()
  })

  /** Problems by node id ("node/detail" counts for the node). */
  const issuesByNode = computed(() => {
    const map = new Map<string, number>()
    for (const issue of report.value?.errors ?? []) {
      const id = issue.where.split('/')[0] ?? ''
      map.set(id, (map.get(id) ?? 0) + 1)
    }
    return map
  })

  /** After a save: the saved copy is the reference again. */
  function markSaved(next: ScenarioReport) {
    dirty.value = false
    report.value = next
  }

  return {
    draft,
    dirty,
    selected,
    positions,
    report,
    checking,
    issuesByNode,
    change,
    updateNode,
    move,
    add,
    connect,
    remove,
    setStart,
    markSaved,
  }
}

export type Editor = ReturnType<typeof useEditor>
