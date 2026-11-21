import type { Language, LocalizedText } from '@/shared/lib/localized'
import { pick } from '@/shared/lib/localized'
import type { DraftAction, DraftEnd, DraftNode, DraftScenario, Position } from '../api/types'

/**
 * The scenario as a graph: each node has exits (its "next", the options of a choice, its end
 * conditions), and each exit leads to a node or to an ending ("@victory", "@defeat"…).
 */
export type ExitKind = 'next' | 'option' | 'win' | 'lose' | 'sideQuest' | 'ventureForth'

export interface Exit {
  /** Stable within the node: "next", "option:0", "end:<id>". */
  key: string
  kind: ExitKind
  label?: LocalizedText
  target?: string
}

/** Endings offered on every graph; any other "@…" target met in the scenario is shown too. */
export const ENDINGS = ['@victory', '@defeat'] as const
export const isEnding = (target: string | undefined) => !!target?.startsWith('@')

const goTo = (node: string | undefined): DraftAction[] => (node ? [{ do: 'goToNode', node }] : [])
const targetOf = (actions: DraftAction[] | undefined) => actions?.find((a) => a.do === 'goToNode')?.node

/** The players' choice of a scene, when it has one. */
export function choiceOf(node: DraftNode): DraftAction | undefined {
  return node.mechanics?.find((a) => a.do === 'askChoice')
}

export function exitsOf(node: DraftNode): Exit[] {
  if (node.type === 'encounter')
    return (node.endConditions ?? []).map((end) => ({
      key: `end:${end.id}`,
      kind: end.kind === 'lose' ? 'lose' : 'win',
      label: end.text,
      target: end.next,
    }))
  if (node.type === 'storyRound')
    return [
      ...(node.sideQuests ?? []).map((q, i) => ({
        key: `sideQuest:${i}`,
        kind: 'sideQuest' as const,
        label: q.label,
        target: q.node || undefined,
      })),
      ...(node.ventureForth ?? []).map((v, i) => ({
        key: `ventureForth:${i}`,
        kind: 'ventureForth' as const,
        label: v.label,
        target: v.node || undefined,
      })),
    ]
  const choice = choiceOf(node)
  if (choice)
    return (choice.options ?? []).map((option, i) => ({
      key: `option:${i}`,
      kind: 'option',
      label: option.label,
      target: targetOf(option.actions),
    }))
  return [{ key: 'next', kind: 'next', target: node.next }]
}

/** The node with one of its exits leading to `target` (undefined: the exit leads nowhere yet). */
export function setExit(node: DraftNode, key: string, target: string | undefined): DraftNode {
  if (key === 'next') return { ...node, next: target }
  for (const list of ['sideQuests', 'ventureForth'] as const) {
    const prefix = list === 'sideQuests' ? 'sideQuest:' : 'ventureForth:'
    if (key.startsWith(prefix)) {
      const index = Number(key.slice(prefix.length))
      return { ...node, [list]: (node[list] ?? []).map((x, i) => (i === index ? { ...x, node: target ?? '' } : x)) }
    }
  }
  if (key.startsWith('end:')) {
    const id = key.slice(4)
    return {
      ...node,
      endConditions: (node.endConditions ?? []).map((e) => (e.id === id ? { ...e, next: target } : e)),
    }
  }
  const index = Number(key.slice(7))
  return {
    ...node,
    mechanics: (node.mechanics ?? []).map((a) =>
      a.do === 'askChoice'
        ? {
            ...a,
            options: (a.options ?? []).map((o, i) =>
              i === index
                ? { ...o, actions: [...(o.actions ?? []).filter((x) => x.do !== 'goToNode'), ...goTo(target)] }
                : o,
            ),
          }
        : a,
    ),
  }
}

/**
 * Turns a scene's single "next" into a choice of the players (its first option keeps the next), or
 * back (the next becomes the first option's).
 */
export function setChoice(
  node: DraftNode,
  on: boolean,
  labels: { question: LocalizedText; option: (n: number) => LocalizedText },
): DraftNode {
  const choice = choiceOf(node)
  if (on && !choice) {
    const { next, ...rest } = node
    return {
      ...rest,
      mechanics: [
        ...(node.mechanics ?? []),
        {
          do: 'askChoice',
          question: labels.question,
          options: [
            { label: labels.option(1), actions: goTo(next) },
            { label: labels.option(2), actions: [] },
          ],
        },
      ],
    }
  }
  if (!on && choice) {
    const first = targetOf(choice.options?.[0]?.actions)
    return { ...node, mechanics: (node.mechanics ?? []).filter((a) => a !== choice), next: first }
  }
  return node
}

/** Replaces the choice of a scene (question, options). */
export function withChoice(node: DraftNode, change: (choice: DraftAction) => DraftAction): DraftNode {
  return { ...node, mechanics: (node.mechanics ?? []).map((a) => (a.do === 'askChoice' ? change(a) : a)) }
}

/** Every target any exit points at, in the scenario. */
export function targets(scenario: DraftScenario): string[] {
  return scenario.nodes.flatMap((n) => exitsOf(n).map((e) => e.target)).filter((t): t is string => !!t)
}

/** The endings the graph shows: the usual two, plus any other used. */
export function endingsOf(scenario: DraftScenario): string[] {
  return [...new Set([...ENDINGS, ...targets(scenario).filter(isEnding)])]
}

/** A node's caption on the graph and in the lists. */
export function captionOf(node: DraftNode, language: Language): string {
  return pick(node.type === 'encounter' ? node.name : node.title, language) || node.id
}

export const COLUMN = 300
export const ROW = 190

/**
 * Where each node sits: the saved position, else a layered layout (one column per step from the
 * start, the endings in the last column).
 */
export function layout(scenario: DraftScenario): Record<string, Position> {
  const saved = scenario.editor?.positions ?? {}
  const depth = new Map<string, number>()
  const queue = [scenario.start]
  depth.set(scenario.start, 0)
  while (queue.length) {
    const id = queue.shift()!
    const node = scenario.nodes.find((n) => n.id === id)
    for (const exit of node ? exitsOf(node) : []) {
      if (exit.target && !isEnding(exit.target) && !depth.has(exit.target)) {
        depth.set(exit.target, depth.get(id)! + 1)
        queue.push(exit.target)
      }
    }
  }
  // Nodes nobody reaches yet go after the others.
  let last = Math.max(0, ...depth.values())
  for (const node of scenario.nodes) if (!depth.has(node.id)) depth.set(node.id, ++last)

  const rows = new Map<number, number>()
  const place = (id: string, column: number): Position => {
    const row = rows.get(column) ?? 0
    rows.set(column, row + 1)
    return { x: column * COLUMN, y: row * ROW }
  }
  const positions: Record<string, Position> = {}
  for (const node of scenario.nodes) positions[node.id] = saved[node.id] ?? place(node.id, depth.get(node.id)!)
  const endColumn = Math.max(0, ...depth.values()) + 1
  for (const ending of endingsOf(scenario)) positions[ending] = saved[ending] ?? place(ending, endColumn)
  return positions
}

/** A node id not used yet: "scene-1", "scene-2"… */
export function freshId(scenario: DraftScenario, prefix: string): string {
  const used = new Set(scenario.nodes.map((n) => n.id))
  let n = 1
  while (used.has(`${prefix}-${n}`)) n++
  return `${prefix}-${n}`
}

/** A file name from a title: "La Prime du Marais" → "la-prime-du-marais". */
export function slug(title: string): string {
  return (
    title
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '') || 'scenario'
  )
}

export const text = (language: Language, value: string): LocalizedText => ({ [language]: value })

/** Writes `value` in `language`, keeping the other languages. */
export function setText(current: LocalizedText | undefined, language: Language, value: string): LocalizedText {
  return { ...current, [language]: value }
}

export function newScene(id: string, language: Language, title: string): DraftNode {
  return { id, type: 'scene', title: text(language, title), narrative: text(language, ''), next: undefined }
}

export function newEncounter(
  id: string,
  language: Language,
  labels: { name: string; win: string; lose: string },
): DraftNode {
  const ends: DraftEnd[] = [
    { id: 'win', kind: 'win', text: text(language, labels.win), next: '@victory' },
    { id: 'lose', kind: 'lose', text: text(language, labels.lose), next: '@defeat' },
  ]
  return {
    id,
    type: 'encounter',
    name: text(language, labels.name),
    intro: text(language, ''),
    lootLevel: 'common',
    endConditions: ends,
  }
}

/** Removes a node, and every exit that led to it. */
export function removeNode(scenario: DraftScenario, id: string): DraftScenario {
  const nodes = scenario.nodes
    .filter((n) => n.id !== id)
    .map((n) => exitsOf(n).reduce((node, exit) => (exit.target === id ? setExit(node, exit.key, undefined) : node), n))
  const positions = { ...scenario.editor?.positions }
  delete positions[id]
  return {
    ...scenario,
    nodes,
    start: scenario.start === id ? (nodes[0]?.id ?? '') : scenario.start,
    editor: { ...scenario.editor, positions },
  }
}
