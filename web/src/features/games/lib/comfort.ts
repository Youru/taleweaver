import type { ScenarioNode, SpecialRule } from '@/features/scenarios'
import type { CampaignState, EncounterState, GameOptions, GameState, OwnedItem } from '../api/types'
import { adventurerOf } from './play'

/*
 * What the companion proposes to the GM (play-spec lot 5). Nothing here applies anything: every function reads
 * the game and the scenario, and the GM decides.
 */

/** M2: the index of the action that wrote this line; undefined when it cannot be gone back on. */
export function actionOfLine(actions: readonly number[], line: number): number | undefined {
  // Several actions may begin on one line (those that wrote nothing): the last one wrote it
  for (let i = actions.length - 1; i >= 0; i--) {
    const begins = actions[i] ?? -1
    if (begins < 0) return undefined
    if (begins <= line) return i
  }
  return undefined
}

const lastCardPlays = (encounter: EncounterState) =>
  encounter.round > 0 && encounter.initiativeTrack.every((card) => encounter.playedCards?.includes(card))

/** M11: ending the turn under way ends a round that gives an Urgency (rulebook p. 18). */
export function urgencyComing(encounter: EncounterState, options?: GameOptions | null): boolean {
  if (options?.removeUrgency || encounter.ended || encounter.activation || !lastCardPlays(encounter)) return false
  const round = encounter.roundLog
  return !round?.waived && !round?.aiStepTrue && !round?.defeated && !round?.spawned
}

/**
 * M10: the encounter's rules to remember now. All of them before the first round; then those that speak of the
 * start of a round on its first card, of its end on its last, and of whoever plays on their turn.
 */
export function remindersFor(encounter: EncounterState, node: ScenarioNode | undefined): SpecialRule[] {
  const rules = node?.specialRules ?? []
  if (encounter.round === 0) return rules
  const card = encounter.initiativeTrack[encounter.currentTurn]
  const first = encounter.playedCards?.length === 1
  const last = lastCardPlays(encounter)
  const adventurer = adventurerOf(card)
  const families = encounter.combatants
    .filter((c) => !c.defeated && card && c.initiativeCards?.includes(card))
    .map((c) => c.family.replaceAll('-', ' ').toLowerCase())
  return rules.filter((rule) => {
    const text = `${rule.name?.en ?? ''} ${rule.text?.en ?? ''}`.toLowerCase()
    return (
      (first && /(start|beginning) of (the|each|every|a) round/.test(text)) ||
      (last && /end of (the|each|every|a) round/.test(text)) ||
      (!!adventurer && /adventurers?['’]?s? turn|encounter action/.test(text)) ||
      families.some((family) => text.includes(family))
    )
  })
}

export interface Proposal {
  kind: 'end' | 'achievement'
  id: string
}

/**
 * M12: what looks met. The format says it in words only, so the English text is read: a family named or every
 * opponent, all Defeated; a way out with an adventurer standing on it.
 */
export function proposals(encounter: EncounterState, node: ScenarioNode | undefined): Proposal[] {
  if (!node || encounter.ended) return []
  const opponents = encounter.combatants.filter((c) => c.side !== 'players')
  const waiting = !!encounter.waiting?.length
  const onExit = (encounter.tokens ?? []).some(
    (token) =>
      !token.removed &&
      /exit|escape/.test(token.key) &&
      encounter.adventurers.some(({ defeated, position: p }) => {
        const area = token.area
        return !defeated && p && area && p.x >= area.x && p.x < area.x + area.w && p.y >= area.y && p.y < area.y + area.h
      }),
  )
  const met = (words: string | undefined) => {
    const text = words?.toLowerCase() ?? ''
    if (onExit && /exit|escape/.test(text)) return true
    if (!opponents.length || !/defeat|kill|slay|destroy/.test(text)) return false
    const named = opponents.filter((c) => text.includes(c.family.replaceAll('-', ' ').toLowerCase()))
    if (!named.length && !/(all|every|each)( of)?( the)?( \w+)? (opponent|combatant|enem|figure)/.test(text)) return false
    return !waiting && (named.length ? named : opponents).every((c) => c.defeated)
  }
  const ends = (node.endConditions ?? [])
    .filter((e) => e.kind === 'win' && encounter.activeEndConditions.includes(e.id) && met(e.text?.en))
    .map((e): Proposal => ({ kind: 'end', id: e.id }))
  const achievements = (node.achievements ?? [])
    .filter((a) => met(a.condition?.en))
    .map((a): Proposal => ({ kind: 'achievement', id: a.id }))
  return [...ends, ...achievements]
}

/** M18: the Consumables "Explore" discards, with who holds them. */
export function consumablesAtRisk(
  campaign: CampaignState,
  typeOf: (item: string) => string | undefined,
): { owned: OwnedItem; holder: string }[] {
  return [
    ...(campaign.stash ?? []).map((owned) => ({ owned, holder: 'stash' })),
    ...(campaign.unassigned ?? []).map((owned) => ({ owned, holder: 'unassigned' })),
    ...campaign.party.flatMap((a) => (a.pack ?? []).map((owned) => ({ owned, holder: a.name }))),
  ].filter(({ owned }) => ((owned.item && typeOf(owned.item)) || owned.type) === 'consumable')
}

/** Every step a node may lead to, wherever it says so. */
function leadsTo(value: unknown, found: string[] = []): string[] {
  if (Array.isArray(value)) value.forEach((each) => leadsTo(each, found))
  else if (value && typeof value === 'object') {
    for (const [key, inner] of Object.entries(value)) {
      if ((key === 'next' || key === 'node') && typeof inner === 'string') found.push(inner)
      else leadsTo(inner, found)
    }
  }
  return found
}

/** M5: the first encounter on each way from here. */
export function encountersAhead(nodes: readonly ScenarioNode[], from: string | undefined): ScenarioNode[] {
  const byId = new Map(nodes.map((node) => [node.id, node]))
  const start = from ? byId.get(from) : undefined
  if (!start) return []
  const ahead: ScenarioNode[] = []
  const seen = new Set([start.id])
  const queue = leadsTo(start)
  for (let id = queue.shift(); id !== undefined; id = queue.shift()) {
    const node = byId.get(id)
    if (!node || seen.has(id)) continue
    seen.add(id)
    if (node.type === 'encounter') ahead.push(node)
    else queue.push(...leadsTo(node))
  }
  return ahead
}

export const materialKinds = ['tiles', 'tokens', 'figures', 'bases', 'cards', 'initiative'] as const
export type MaterialKind = (typeof materialKinds)[number]
export type Material = Record<MaterialKind, { id: string; count: number }[]>

function counted(ids: (string | null | undefined)[]) {
  const counts = new Map<string, number>()
  for (const id of ids) if (id) counts.set(id, (counts.get(id) ?? 0) + 1)
  return [...counts].map(([id, count]) => ({ id, count }))
}

/** M8, M5: what to take out of the box; without an encounter, what the scenario places at the start. */
export function materialOf(node: ScenarioNode | undefined, encounter?: EncounterState): Material {
  const setup = node?.setup as
    | { combatants?: { family?: string; ref?: string; figure?: string; initiativeCard?: string }[] }
    | undefined
  const standing = encounter?.combatants.filter((c) => !c.defeated)
  const placed = setup?.combatants ?? []
  const diagrams = (node?.diagrams ?? []).filter((d) => encounter?.placedDiagrams?.includes(d.id))
  return {
    tiles: counted(
      [...(node?.map?.tiles ?? []), ...diagrams.flatMap((d) => d.map?.tiles ?? [])].map((tile) => tile.ref),
    ),
    tokens: counted(
      encounter
        ? (encounter.tokens ?? []).filter((t) => !t.removed).map((t) => t.key)
        : (node?.map?.tokens ?? []).map((t) => [t.type, t.color].filter(Boolean).join(':')),
    ),
    figures: counted([
      ...(standing?.map((c) => c.figure ?? c.family) ?? placed.map((c) => c.figure ?? c.family ?? c.ref)),
      ...(encounter?.adventurers.map((a) => a.name) ?? []),
      ...(encounter?.allies?.map((a) => a.family) ?? []),
    ]),
    bases: counted(standing?.filter((c) => c.number).map((c) => String(c.number)) ?? []),
    cards: counted([...new Set(standing?.map((c) => c.card) ?? placed.map((c) => c.ref ?? c.family))]),
    initiative: counted([...new Set(encounter?.initiativeTrack ?? placed.map((c) => c.initiativeCard))]),
  }
}

/** M15: the adventurer waited for; null: the GM. */
export function waitingFor(state: Pick<GameState, 'prompt' | 'encounter'>): string | null {
  const { prompt, encounter } = state
  const reacting = adventurerOf(encounter?.reactions?.at(-1)?.figure)
  if (prompt) return prompt.decider ?? adventurerOf(prompt.instance) ?? null
  if (reacting) return reacting
  if (!encounter || encounter.ended || encounter.round === 0) return null
  const card = encounter.initiativeTrack[encounter.currentTurn]
  const ally = encounter.allies?.find((a) => !a.defeated && a.trackCard === card)
  return adventurerOf(card) ?? ally?.owner ?? null
}

export interface ReadPart {
  text: string
  speech: boolean
  who?: string
}

const said = 'says|said|asks|asked|replies|replied|whispers|whispered|shouts|shouted|dit|demande|répond|murmure|crie'

/**
 * M19: the speeches of a paragraph, each with its character when the paragraph leaves no doubt: one name with a
 * verb of speech, or one name of the party outside the quotes.
 */
export function speeches(paragraph: string, names: readonly string[]): ReadPart[] {
  const parts: ReadPart[] = paragraph
    .split(/(“[^”]*”|"[^"]*"|«[^»]*»)/)
    .map((text, i) => ({ text, speech: i % 2 === 1 }))
    .filter((part) => part.text)
  const outside = parts
    .filter((part) => !part.speech)
    .map((part) => part.text)
    .join(' ')
  const speakers = new Set<string>()
  const pattern = new RegExp(`(\\p{Lu}[\\p{L}'-]+) (?:${said})(?!\\p{L})|(?:${said}) (\\p{Lu}[\\p{L}'-]+)`, 'gu')
  for (const match of outside.matchAll(pattern)) {
    const name = match[1] ?? match[2]
    if (name && !/^(He|She|They|It|I|We|You|Il|Elle|On|Ils|Elles|The|Le|La)$/.test(name)) speakers.add(name)
  }
  const mentioned = names.filter((name) => outside.includes(name))
  const only = speakers.size === 1 ? [...speakers][0] : !speakers.size && mentioned.length === 1 ? mentioned[0] : undefined
  return parts.map((part) => (part.speech && only ? { ...part, who: only } : part))
}
