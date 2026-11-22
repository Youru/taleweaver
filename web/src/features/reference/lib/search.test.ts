import { readdirSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { findReference, parseBook } from './book'
import { readInline } from './inline'
import { questions } from './questions'
import { createSearch } from './search'

// On the real book: a search is only good on the text it searches
const folder = '../../reference/rules/rulebook/'
const read = (path: string) => readFileSync(path, 'utf8')
const book = parseBook(
  readdirSync(`${folder}chapters`).map((file) => ({
    id: file.split('-')[0]!,
    markdown: read(`${folder}chapters/${file}`),
  })),
)
const rules = createSearch(book, read(`${folder}glossary.md`), JSON.parse(read('../../packs/um1/rules.json')))

describe('rules search', () => {
  it.each([
    ['comment sauter', 'Jumping'],
    ['attaque d’opportunité', 'Break Attacks'],
    ['quitter le contact', 'Break Attacks'],
    ['ligne de vue', 'Line of Sight'],
    ['que se passe-t-il quand on meurt', 'Being Defeated'],
    ['esquive', 'Dodge'],
    ['riposte', 'Counter'],
    ['dans le dos', 'Flanking and Backstab'],
    ['combien de PE par tour', 'Phases of an Adventurer’s Turn'],
    ['arrondi', 'Rounding Numbers'],
    ['relancer un dé', 'Re-Rolling Dice'],
    ['revendre un objet', 'Selling Items'],
    ['test de compétence', 'Adventurer Skill Checks'],
    ['grand socle', 'Large Base Figures'],
    ['urgence', 'Urgency Tokens'],
    ['totem', 'Totems'],
    ['hauteur', 'Elevation'],
    ['qui joue en premier', 'The Initiative Track'],
    ['cible IA', 'Targeting Priority'],
    ['up to within', '“Up To” vs “Within”'],
    ['break attac', 'Break Attacks'],
  ])('"%s" opens %s', (typed, title) => {
    expect(rules.search(typed).hits[0]?.section.title).toBe(title)
  })

  it('finds an effect by its French name, on its row of the table', () => {
    const found = rules.search('empoisonné')
    expect(found.definitions[0]?.entry.id).toBe('poison')
    expect(found.hits[0]?.passage.label).toContain('Poison')
  })

  it('forgives a typing mistake, and says so', () => {
    expect(rules.search('esquve').corrections).toEqual([{ typed: 'esquve', read: 'esquive' }])
    expect(rules.search('posion').definitions[0]?.entry.id).toBe('poison')
  })

  it('puts the question of the table before the book', () => {
    expect(rules.search('les effets se cumulent').questions[0]?.question.section).toContain('Limitations')
  })

  it('says nothing for nothing', () => {
    expect(rules.search('est-ce que').hits).toEqual([])
    expect(rules.search('xqzvbnm').hits).toEqual([])
  })

  it('has a place in the book for every question, and the words it points at', () => {
    expect(rules.questions).toHaveLength(questions.length)
    for (const { question, hit } of rules.questions) {
      if (question.find) expect(hit.passage.raw.toLowerCase()).toContain(question.find.toLowerCase())
    }
  })
})

describe('rulebook', () => {
  it('gives each rule an address of its own', () => {
    expect(new Set(book.sections.map(({ id }) => id)).size).toBe(book.sections.length)
  })

  it('follows a reference by its name, else by its page', () => {
    const link = readInline('See **Being Defeated** Pg. 69.').find((piece) => piece.link)!.link!
    expect(findReference(book, link.page, link.name)?.title).toBe('Being Defeated')
    expect(findReference(book, 36)?.title).toBe('The Initiative Track')
  })

  it('every page cited leads somewhere', () => {
    const texts = book.sections.flatMap((s) => s.blocks).flatMap((b) => ('text' in b ? [b.text] : b.rows.flat()))
    const lost = texts
      .flatMap((text) => readInline(text))
      .flatMap((piece) => piece.link ?? [])
      .filter((link) => link.page >= 4 && link.page <= 100 && !findReference(book, link.page, link.name))
    expect(lost).toEqual([])
  })
})
