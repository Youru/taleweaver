import { normalize } from './text'

export type Block = { index: number; page: number } & (
  | { kind: 'heading' | 'paragraph' | 'quote'; text: string }
  | { kind: 'item'; text: string; marker: string; depth: number }
  | { kind: 'table'; head: string[]; rows: string[][] }
)

/** A heading of the rulebook and what is under it, up to the next one. */
export interface Section {
  /** "07/the-move-action": kept in the address of the page. */
  id: string
  chapter: string
  chapterTitle: string
  title: string
  /** The heading above, for a sub-section. */
  parent?: string
  page: number
  lastPage: number
  blocks: Block[]
}

export interface Book {
  sections: Section[]
  chapters: { id: string; title: string; sections: Section[] }[]
}

/** The index of the book: its pages could not be extracted, the table of contents replaces it. */
const leftOut = new Set(['12'])

const pageMark = /^<!-- p\.(\d+) -->$/
const listItem = /^(\s*)([-*]|\d+\.) (.*)$/

const cells = (line: string) =>
  line
    .trim()
    .replace(/^\||\|$/g, '')
    .split('|')
    .map((cell) => cell.trim())

export function parseBook(chapters: { id: string; markdown: string }[]): Book {
  const book: Book = { sections: [], chapters: [] }
  const ids = new Map<string, number>()

  for (const { id: chapter, markdown } of chapters) {
    if (leftOut.has(chapter)) continue
    const lines = markdown.split(/\r?\n/)
    const chapterTitle = lines.find((line) => line.startsWith('# '))?.slice(2) ?? chapter
    const found: Section[] = []
    let page = 0
    let parent: string | undefined
    let current: Section | undefined
    let paragraph: string[] = []

    const open = (title: string, above?: string) => {
      const slug = `${chapter}/${normalize(title).replaceAll(' ', '-')}`
      const times = (ids.get(slug) ?? 0) + 1
      ids.set(slug, times)
      current = {
        id: times === 1 ? slug : `${slug}-${times}`,
        chapter,
        chapterTitle,
        title,
        parent: above,
        page,
        lastPage: page,
        blocks: [],
      }
      found.push(current)
    }
    const place = () => {
      if (!current) open(chapterTitle)
      const section = current!
      if (!section.blocks.length) section.page = page
      section.lastPage = page
      return { section, at: { index: section.blocks.length, page } }
    }
    const endParagraph = () => {
      if (!paragraph.length) return
      const { section, at } = place()
      section.blocks.push({ ...at, kind: 'paragraph', text: paragraph.join(' ') })
      paragraph = []
    }

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]!.trimEnd()
      const mark = pageMark.exec(line)
      const heading = /^(#{1,4}) (.+)$/.exec(line)
      const item = listItem.exec(line)
      if (!line || mark || heading || item || line.startsWith('|') || line.startsWith('>')) endParagraph()
      if (!line) continue

      if (mark) {
        page = Number(mark[1])
      } else if (heading) {
        const level = heading[1]!.length
        const title = heading[2]!.replace(/\*\*/g, '').replace(/:$/, '')
        if (level === 2) parent = title
        if (level === 2 || level === 3) open(title, level === 3 ? parent : undefined)
        if (level === 4) {
          const { section, at } = place()
          section.blocks.push({ ...at, kind: 'heading', text: title })
        }
      } else if (line.startsWith('>')) {
        const quote = [line.replace(/^>\s?/, '')]
        while (lines[i + 1]?.startsWith('>')) quote.push(lines[++i]!.replace(/^>\s?/, '').trimEnd())
        const { section, at } = place()
        section.blocks.push({ ...at, kind: 'quote', text: quote.filter(Boolean).join(' ') })
      } else if (line.startsWith('|')) {
        const table = [cells(line)]
        while (lines[i + 1]?.startsWith('|')) table.push(cells(lines[++i]!))
        const [head = [], ...rows] = table.filter((row) => !row.every((cell) => /^:?-+:?$/.test(cell)))
        const { section, at } = place()
        section.blocks.push({ ...at, kind: 'table', head, rows })
      } else if (item) {
        const { section, at } = place()
        const marker = /\d/.test(item[2]!) ? item[2]! : '•'
        section.blocks.push({ ...at, kind: 'item', marker, depth: Math.floor(item[1]!.length / 2), text: item[3]! })
      } else {
        paragraph.push(line.trim())
      }
    }
    endParagraph()

    // A heading that only holds others ("The Attack Action") lives on as their parent
    const sections = found.filter((section) => section.blocks.length)
    if (!sections.length) continue
    book.chapters.push({ id: chapter, title: chapterTitle, sections })
    book.sections.push(...sections)
  }
  return book
}

/** A line of text of a block: a paragraph, an item, or one row of a table. */
export interface Passage {
  block: number
  row?: number
  page: number
  raw: string
  /** What names it: the bold words it starts with, its first cell, or the heading it is. */
  label: string
}

export function passagesOf(section: Section): Passage[] {
  return section.blocks.flatMap((block): Passage[] => {
    const at = { block: block.index, page: block.page }
    if (block.kind === 'table')
      return block.rows.map((cells, row) => ({ ...at, row, raw: cells.join(' : '), label: cells[0] ?? '' }))
    const label = block.kind === 'heading' ? block.text : /^[^*]{0,25}\*\*([^*]{2,70})\*\*/.exec(block.text)?.[1]
    return [{ ...at, raw: block.text, label: label ?? '' }]
  })
}

/** Where "**Being Defeated** Pg. 69" leads: that heading if it is around that page, else the top of the page. */
export function findReference(book: Book, page: number, name?: string): Section | undefined {
  const wanted = normalize(name ?? '')
  const named = wanted
    ? book.sections
        .filter((section) => normalize(section.title) === wanted || normalize(section.parent ?? '') === wanted)
        .find((section) => section.page - 2 <= page && page <= section.lastPage + 2)
    : undefined
  return (
    named ??
    book.sections.find((section) => section.page === page) ??
    book.sections.find((section) => section.page <= page && page <= section.lastPage) ??
    // A page of pictures has no text: the rule it illustrates is the one before
    [...book.sections].reverse().find((section) => section.lastPage < page)
  )
}
