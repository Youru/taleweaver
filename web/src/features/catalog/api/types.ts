import type { LocalizedText } from '@/shared/lib/localized'

/** The parts of Taleweaver.Api.Content.Combatant the front reads. */
export interface Combatant {
  id: string
  family: string
  name: LocalizedText
  subtitle?: LocalizedText
  kind: string
  lootLevel?: string
  activation?: string
  stats?: { health?: number | '*'; defense?: number | '*'; movement?: number | '*'; armor?: number | '*' }
}

export interface Effect {
  id: string
  name: LocalizedText
}
