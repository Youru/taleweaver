<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { assetUrl } from '@/shared/api/assets'
import { usePreferences } from '@/shared/lib/preferences'
import type { LogEntry } from '../api/types'
import { formatLog, type FormattedLog } from '../lib/formatLog'

/**
 * The journal: every line of the game (log), or the page of the story being read (page: no heading,
 * and new text is brought into view from its start rather than from the end of the list).
 */
const props = defineProps<{ log: LogEntry[]; page?: boolean }>()

const { t, te } = useI18n()
const { language } = storeToRefs(usePreferences())
const lines = computed(() =>
  props.log.map((entry) =>
    formatLog(
      entry,
      language.value,
      (key, args) => t(key, args),
      (key) => te(key),
    ),
  ),
)

/**
 * What is drawn. The journal (log) is plain lines. The page of the story shows scenes and encounter
 * openings as pages of the book, and gathers the gains that follow each other in one box.
 */
type Block =
  | { kind: 'story'; line: FormattedLog }
  | { kind: 'line'; line: FormattedLog }
  | { kind: 'gains'; lines: FormattedLog[] }
const blocks = computed<Block[]>(() => {
  if (!props.page) return lines.value.map((line) => ({ kind: 'line', line }))
  const out: Block[] = []
  for (const line of lines.value) {
    const last = out.at(-1)
    if (line.story) out.push({ kind: 'story', line })
    else if (line.tone === 'reward' && last?.kind === 'gains') last.lines.push(line)
    else if (line.tone === 'reward') out.push({ kind: 'gains', lines: [line] })
    else out.push({ kind: 'line', line })
  }
  return out
})

/** The book's art (Encounter template): the red banner and the parchment of the printed pages. */
const art = (path: string) => `url("${assetUrl(`assets/templates/encounter/${path}`)}")`
const bookArt = { '--banner': art('top/red-header.png'), '--parchment': art('print-background-right.jpg') }
const paragraphs = (text: string) => text.split(/\n+/).filter((p) => p.trim())

// Keep the latest lines in view: the end of the journal, or the start of what was just added to the
// page (a new page starts at its top).
const list = ref<HTMLElement | null>(null)
watch(
  () => props.log,
  async (log, before) => {
    await nextTick()
    const el = list.value
    if (!el) return
    if (!props.page) return toEnd(el)
    // Same page, longer (the game is fetched again after each answer: compare the lines, not the objects)
    const grown = before && log.length > before.length && JSON.stringify(log[0]) === JSON.stringify(before[0])
    const first = grown ? (el.children[before.length] as HTMLElement | undefined) : undefined
    el.scrollTo({ top: first ? first.offsetTop : 0, behavior: first ? 'smooth' : 'auto' })
  },
  { immediate: true },
)

/** The last line in view, whichever element scrolls (the list itself, or the tabs holding it). */
function toEnd(el: HTMLElement) {
  el.scrollTo({ top: el.scrollHeight })
  ;(el.lastElementChild as HTMLElement | null)?.scrollIntoView({ block: 'nearest' })
}

// A journal in a tab that was hidden has nothing to scroll yet: once shown, its end comes into view
let observer: ResizeObserver | undefined
onMounted(() => {
  const el = list.value
  if (!el || props.page || typeof ResizeObserver === 'undefined') return
  let shown = el.clientHeight > 0
  observer = new ResizeObserver(() => {
    const now = el.clientHeight > 0
    if (now && !shown) toEnd(el)
    shown = now
  })
  observer.observe(el)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <section class="journal" :class="{ reading: page }" :aria-label="page ? t('play.story') : t('play.journal')">
    <ol ref="list" :style="bookArt">
      <template v-for="(block, index) in blocks" :key="index">
        <!-- A scene, the start of an encounter: a page of the book -->
        <li v-if="block.kind === 'story' && block.line.story" class="story">
          <header>
            <small>{{ block.line.story.kicker }}</small>
            <strong>{{ block.line.story.heading }}</strong>
            <small v-if="block.line.story.detail" class="detail">{{ block.line.story.detail }}</small>
          </header>
          <div v-if="block.line.text" class="page" :class="{ long: block.line.text.length > 280 }">
            <p v-for="(paragraph, i) in paragraphs(block.line.text)" :key="i">{{ paragraph }}</p>
          </div>
        </li>
        <!-- What the party won, together -->
        <li v-else-if="block.kind === 'gains'" class="gains">
          <ul>
            <li v-for="(gain, i) in block.lines" :key="i">
              <i class="pi pi-check-circle" aria-hidden="true" />
              <span>
                <strong>{{ gain.title }}</strong>
                <span v-if="gain.text"> {{ gain.text }}</span>
              </span>
            </li>
          </ul>
        </li>
        <li v-else-if="block.kind === 'line'" :class="block.line.tone">
          <strong v-if="block.line.title">{{ block.line.title }}</strong>
          <p v-if="block.line.text">{{ block.line.text }}</p>
        </li>
      </template>
    </ol>
  </section>
</template>

<style scoped>
.journal {
  display: flex;
  flex-direction: column;
  min-height: 0;
}
ol {
  position: relative;
  list-style: none;
  margin: 0;
  padding: 0;
  overflow-y: auto;
  flex: 1;
}
li {
  padding: 0.35rem 0.6rem;
  border-left: 3px solid var(--p-content-border-color);
  margin-bottom: 0.25rem;
}
li p {
  margin: 0.2rem 0 0;
  white-space: pre-line;
}
/* Texts read aloud: in the journal, plain lines; on the page of the story, the book's paper */
.narrative {
  border-left-color: var(--p-primary-color);
  background: var(--p-content-hover-background);
}
.narrative p {
  font-family: Georgia, serif;
}
.reading .narrative {
  border-left-color: #6d1a1a;
  background: #efe3c8 var(--parchment) center / cover;
  color: #1d1712;
}
.reading .narrative p {
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-style: italic;
  line-height: 1.4;
}

/* A scene or an encounter's introduction, as the books print a story: red banner, parchment,
   justified italic paragraphs, indented, the first one opening on a drop cap */
.story {
  --title-font: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  padding: 0;
  margin: 0.6rem 0;
  border: none;
  border-radius: 4px;
  overflow: hidden;
  box-shadow: 0 1px 6px rgb(0 0 0 / 0.3);
}
.story header {
  display: flex;
  flex-direction: column;
  padding: 0.7rem 1.4rem 0.6rem;
  background: #6d1a1a var(--banner) left center / cover;
  color: #f3e9d8;
  font-family: var(--title-font);
}
.story header small {
  font-variant: small-caps;
  letter-spacing: 0.12em;
  font-size: 0.8rem;
  opacity: 0.85;
}
.story header strong {
  font-variant: small-caps;
  font-size: 1.6rem;
  line-height: 1.1;
  letter-spacing: 0.02em;
}
.story header .detail {
  margin-top: 0.2rem;
}
.page {
  padding: 1rem 1.4rem 1.2rem;
  background: #e6d6b4 var(--parchment) center / cover;
  color: #1d1712;
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-style: italic;
  font-size: 1.05rem;
  line-height: 1.5;
}
.page p {
  margin: 0;
  max-width: 46rem;
  text-align: justify;
  hyphens: auto;
  text-indent: 1.5em;
}
.page p + p {
  margin-top: 0.35rem;
}
.page p:first-child {
  text-indent: 0;
}
.page.long p:first-child::first-letter {
  float: left;
  margin: 0.1rem 0.4rem 0 0;
  color: #6d1a1a;
  font-family: var(--title-font);
  font-style: normal;
  font-size: 3.3rem;
  line-height: 0.85;
}
.ai {
  border-left-color: var(--p-orange-500);
}
.round {
  border-left-color: var(--p-surface-400);
  color: var(--p-text-muted-color);
}
.alert {
  border-left-color: var(--p-red-500);
  color: var(--p-red-600);
}
.reward {
  border-left-color: var(--p-green-500);
}
/* On the page of the story, what was won: one box on the same paper */
.gains {
  padding: 0.5rem 0.9rem;
  border-left-color: #2f6b3a;
  background: #efe3c8 var(--parchment) center / cover;
  color: #1d1712;
}
.gains ul {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin: 0;
  padding: 0;
  list-style: none;
}
.gains li {
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
  margin: 0;
  padding: 0;
  border: none;
}
.gains i {
  color: #2f6b3a;
}
.muted {
  color: var(--p-text-muted-color);
  font-size: 0.85rem;
  border-left-color: transparent;
}
</style>
