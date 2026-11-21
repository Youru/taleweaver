import type { CatalogItem } from '@/features/catalog'
import type { AdventurerState, CampaignState, OwnedItem } from '@/features/games'
import type { TokenPiece } from '@/features/table'
import { pick, type Language } from '@/shared/lib/localized'

/** The item types, in the order of the card's limits (rulebook p. 25). */
export const itemTypes = ['weapon', 'armor', 'core', 'accessory', 'relic', 'consumable'] as const
export const itemTiers = ['mundane', 'common', 'uncommon', 'rare', 'wandering', 'monster', 'story'] as const

/** Equip limits of an adventurer (rulebook p. 25: 2 hands of weapons, 1 armor…), and the Pack's 3 Items. */
export const equipLimits: Record<string, number> = {
  weapon: 2,
  armor: 1,
  core: 1,
  accessory: 1,
  relic: 3,
  consumable: 3,
}
export const packSize = 3
/** "Increase your Pack Size by 6." */
const adventurersPack = /adventurer-s-pack$/

/** Sale price by tier, unique version doubled (rulebook p. 27, table 5). Story items are never sold. */
const salePrices: Record<string, number> = { mundane: 5, common: 10, uncommon: 15, rare: 20 }

const fold = (value: string) => value.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase()

export interface ItemFilter {
  query: string
  type?: string | null
  tier?: string | null
}

/**
 * The cards matching what the GM typed: every word in the name (any language), a tag or the id,
 * accents and case ignored; names starting with the query first, then alphabetical.
 */
export function searchItems(items: CatalogItem[], filter: ItemFilter, language: Language, limit = 80): CatalogItem[] {
  const words = fold(filter.query).split(/\s+/).filter(Boolean)
  const query = words.join(' ')
  const name = (item: CatalogItem) => fold(pick(item.name, language))
  return items
    .filter((item) => (!filter.type || item.type === filter.type) && (!filter.tier || item.tier === filter.tier))
    .filter((item) => {
      const haystack = fold([...Object.values(item.name), ...item.tags, item.id.split('/').pop() ?? ''].join(' '))
      return words.every((word) => haystack.includes(word))
    })
    .sort(
      (a, b) =>
        Number(query !== '' && !name(a).startsWith(query)) - Number(query !== '' && !name(b).startsWith(query)) ||
        name(a).localeCompare(name(b)),
    )
    .slice(0, limit)
}

/** Where an item is: the items to hand out, the stash, or an adventurer's equipment or Pack. */
export type Place = { owner: 'unassigned' } | { owner: 'stash' } | { owner: string; slot: 'equipped' | 'pack' }

export interface Located {
  owned: OwnedItem
  place: Place
}

/** Every item the party holds, with where it is. */
export function allItems(campaign: CampaignState): Located[] {
  return [
    ...(campaign.unassigned ?? []).map((owned) => ({ owned, place: { owner: 'unassigned' } as Place })),
    ...campaign.party.flatMap((a) => [
      ...(a.equipped ?? []).map((owned) => ({ owned, place: { owner: a.name, slot: 'equipped' } as Place })),
      ...(a.pack ?? []).map((owned) => ({ owned, place: { owner: a.name, slot: 'pack' } as Place })),
    ]),
    ...(campaign.stash ?? []).map((owned) => ({ owned, place: { owner: 'stash' } as Place })),
  ]
}

export function samePlace(a: Place, b: Place): boolean {
  return a.owner === b.owner && ('slot' in a ? a.slot : null) === ('slot' in b ? b.slot : null)
}

export interface LimitWarning {
  /** over: more items of a type than equipped allowed; pack: Pack full; relic: same Relic twice. */
  kind: 'over' | 'pack' | 'relic'
  type?: string
  count?: number
  max?: number
  name?: string
}

/**
 * What breaks the limits of the rules (rulebook p. 25): too many items of a type equipped, too many in
 * the Pack, the same Relic twice. The app warns; the table decides.
 */
export function limitWarnings(
  adventurer: AdventurerState,
  catalog: Map<string, CatalogItem>,
  language: Language,
): LimitWarning[] {
  const warnings: LimitWarning[] = []
  const equipped = (adventurer.equipped ?? []).flatMap((o) => (o.item ? [catalog.get(o.item)] : [])).filter(isItem)
  for (const type of itemTypes) {
    const count = equipped.filter((item) => item.type === type).length
    const max = equipLimits[type] ?? Infinity
    if (count > max) warnings.push({ kind: 'over', type, count, max })
  }
  const pack = adventurer.pack ?? []
  const max = packSize + 6 * equipped.filter((item) => adventurersPack.test(item.id)).length
  if (pack.length > max) warnings.push({ kind: 'pack', count: pack.length, max })
  const relics = [...equipped, ...pack.flatMap((o) => (o.item ? [catalog.get(o.item)] : [])).filter(isItem)].filter(
    (item) => item.type === 'relic',
  )
  for (const [id, count] of countBy(relics.map((r) => r.id)))
    if (count > 1) warnings.push({ kind: 'relic', name: pick(catalog.get(id)?.name, language), count })
  return warnings
}

/** How many items of each type are equipped: "Weapons 1/2". */
export function equippedCounts(adventurer: AdventurerState, catalog: Map<string, CatalogItem>) {
  const equipped = (adventurer.equipped ?? []).flatMap((o) => (o.item ? [catalog.get(o.item)] : [])).filter(isItem)
  return itemTypes
    .map((type) => ({ type, count: equipped.filter((i) => i.type === type).length, max: equipLimits[type] ?? 0 }))
    .filter((c) => c.count > 0)
}

/** What the card sells for (rulebook p. 27); undefined when it has no sale price (story, monster loot…). */
export function salePrice(item: CatalogItem | undefined): number | undefined {
  if (!item || item.story || !item.tier) return undefined
  const price = salePrices[item.tier]
  return price === undefined ? undefined : item.unique ? price * 2 : price
}

/**
 * The token a card places on the board ("Place a Plank token on an adjacent space"), found in the
 * catalogue by name: the smallest token whose name has every word ("Small Plank" before "Large Plank").
 * Tokens only put on cards (Energy, Used) say no space and are not proposed.
 */
export function suggestedToken(
  item: CatalogItem | undefined,
  tokens: Record<string, TokenPiece> | undefined,
): string | undefined {
  if (!item || !tokens) return undefined
  const text = [item.text?.en, item.backSide?.text.en].filter(Boolean).join('\n').replace(/\s+/g, ' ')
  const match = /place (?:a|an|\d+) (?:IXI )?([A-Za-z][A-Za-z ]*?) token[^.]*?\bspaces?\b/i.exec(text)
  if (!match?.[1]) return undefined
  const words = fold(match[1]).split(' ')
  const area = (size?: string) => {
    const [w = 1, h = 1] = (size ?? '1x1').split('x').map(Number)
    return w * h
  }
  return Object.entries(tokens)
    .filter(([, piece]) => piece.category !== 'ability')
    .filter(([, piece]) => {
      const name = fold(piece.name.en ?? '')
      return words.every((word) => name.includes(word))
    })
    .sort(([a, x], [b, y]) => area(x.size) - area(y.size) || a.localeCompare(b))[0]?.[0]
}

/** A Consumable, or a card that says X DISCARD: used once, then discarded. */
export function discardedOnUse(item: CatalogItem | undefined): boolean {
  if (!item) return false
  return item.type === 'consumable' || item.tags.includes('X DISCARD') || /\bX DISCARD\b/.test(item.text?.en ?? '')
}

function isItem(item: CatalogItem | undefined): item is CatalogItem {
  return !!item
}

function countBy(values: string[]): Map<string, number> {
  const counts = new Map<string, number>()
  for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1)
  return counts
}

/** One space of the adventurer's mat: a card in it, or empty; `extra` when over the type's limit. */
export interface MatSlot {
  key: string
  owned?: OwnedItem
  extra?: boolean
}

/**
 * The adventurer's equipment laid out like a player mat: one row per item type with its spaces
 * (2 Weapons, 1 Armor…), empty spaces shown, cards over the limit added in red; then the Pack, with
 * its spaces (3, +6 per Adventurer's Pack). Cards of unknown type (still to draw) go in "other".
 */
export function playerMat(adventurer: AdventurerState, catalog: Map<string, CatalogItem>) {
  const typeOf = (o: OwnedItem) => (o.item ? catalog.get(o.item)?.type : o.type) ?? 'other'
  const equipped = adventurer.equipped ?? []
  const fill = (key: string, cards: OwnedItem[], spaces: number): MatSlot[] => [
    ...cards.map((owned, i) => ({ key: owned.id, owned, extra: i >= spaces })),
    ...Array.from({ length: Math.max(0, spaces - cards.length) }, (_, i) => ({ key: `${key}-${i}` })),
  ]
  const rows = itemTypes.map((type) => ({
    type,
    slots: fill(type, equipped.filter((o) => typeOf(o) === type), equipLimits[type] ?? 0),
  }))
  const other = equipped.filter((o) => !(itemTypes as readonly string[]).includes(typeOf(o)))
  if (other.length) rows.push({ type: 'other' as never, slots: fill('other', other, 0).map((s) => ({ ...s, extra: false })) })
  const packSpaces =
    packSize +
    6 * equipped.filter((o) => o.item && adventurersPack.test(catalog.get(o.item)?.id ?? '')).length
  return { rows, pack: fill('pack', adventurer.pack ?? [], packSpaces), packSpaces }
}
