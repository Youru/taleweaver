import type { BoardPieces } from '@/features/table'
import { pick, type Language, type LocalizedText } from '@/shared/lib/localized'
import type { DraftNode, DraftScenario } from '../api/types'
import { eventsOfToken, tokenRulesOf } from './events'
import { captionOf } from './graph'
import { mapOf, setupOf, tokenKey } from './map'

/**
 * What the printed page of an encounter shows, laid out as the official Encounter template: the
 * name and introduction, the map, the setup list, the end conditions with their rewards, the special
 * rules, and the hidden text of each token (read through the red filter).
 */
export interface EncounterSheet {
  id: string
  name: string
  intro: string
  location?: string
  northArrow: number
  setup: SetupLine[]
  ends: PrintedEnd[]
  rules: { name: string; text: string }[]
  /** Curiously Hidden Rules: read only when an event says so (printed under the red filter). */
  hiddenRules: { name: string; text: string }[]
  hidden: HiddenSection[]
}

export interface SetupLine {
  /** tiles, terrain, totem, objective, loot, exit, encounter, combatant, figures */
  group: string
  items: string[]
}

export interface PrintedEnd {
  kind: 'win' | 'lose'
  text: string
  reward: string
  /** Where it leads: a step's caption, or an ending ("@victory"…). */
  next?: string
  /** Only in play once an event adds it: printed under the red filter. */
  later?: boolean
}

export interface HiddenSection {
  /** The token category: loot, totem, objective, exit… */
  group: string
  entries: HiddenEntry[]
}

export interface HiddenEntry {
  key: string
  /** The token's colour, or its name when it has none. */
  label: string
  /** How it is activated: printed in the clear. */
  activation: string
  /** What it reveals: printed under the red filter. */
  text: string[]
}

/** The order of the setup list and of the hidden sections, as in the books. */
const GROUPS = ['terrain', 'totem', 'objective', 'loot', 'exit', 'encounter', 'combatant', 'direction']

const counted = (labels: string[]) => {
  const counts = new Map<string, number>()
  for (const label of labels) counts.set(label, (counts.get(label) ?? 0) + 1)
  return [...counts].map(([label, n]) => `${n} × ${label}`)
}

export function encounterSheet(
  scenario: DraftScenario,
  node: DraftNode,
  pieces: BoardPieces | undefined,
  language: Language,
  colorName: (color: string) => string,
  /** "Blue Loot" or "Loot bleu": the order of the language. */
  tokenLabel: (name: string, color: string) => string = (name, color) => `${color} ${name}`,
): EncounterSheet {
  const text = (value: LocalizedText | undefined) => pick(value, language)
  const map = mapOf(node)
  const setup = setupOf(node)
  const categoryOf = (type: string) => pieces?.tokens[type]?.category ?? 'encounter'
  const tokenName = (type: string) => text(pieces?.tokens[type]?.name) || type.split('/').pop() || type

  const lines: SetupLine[] = []
  if (map.tiles.length)
    lines.push({ group: 'tiles', items: [map.tiles.map((t) => pieces?.tiles[t.ref]?.name ?? t.ref).join(', ')] })
  for (const group of GROUPS) {
    const tokens = map.tokens.filter((t) => categoryOf(t.type) === group)
    if (tokens.length)
      lines.push({
        group,
        items: counted(tokens.map((t) => (t.color ? capitalize(colorName(t.color)) : tokenName(t.type)))),
      })
  }
  const others = map.tokens.filter((t) => !GROUPS.includes(categoryOf(t.type)))
  if (others.length) lines.push({ group: 'other', items: counted(others.map((t) => tokenName(t.type))) })
  /** A card named by its ref ("um1:combatant/blighted-terror-common") belongs to the longest family it starts with. */
  const familyOf = (ref: string | undefined) => {
    const card = ref?.split('/').pop() ?? ''
    return Object.keys(pieces?.families ?? {})
      .filter((f) => card.startsWith(f))
      .sort((a, b) => b.length - a.length)[0]
  }
  const families = setup.combatants.map((c) => {
    const family = c.family ?? familyOf(c.ref)
    return text(family ? pieces?.families[family]?.name : undefined) || family || c.ref || '?'
  })
  if (families.length) lines.push({ group: 'figures', items: counted(families) })

  const caption = (target: string | undefined) => {
    if (!target) return undefined
    const step = scenario.nodes.find((n) => n.id === target)
    return step ? captionOf(step, language) : target
  }
  const ends = (node.endConditions ?? []).map((e) => ({
    kind: e.kind === 'lose' ? ('lose' as const) : ('win' as const),
    text: text(e.text),
    reward: text(e.reward),
    next: caption(e.next),
    ...(e.active === false ? { later: true } : {}),
  }))

  const rules = (node.specialRules ?? []).map((r) => ({ name: text(r.name), text: text(r.text) }))

  // Every token with something to read: those of the map, and those described without being on it
  const keys = [...new Set([...map.tokens.map((t) => tokenKey(t.type, t.color)), ...Object.keys(tokenRulesOf(node))])]
  const typeOfKey = (key: string) => map.tokens.find((t) => tokenKey(t.type, t.color) === key)?.type
  const sections = new Map<string, HiddenEntry[]>()
  for (const key of keys) {
    const rule = tokenRulesOf(node)[key]
    const hiddenText = eventsOfToken(node, key)
      .map((e) => text(e.hiddenText))
      .filter((t) => !!t)
    const activation = text(rule?.activation)
    if (!hiddenText.length && !activation) continue
    const type = typeOfKey(key)
    const [short, color] = key.split(':')
    const group = type ? categoryOf(type) : (short ?? 'encounter')
    const label = color
      ? tokenLabel(type ? tokenName(type) : (short ?? key), colorName(color))
      : type
        ? tokenName(type)
        : key
    sections.set(group, [
      ...(sections.get(group) ?? []),
      { key, label: capitalize(label), activation, text: hiddenText },
    ])
  }
  const hidden = [...sections].sort(([a], [b]) => rank(a) - rank(b)).map(([group, entries]) => ({ group, entries }))

  return {
    id: node.id,
    name: text(node.name) || node.id,
    intro: text(node.intro),
    location: node.locationType,
    northArrow: Number((node.map as { northArrow?: number } | undefined)?.northArrow ?? 0),
    setup: lines,
    ends,
    rules,
    hiddenRules: ((node.hiddenRules as { name?: LocalizedText; text?: LocalizedText }[] | undefined) ?? []).map(
      (r) => ({ name: text(r.name), text: text(r.text) }),
    ),
    hidden,
  }
}

const rank = (group: string) => {
  const i = ['loot', 'totem', 'objective', 'exit', 'direction', 'encounter', 'combatant'].indexOf(group)
  return i < 0 ? 99 : i
}

const capitalize = (value: string) => value.charAt(0).toUpperCase() + value.slice(1)

/** The steps of the scenario in reading order: from the start, following each exit. */
export function readingOrder(scenario: DraftScenario): DraftNode[] {
  const byId = new Map(scenario.nodes.map((n) => [n.id, n]))
  const seen = new Set<string>()
  const order: DraftNode[] = []
  const visit = (id: string | undefined) => {
    const node = id ? byId.get(id) : undefined
    if (!node || seen.has(node.id)) return
    seen.add(node.id)
    order.push(node)
    const next: (string | undefined)[] = [
      node.next as string | undefined,
      ...(node.endConditions ?? []).map((e) => e.next),
      ...(node.mechanics ?? []).flatMap((a) =>
        (a.options ?? []).flatMap((o) => (o.actions ?? []).filter((x) => x.do === 'goToNode').map((x) => x.node)),
      ),
    ]
    next.forEach(visit)
  }
  visit(scenario.start)
  scenario.nodes.forEach((n) => visit(n.id))
  return order
}
