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
  /** The encounter's loot level ("mundane"…), printed under the north arrow and beside the Loot title. */
  lootLevel?: string
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
  /** A win condition after the first one: "Alternate win condition". */
  alternate?: boolean
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
  /** The token's picture (asset path), printed before the label as in the books. */
  image?: string
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
  if (map.tiles.length) {
    // In number order, as the books list them: UM31, UM33, UM35…
    const names = [...new Set(map.tiles.map((t) => pieces?.tiles[t.ref]?.name ?? t.ref))]
    lines.push({ group: 'tiles', items: [names.sort(byNumber).join(', ')] })
  }
  for (const group of GROUPS) {
    const tokens = map.tokens.filter((t) => categoryOf(t.type) === group)
    if (!tokens.length) continue
    if (group === 'terrain') {
      lines.push({ group, items: terrainItems(tokens.map((t) => t.type)) })
      continue
    }
    lines.push({
      group,
      items: counted(
        [...tokens]
          .sort((a, b) => colorRank(a.color) - colorRank(b.color))
          .map((t) => (t.color ? capitalize(colorName(t.color)) : tokenName(t.type))),
      ),
    })
  }
  /**
   * Terrain as the books write it: "7 x Obstructing (2x1)", the size from the catalogue rather than
   * from the token's name; each kind together, the largest first, kinds in the order of the map.
   */
  function terrainItems(types: string[]): string[] {
    const kinds: string[] = []
    const entries = new Map<string, { kind: string; size: string; area: number; n: number }>()
    for (const type of types) {
      const kind = terrainName(tokenName(type))
      const size = pieces?.tokens[type]?.size ?? '1x1'
      const [w = 1, h = 1] = size.split('x').map(Number)
      if (!kinds.includes(kind)) kinds.push(kind)
      const key = `${kind} (${size})`
      const entry = entries.get(key) ?? { kind, size, area: w * h, n: 0 }
      entry.n++
      entries.set(key, entry)
    }
    return [...entries.values()]
      .sort((a, b) => kinds.indexOf(a.kind) - kinds.indexOf(b.kind) || b.area - a.area)
      .map((e) => `${e.n} × ${e.kind} (${e.size})`)
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
  let wins = 0
  const ends = (node.endConditions ?? []).map((e) => ({
    kind: e.kind === 'lose' ? ('lose' as const) : ('win' as const),
    ...(e.kind !== 'lose' && wins++ > 0 ? { alternate: true } : {}),
    text: text(e.text),
    reward: text(e.reward),
    next: caption(e.next),
    ...(e.active === false ? { later: true } : {}),
  }))

  const rules = (node.specialRules ?? []).map((r) => ({ name: text(r.name), text: text(r.text) }))

  // Every token with something to read: those of the map, and those described without being on it
  const keys = [...new Set([...map.tokens.map((t) => tokenKey(t.type, t.color)), ...Object.keys(tokenRulesOf(node))])]
  // A token described without being on the map ("loot:yellow") takes the type of its category
  const typeOfKey = (key: string) =>
    map.tokens.find((t) => tokenKey(t.type, t.color) === key)?.type ??
    Object.keys(pieces?.tokens ?? {}).find((type) => type.split('/').pop() === key.split(':')[0])
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
    const piece = type ? pieces?.tokens[type] : undefined
    const image = (color && piece?.colors?.[color]) || piece?.image
    sections.set(group, [
      ...(sections.get(group) ?? []),
      { key, label: capitalize(label), ...(image ? { image } : {}), activation, text: hiddenText },
    ])
  }
  const hidden = [...sections]
    .sort(([a], [b]) => rank(a) - rank(b))
    .map(([group, entries]) => ({
      group,
      entries: [...entries].sort((a, b) => colorRank(a.key.split(':')[1]) - colorRank(b.key.split(':')[1])),
    }))

  return {
    id: node.id,
    name: text(node.name) || node.id,
    intro: text(node.intro),
    location: node.locationType,
    lootLevel: node.lootLevel,
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

/** The order of the colours in the books: blue, red, green, yellow, orange, purple… (stable otherwise). */
const COLORS = ['blue', 'red', 'green', 'yellow', 'orange', 'purple', 'pink', 'white', 'indigo']
const colorRank = (color: string | undefined) => {
  const i = color ? COLORS.indexOf(color) : -1
  return i < 0 ? COLORS.length : i
}

/** "UM5" before "UM31": the numbers compared as numbers. */
const byNumber = (a: string, b: string) => a.localeCompare(b, undefined, { numeric: true })

/**
 * A terrain token's name without its size ("Obstructing 1X2" → "Obstructing") nor the words the
 * books leave out ("Terrain Light Water" → "Water").
 */
export function terrainName(name: string): string {
  const bare = name
    .replace(/\s+\d+\s*[xX]\s*\d+$/, '')
    .replace(/^Terrain\s+/, '')
    .trim()
  return bare === 'Light Water' ? 'Water' : bare
}

/** A setup group printed in a column; `continued` when it began in the previous one. */
export interface SetupCell {
  group: string
  continued: boolean
  items: string[]
}

/**
 * The setup groups over `count` columns, as balanced as the books: whole groups when that is not
 * much taller (Totems | Objectives | Loot); otherwise a group too long goes on in the next column
 * ("Terrain (continued)"), and never starts with fewer than two of its lines. Heights are reckoned in
 * lines: a heading, then each item by its length.
 */
export function setupColumns(lines: SetupLine[], count = 3, lineLength = 28): SetupCell[][] {
  const HEADING = 1.3
  const weight = (item: string) => Math.max(1, Math.ceil(item.length / lineLength))
  const pack = (height: number, split: boolean): SetupCell[][] | undefined => {
    const columns: SetupCell[][] = [[]]
    let used = 0
    const next = () => {
      columns.push([])
      used = 0
    }
    for (const line of lines) {
      // A new group in this column needs its heading and two lines (or its only one)
      const opening = HEADING + line.items.slice(0, split ? 2 : undefined).reduce((sum, i) => sum + weight(i), 0)
      if (used > 0 && used + opening > height) next()
      let cell: SetupCell = { group: line.group, continued: false, items: [] }
      columns.at(-1)!.push(cell)
      used += HEADING
      for (const item of line.items) {
        if (split && cell.items.length > 0 && used + weight(item) > height) {
          next()
          cell = { group: line.group, continued: true, items: [] }
          columns.at(-1)!.push(cell)
          used += HEADING
        }
        cell.items.push(item)
        used += weight(item)
        // Whole groups: one taller than the column does not fit it
        if (!split && used > height + 1e-9) return undefined
      }
    }
    return columns.length <= count ? columns : undefined
  }
  const total = lines.reduce((sum, l) => sum + HEADING + l.items.reduce((s, i) => s + weight(i), 0), 0)
  const lowest = (split: boolean) => {
    for (let height = Math.max(1, total / count); height <= total + HEADING; height += 0.1) {
      const columns = pack(height, split)
      if (columns) return { height, columns }
    }
    return undefined
  }
  const whole = lowest(false)
  const cut = lowest(true)
  if (whole && (!cut || whole.height <= cut.height * 1.5)) return whole.columns
  return cut?.columns ?? [lines.map((l) => ({ group: l.group, continued: false, items: l.items }))]
}

const capitalize = (value: string) => value.charAt(0).toUpperCase() + value.slice(1)

/**
 * The steps of the scenario in reading order: the start first, then the order of the file, which is
 * the author's (the official books do not follow the exits: The Cave Sickle Queen prints the
 * alternate path, Sickle Spelunking, before Damp Depths).
 */
export function readingOrder(scenario: DraftScenario): DraftNode[] {
  const start = scenario.nodes.find((n) => n.id === scenario.start)
  return start ? [start, ...scenario.nodes.filter((n) => n !== start)] : [...scenario.nodes]
}

/** Setup groups printed beside the first half of a large map; the others go beside the second half. */
export const FIRST_HALF_GROUPS = ['tiles', 'terrain']
