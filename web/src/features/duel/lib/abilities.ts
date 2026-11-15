import type { SymbolAbilityCard } from '@/features/catalog'
import { pick, type Language } from '@/shared/lib/localized'
import type { Symbols } from './dice'

/** What spending symbols adds to an Attack, when the effect is one of the four counted ones. */
export type BoostKind = 'toHit' | 'physical' | 'magic' | 'pierce'

export interface SymbolAbility {
  cost: Symbols
  /** Undefined for effects the GM plays out (Follow Up, Heal…). */
  boost?: { kind: BoostKind; amount: number }
  text: string
}

const boosts: [RegExp, BoostKind][] = [
  [/^\+(\d+) TO THE ATK ROLL$/i, 'toHit'],
  [/^\+(\d+) PHYSICAL DMG$/i, 'physical'],
  [/^\+(\d+) MAGIC DMG$/i, 'magic'],
  [/^\+(\d+) ARMOR PIERCING$/i, 'pierce'],
]

/**
 * The symbol abilities of a card, ready to spend. Costs read "book", "shield×2"; the ones paid with
 * another card's symbols ("*star", an Esper's) are left out: that card is not known here.
 */
export function symbolAbilities(cards: SymbolAbilityCard[] | undefined, language: Language): SymbolAbility[] {
  const abilities: SymbolAbility[] = []
  for (const card of cards ?? []) {
    const cost: Symbols = { shield: 0, book: 0, star: 0 }
    let known = true
    for (const { symbol } of card.cost) {
      const match = /^(shield|book|star)(?:×(\d+))?$/.exec(symbol)
      if (!match) {
        known = false
        break
      }
      cost[match[1] as keyof Symbols] += Number(match[2] ?? 1)
    }
    if (!known || cost.shield + cost.book + cost.star === 0) continue
    const text = pick(card.effect, 'en').trim()
    const boost = boosts.map(([pattern, kind]) => ({ kind, match: pattern.exec(text) })).find((b) => b.match)
    abilities.push({
      cost,
      boost: boost?.match ? { kind: boost.kind, amount: Number(boost.match[1]) } : undefined,
      text: pick(card.effect, language),
    })
  }
  return abilities
}
