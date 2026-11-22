import { describe, expect, it } from 'vitest'
import { messages } from '@/shared/i18n/messages'
import { en } from '../content/en'
import { fr } from '../content/fr'
import { piecesOf, type Manual } from './manual'

const manuals = { fr, en }

function sectionsOf(manual: Manual) {
  return manual.groups.flatMap((group) => group.sections)
}
function piecesIn(manual: Manual) {
  return sectionsOf(manual)
    .flatMap((s) => [s.lead, ...s.parts.flatMap((p) => [p.text, ...(p.steps ?? []), ...(p.list ?? []), p.note])])
    .flatMap((text) => piecesOf(text ?? ''))
}

describe('the manual', () => {
  it('cuts a text at its references', () => {
    expect(piecesOf('[[home.play]], puis [[#room]].')).toEqual([
      { label: 'home.play' },
      { text: ', puis ' },
      { section: 'room' },
      { text: '.' },
    ])
  })

  it.each(['fr', 'en'] as const)('names only labels and sections that exist (%s)', (language) => {
    const ids = sectionsOf(manuals[language]).map((s) => s.id)
    const broken = piecesIn(manuals[language]).filter((piece) => {
      if ('section' in piece) return !ids.includes(piece.section)
      if (!('label' in piece)) return false
      const label = piece.label
        .split('.')
        .reduce<unknown>((at, key) => (at as Record<string, unknown> | undefined)?.[key], messages[language])
      // A label with a value to fill in would show its braces
      return typeof label !== 'string' || /[{|]/.test(label)
    })
    expect(broken).toEqual([])
  })

  it('says the same in both languages', () => {
    const outline = (manual: Manual) =>
      sectionsOf(manual).map((s) => ({
        id: s.id,
        parts: s.parts.map((p) => [!!p.title, !!p.text, p.steps?.length, p.list?.length, !!p.note]),
      }))
    const references = (manual: Manual) => piecesIn(manual).filter((piece) => !('text' in piece))
    expect(outline(en)).toEqual(outline(fr))
    expect(references(en)).toEqual(references(fr))
  })
})
