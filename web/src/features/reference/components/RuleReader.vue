<script setup lang="ts">
import { computed, nextTick, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import { findReference, type Book, type Section } from '../lib/book'
import type { Piece } from '../lib/inline'
import RuleText from './RuleText.vue'

/** A rule as the book gives it, opened on the passage that answers. */
const props = defineProps<{
  book: Book
  section: Section
  passage?: { block: number; row?: number }
  marks: string[]
  canGoBack: boolean
  /** Narrow screen: the rule was behind the list until now. */
  shown: boolean
}>()
const emit = defineEmits<{ open: [section: Section]; back: [] }>()
const { t } = useI18n()

const scroller = useTemplateRef<HTMLElement>('scroller')
const position = computed(() => props.book.sections.indexOf(props.section))
const previous = computed(() => props.book.sections[position.value - 1])
const next = computed(() => props.book.sections[position.value + 1])
const pages = computed(() =>
  props.section.page === props.section.lastPage
    ? String(props.section.page)
    : `${props.section.page}-${props.section.lastPage}`,
)

function follow(link: NonNullable<Piece['link']>) {
  const found = findReference(props.book, link.page, link.name)
  if (found) emit('open', found)
}

watch(
  () => [props.section, props.passage?.block, props.passage?.row, props.shown],
  async () => {
    await nextTick()
    const box = scroller.value
    if (!box) return
    box.scrollTop = 0
    const answer = box.querySelector<HTMLElement>('.answer')
    if (!answer) return
    const top = answer.getBoundingClientRect().top - box.getBoundingClientRect().top
    // Already in sight: the top of the rule gives the context
    if (top + answer.offsetHeight > box.clientHeight) box.scrollTop = top - box.clientHeight / 4
  },
  { immediate: true },
)
</script>

<template>
  <article ref="scroller" class="reader" :aria-label="section.title">
    <header>
      <Button
        class="back"
        :class="{ needless: !canGoBack }"
        icon="pi pi-arrow-left"
        :label="t('reference.back')"
        text
        size="small"
        @click="emit('back')"
      />
      <p class="path">
        {{ section.chapterTitle }}<template v-if="section.parent"> › {{ section.parent }}</template>
      </p>
      <h2>{{ section.title }}</h2>
      <p class="page">{{ t('reference.page', { page: pages }) }}</p>
    </header>

    <div class="text" lang="en">
      <template v-for="block in section.blocks" :key="block.index">
        <h3 v-if="block.kind === 'heading'" :class="{ answer: passage?.block === block.index }">{{ block.text }}</h3>
        <div v-else-if="block.kind === 'table'" class="table">
          <table>
            <thead>
              <tr>
                <th v-for="(cell, index) in block.head" :key="index"><RuleText :text="cell" /></th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(row, index) in block.rows"
                :key="index"
                :class="{ answer: passage?.block === block.index && passage?.row === index }"
              >
                <td v-for="(cell, column) in row" :key="column">
                  <RuleText :text="cell" :marks="marks" @follow="follow" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p
          v-else
          :class="[
            block.kind,
            {
              answer: passage?.block === block.index,
              important: block.kind === 'paragraph' && /^(\*\*)?(IMPORTANT|NOTE|TIP)/.test(block.text),
            },
          ]"
          :style="block.kind === 'item' ? { marginLeft: `${block.depth * 1.5}rem` } : undefined"
        >
          <span v-if="block.kind === 'item'" class="marker">{{ block.marker }}</span>
          <RuleText :text="block.text" :marks="marks" @follow="follow" />
        </p>
      </template>
    </div>

    <nav class="around" :aria-label="t('reference.around')">
      <button v-if="previous" type="button" @click="emit('open', previous)">← {{ previous.title }}</button>
      <button v-if="next" type="button" class="next" @click="emit('open', next)">{{ next.title }} →</button>
    </nav>
  </article>
</template>

<style scoped>
.reader {
  height: 100%;
  box-sizing: border-box;
  overflow-y: auto;
  padding: 0.75rem 1.25rem 1rem;
  color: var(--mid-ink);
  background: #f6ecda;
}
.needless {
  display: none;
}
.path,
.page {
  margin: 0;
  font-size: 0.8rem;
  opacity: 0.75;
}
h2 {
  margin: 0.15rem 0;
  font-size: 1.45rem;
  line-height: 1.2;
}
.text {
  max-width: 44rem;
  font: 1.05rem/1.5 var(--mid-book-font);
}
.text p {
  margin: 0.65rem 0;
}
.text h3 {
  margin: 1.1rem 0 0.3rem;
  font-size: 1.05rem;
}
.text p.item {
  display: flex;
  gap: 0.5rem;
}
.marker {
  flex: none;
  min-width: 1.1rem;
}
.quote {
  padding: 0.4rem 0.7rem;
  border-left: 3px solid rgb(109 26 26 / 0.3);
  font-style: italic;
}
.important {
  padding: 0.4rem 0.7rem;
  background: rgb(109 26 26 / 0.08);
}
.answer {
  border-radius: 3px;
  background: rgb(232 184 66 / 0.2);
  box-shadow:
    -0.4rem 0 0 rgb(232 184 66 / 0.2),
    -0.65rem 0 0 #c08a1e;
}
.table {
  margin: 0.8rem 0;
  overflow-x: auto;
}
table {
  border-collapse: collapse;
  font-size: 0.92rem;
}
th,
td {
  padding: 0.35rem 0.5rem;
  border-bottom: 1px solid rgb(60 35 15 / 0.2);
  text-align: left;
  vertical-align: top;
}
th {
  color: var(--mid-cream);
  background: var(--mid-crimson);
}
.around {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  max-width: 44rem;
  margin-top: 1.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid rgb(60 35 15 / 0.2);
}
.around button {
  padding: 0.5rem 0;
  border: 0;
  background: none;
  color: var(--mid-crimson);
  font: inherit;
  cursor: pointer;
}
.next {
  margin-left: auto;
  text-align: right;
}
@container rules (max-width: 46rem) {
  /* A table that does not fit reads as a list */
  thead {
    display: none;
  }
  tr,
  td {
    display: block;
  }
  tr {
    padding: 0.3rem 0;
    border-bottom: 1px solid rgb(60 35 15 / 0.2);
  }
  td {
    padding: 0.1rem 0.3rem;
    border: 0;
  }
  .back,
  .back.needless {
    display: inline-flex;
  }
}
</style>
