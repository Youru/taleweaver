<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { assetUrl } from '@/shared/api/assets'
import { usePreferences } from '@/shared/lib/preferences'
import type { LogEntry } from '../api/types'
import { formatLog } from '../lib/formatLog'

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
    if (!props.page) return el.scrollTo({ top: el.scrollHeight })
    // Same page, longer (the game is fetched again after each answer: compare the lines, not the objects)
    const grown = before && log.length > before.length && JSON.stringify(log[0]) === JSON.stringify(before[0])
    const first = grown ? (el.children[before.length] as HTMLElement | undefined) : undefined
    el.scrollTo({ top: first ? first.offsetTop : 0, behavior: first ? 'smooth' : 'auto' })
  },
  { immediate: true },
)

// A journal in a tab that was hidden has nothing to scroll yet: once shown, its end comes into view
let observer: ResizeObserver | undefined
onMounted(() => {
  const el = list.value
  if (!el || props.page || typeof ResizeObserver === 'undefined') return
  let shown = el.clientHeight > 0
  observer = new ResizeObserver(() => {
    const now = el.clientHeight > 0
    if (now && !shown) el.scrollTo({ top: el.scrollHeight })
    shown = now
  })
  observer.observe(el)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <section class="journal" :class="{ reading: page }" :aria-label="page ? t('play.story') : t('play.journal')">
    <ol ref="list" :style="bookArt">
      <template v-for="(line, index) in lines" :key="index">
        <!-- A scene, the start of an encounter: a page of the book -->
        <li v-if="line.story" class="story">
          <header>
            <small>{{ line.story.kicker }}</small>
            <strong>{{ line.story.heading }}</strong>
            <small v-if="line.story.detail" class="detail">{{ line.story.detail }}</small>
          </header>
          <div v-if="line.text" class="page" :class="{ long: line.text.length > 280 }">
            <p v-for="(paragraph, i) in paragraphs(line.text)" :key="i">{{ paragraph }}</p>
          </div>
        </li>
        <li v-else :class="line.tone">
          <strong v-if="line.title">{{ line.title }}</strong>
          <p v-if="line.text">{{ line.text }}</p>
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
/* Texts read aloud (hidden texts, rules, notes): on the book's paper, in dark ink in both themes */
.narrative {
  border-left-color: #6d1a1a;
  background: #efe3c8 var(--parchment) center / cover;
  color: #1d1712;
}
.narrative p {
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
/* On the page of the story, what was won is read with it, on the same paper */
.reading .reward {
  border-left-color: #2f6b3a;
  background: #efe3c8 var(--parchment) center / cover;
  color: #1d1712;
}
.muted {
  color: var(--p-text-muted-color);
  font-size: 0.85rem;
  border-left-color: transparent;
}
</style>
