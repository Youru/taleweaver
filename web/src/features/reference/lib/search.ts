import type { LocalizedText } from '@/shared/lib/localized'
import type { Reference, RuleEntry } from '../api/queries'
import { passagesOf, type Book, type Passage, type Section } from './book'
import { questions, type Question } from './questions'
import { tables, type Table } from './reference'
import { distance, normalize, singular, termPattern } from './text'
import { emptyWords, spokenWords } from './vocabulary'

/** One thing asked: what was typed, and every way the book may write it. */
interface Concept {
  said: string
  terms: string[]
  patterns: RegExp[]
}

export interface Hit {
  section: Section
  /** The passage that answers best. */
  passage: Passage
  score: number
}

export interface Found {
  hits: Hit[]
  questions: { question: Question; hit: Hit }[]
  definitions: { table: Table; entry: RuleEntry }[]
  /** The words to mark in the text. */
  marks: string[]
  /** A word read otherwise: a typing mistake. */
  corrections: { typed: string; read: string }[]
  /** Nothing holds every word: the hits hold some of them. */
  partial: boolean
}

const mostHits = 25
const longestPhrase = 5
// Introduction and lists of components rarely answer a question
const chapterWeight: Record<string, number> = { '01': 0.3, '02': 0.45, '03': 0.4 }

const count = (text: string, pattern: RegExp) => text.match(pattern)?.length ?? 0
const key = (text: string) => normalize(text).split(' ').map(singular).join(' ')
const allText = (text: LocalizedText | null | undefined) => normalize(Object.values(text ?? {}).join(' '))

/** The French words of the glossary, and those the table says: key → English terms. */
export function buildAliases(glossary: string): Map<string, string[]> {
  const aliases = new Map<string, string[]>()
  const add = (said: string, written: string[]) => {
    const from = key(said)
    if (!from || (!from.includes(' ') && emptyWords.has(normalize(said)))) return
    const to = written.map(normalize).filter((term) => term.length > 1)
    aliases.set(from, [...new Set([...(aliases.get(from) ?? []), ...to])])
  }
  for (const [said, written] of spokenWords) {
    for (const way of said.split('|')) add(way, written.split('|'))
  }
  for (const line of glossary.split(/\r?\n/)) {
    const row = /^\|([^|]+)\|([^|]+)\|/.exec(line)
    if (!row || row[1]!.includes('---') || row[1]!.trim() === 'EN') continue
    const names = (cell: string) =>
      cell
        .replace(/\([^)]*\)|…/g, '')
        .replace(/^[^:]+:/, '')
        .split(/[,/]/)
        .map((name) => name.trim())
        .filter(Boolean)
    const english = names(row[1]!)
    const french = names(row[2]!)
    french.forEach((name, i) => add(name, english.length === french.length ? [english[i]!] : english))
  }
  return aliases
}

export function createSearch(book: Book, glossary: string, reference?: Reference) {
  const aliases = buildAliases(glossary)
  const indexed = book.sections.map((section) => ({
    section,
    title: normalize(section.title),
    parent: normalize(section.parent ?? ''),
    weight: (chapterWeight[section.chapter] ?? 1) * (/example|breakdown/i.test(section.title) ? 0.6 : 1),
    passages: passagesOf(section).map((passage) => ({
      passage,
      text: normalize(passage.raw),
      label: normalize(passage.label),
    })),
  }))
  const asked = questions.flatMap((question) => {
    const at = indexed.find(
      ({ section, title }) => section.chapter === question.chapter && title === normalize(question.section),
    )
    if (!at) return []
    const find = normalize(question.find ?? '')
    const answer = at.passages.find(({ text }) => find && text.includes(find)) ?? at.passages[0]!
    const hit: Hit = { section: at.section, passage: answer.passage, score: 0 }
    return [{ question, hit, text: allText(question.text) }]
  })
  const entries = new Map<string, { table: Table; entry: RuleEntry; name: string }>()
  for (const table of reference ? tables : []) {
    for (const entry of reference![table]) {
      const name = allText(entry.name)
      // Some tags are printed in two tables
      if (entry.text && !entries.has(`${table}:${name}`)) entries.set(`${table}:${name}`, { table, entry, name })
    }
  }

  const bookWords = new Set(
    indexed.flatMap(({ title, passages }) => [title, ...passages.map(({ text }) => text)].join(' ').split(' ')),
  )
  const questionWords = new Set(asked.flatMap(({ text }) => text.split(' ')))
  const known = [...bookWords, ...questionWords, ...[...aliases.keys()].filter((from) => !from.includes(' '))]
  // While a word is being typed, its beginning is enough
  const isKnown = (word: string) => known.some((other) => other.startsWith(singular(word)))

  function read(typed: string) {
    const words = normalize(typed).split(' ').filter(Boolean)
    const concepts: Concept[] = []
    const corrections: Found['corrections'] = []
    const add = (said: string, terms: string[]) => {
      const all = [...new Set(terms)]
      if (!concepts.some((concept) => concept.said === said))
        concepts.push({ said, terms: all, patterns: all.map(termPattern) })
    }
    for (let i = 0; i < words.length;) {
      let taken = 0
      // The longest phrase first: "ligne de vue" before "vue"
      for (let size = Math.min(longestPhrase, words.length - i); size > 0 && !taken; size--) {
        const phrase = words.slice(i, i + size)
        if (size === 1 && emptyWords.has(phrase[0]!)) continue
        const written = aliases.get(key(phrase.join(' ')))
        if (!written) continue
        add(phrase.join(' '), size === 1 ? [...written, singular(phrase[0]!)] : written)
        taken = size
      }
      if (!taken) {
        const word = words[i]!
        taken = 1
        if (!emptyWords.has(word) && (word.length > 1 || /\d/.test(word))) {
          let understood = word
          if (word.length >= 4 && !isKnown(word)) {
            const most = word.length >= 7 ? 2 : 1
            let best = most + 1
            for (const other of known) {
              if (other.length < 4) continue
              const gap = distance(word, other, most)
              if (gap < best) {
                best = gap
                understood = other
              }
            }
            if (understood !== word) corrections.push({ typed: word, read: understood })
          }
          add(understood, [...(aliases.get(key(understood)) ?? []), singular(understood)])
        }
      }
      i += taken
    }
    return { concepts, corrections }
  }

  return {
    questions: asked,
    search(typed: string): Found {
      const { concepts, corrections } = read(typed)
      const found: Found = { hits: [], questions: [], definitions: [], marks: [], corrections, partial: false }
      if (!concepts.length) return found

      const phrase = concepts.map(({ terms }) => terms[0]).join(' ')
      const spread = concepts.map(({ patterns }) => {
        const holders = indexed.filter(({ title, passages }) =>
          patterns.some((p) => count(title, p) || passages.some(({ text }) => count(text, p))),
        ).length
        // A rare word says more than "attack"
        return Math.log(1 + indexed.length / (1 + holders))
      })

      const scored = indexed.flatMap((entry) => {
        let score = 0
        let covered = 0
        const perPassage = entry.passages.map(() => 0)
        concepts.forEach(({ patterns }, c) => {
          let best = 0
          patterns.forEach((pattern, rank) => {
            // The first translation is the intended one, the others are help
            const trust = rank ? 0.6 : 1
            let inText = 0
            let named = 0
            entry.passages.forEach(({ text, label }, i) => {
              const times = count(text, pattern)
              if (!times) return
              const labelled = count(label, pattern) > 0
              inText += times
              if (labelled) named = 8
              perPassage[i]! += (Math.min(times, 3) + (labelled ? 6 : 0)) * spread[c]! * trust
            })
            const value =
              (count(entry.title, pattern) ? 22 : 0) +
              (count(entry.parent, pattern) ? 5 : 0) +
              named +
              Math.log2(1 + inText) * 2
            best = Math.max(best, value * trust)
          })
          if (best) covered++
          score += best * spread[c]!
        })
        if (!covered) return []
        if (entry.title === phrase) score += 60
        else if (entry.parent === phrase) score += 15
        // Where the heading is all that was asked, the rule starts at its first words
        const named = concepts.every(({ patterns }) => count(entry.title, patterns[0]!))
        const top = named ? 0 : perPassage.indexOf(Math.max(...perPassage))
        score += perPassage[top]!
        const hit: Hit = { section: entry.section, passage: entry.passages[top]!.passage, score: score * entry.weight }
        return [{ hit, covered }]
      })
      const most = Math.max(0, ...scored.map(({ covered }) => covered))
      found.hits = scored
        .filter(({ covered }) => covered === most)
        .map(({ hit }) => hit)
        .sort((a, b) => b.score - a.score)
        .slice(0, mostHits)
      found.partial = most > 0 && most < concepts.length

      found.questions = asked
        .map((candidate) => ({
          ...candidate,
          matched: concepts.filter(
            ({ said, patterns }) =>
              count(candidate.text, termPattern(singular(said))) || count(candidate.text, patterns[0]!),
          ).length,
        }))
        // Most of the words, not one shared word out of three
        .filter(({ matched }) => matched > 0 && matched / concepts.length > 0.6)
        .sort((a, b) => b.matched - a.matched || a.text.length - b.text.length)
        .slice(0, 3)
        .map(({ question, hit }) => ({ question, hit }))

      found.definitions = [...entries.values()]
        .filter(({ name }) => concepts.every(({ patterns }) => patterns.slice(0, 2).some((p) => count(name, p))))
        .sort((a, b) => a.name.length - b.name.length)
        .slice(0, 2)
        .map(({ table, entry }) => ({ table, entry }))

      found.marks = [...new Set(concepts.flatMap(({ terms }) => terms))].filter((term) => term.length > 1)
      return found
    },
  }
}

export type RuleSearch = ReturnType<typeof createSearch>
