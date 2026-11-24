import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { findReference, parseBook, parseLocalizedBook } from './book'
import { readInline } from './inline'
import { questions } from './questions'
import { createSearch } from './search'

// On the real book: a search is only good on the text it searches. Skipped where the game's content is absent (CI).
const root = process.env.TALEWEAVER_CONTENT_ROOT ? `${process.env.TALEWEAVER_CONTENT_ROOT}/` : '../../'
const folder = `${root}reference/rules/rulebook/`
const present = existsSync(`${folder}glossary.md`)
const read = (path: string) => readFileSync(path, 'utf8')
const book = present
  ? parseBook(
      readdirSync(`${folder}chapters`).map((file) => ({
        id: file.split('-')[0]!,
        markdown: read(`${folder}chapters/${file}`),
      })),
    )
  : parseBook([])
const rules = present
  ? createSearch(book, read(`${folder}glossary.md`), JSON.parse(read(`${root}packs/um1/rules.json`)))
  : createSearch(book, '')

describe.skipIf(!present)('rules search', () => {
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

describe.skipIf(!present)('rulebook', () => {
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

// The French book (reference/fr/…/chapters, line for line): every question still finds its rule
const frFolder = `${root}reference/fr/rules/rulebook/chapters/`
const frenchPresent = present && existsSync(frFolder)
// A skipped suite still runs its body to collect its tests: without the book (the public CI), read nothing
describe.skipIf(!frenchPresent)('rules search in French', () => {
  if (!frenchPresent) return
  const chapters = readdirSync(`${folder}chapters`).map((file) => ({
    id: file.split('-')[0]!,
    markdown: read(`${folder}chapters/${file}`),
    translations: existsSync(`${frFolder}${file}`) ? { fr: read(`${frFolder}${file}`) } : undefined,
  }))
  const french = parseLocalizedBook(chapters, 'fr')
  const search = createSearch(french, read(`${folder}glossary.md`), JSON.parse(read(`${root}packs/um1/rules.json`)))

  it('reads every chapter in French, under the English section ids', () => {
    expect(french.sections.map((s) => s.id)).toEqual(book.sections.map((s) => s.id))
    expect(french.sections.filter((s) => s.english).length).toBe(book.sections.length)
  })

  it('answers every question of the table', () => {
    expect(search.questions.length).toBe(questions.length)
    const english = createSearch(book, '')
    for (const [i, { hit }] of search.questions.entries()) {
      const at = english.questions[i]!.hit
      expect(hit.section.id).toBe(at.section.id)
      expect(hit.passage?.block).toBe(at.passage?.block)
    }
  })

  it('finds a rule by its English words too, and still shows it in French', () => {
    const [first] = search.search('line of sight').hits
    expect(first?.section.english?.title).toMatch(/line of sight/i)
    expect(first?.section.title).not.toBe(first?.section.english?.title)
    expect(search.search('ligne de vue').hits[0]?.section.id).toBe(first?.section.id)
  })
})
