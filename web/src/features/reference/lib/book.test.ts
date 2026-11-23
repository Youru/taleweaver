import { describe, expect, it } from 'vitest'
import { parseLocalizedBook } from './book'

const english = [
  '# Moving',
  '<!-- p.40 -->',
  '## The Move Action',
  'Move up to your speed.',
  '',
  '## Jumping',
  'Jump over a gap.',
].join('\n')
const french = [
  '# Se déplacer',
  '<!-- p.40 -->',
  "## L'action Bouger",
  'Bougez de votre vitesse.',
  '',
  '## Sauter',
  '<!-- EN --> Jump over a gap.',
].join('\n')

describe('parseLocalizedBook', () => {
  it('shows the translation under the English section ids', () => {
    const book = parseLocalizedBook([{ id: '07', markdown: english, translations: { fr: french } }], 'fr')
    expect(book.sections.map((s) => s.id)).toEqual(['07/the-move-action', '07/jumping'])
    expect(book.sections[0]!.title).toBe("L'action Bouger")
    expect(book.chapters[0]!.title).toBe('Se déplacer')
  })

  it('shows a line left untranslated in English, without its mark', () => {
    const book = parseLocalizedBook([{ id: '07', markdown: english, translations: { fr: french } }], 'fr')
    expect(book.sections[1]!.blocks[0]).toMatchObject({ kind: 'paragraph', text: 'Jump over a gap.' })
  })

  it('keeps a chapter in English when its sections do not match', () => {
    const broken = french.replace('## Sauter\n', '')
    const book = parseLocalizedBook([{ id: '07', markdown: english, translations: { fr: broken } }], 'fr')
    expect(book.sections[0]!.title).toBe('The Move Action')
  })

  it('reads the English without translations', () => {
    const book = parseLocalizedBook([{ id: '07', markdown: english }], 'fr')
    expect(book.sections[0]!.title).toBe('The Move Action')
  })
})
