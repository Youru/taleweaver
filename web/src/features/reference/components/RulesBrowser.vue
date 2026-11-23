<script setup lang="ts">
import { computed, nextTick, onMounted, ref, shallowRef, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useReference, useRulebook } from '../api/queries'
import { parseLocalizedBook, type Passage, type Section } from '../lib/book'
import { themes } from '../lib/questions'
import { tables } from '../lib/reference'
import { createSearch, type Hit } from '../lib/search'
import RuleReader from './RuleReader.vue'
import RuleText from './RuleText.vue'

/**
 * The rules, to answer a question of the table (rules-spec): type, the answer shows. Without typing: the questions
 * often asked, the keywords, the contents. The page and the panel in play both show this.
 */
const typed = defineModel<string>('typed', { default: '' })
const sectionId = defineModel<string>('section', { default: '' })
/** On a player's phone: the book without its examples, the secret names masked (rules-spec §4). */
defineProps<{ player?: boolean }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const rulebook = useRulebook()
const reference = useReference()

const book = computed(() => parseLocalizedBook(rulebook.data.value?.chapters ?? [], language.value))
/** The book has a translation in the chosen language (else it reads in English). */
const translated = computed(() => !!rulebook.data.value?.chapters.some((c) => c.translations?.[language.value]))
const engine = computed(() =>
  rulebook.data.value ? createSearch(book.value, rulebook.data.value.glossary, reference.data.value) : undefined,
)
const asking = computed(() => typed.value.trim().length > 1)
const found = computed(() => (asking.value ? engine.value?.search(typed.value) : undefined))

interface Choice {
  key: string
  section: Section
  passage?: Pick<Passage, 'block' | 'row'>
}
/** What the arrows go through: the questions, then the rules. */
const choices = computed(() => {
  const result = found.value
  if (!result) return []
  const choice = (key: string, hit: Hit): Choice => ({ key, section: hit.section, passage: hit.passage })
  return [
    ...result.questions.map(({ question, hit }) => choice(`question:${question.text.en}`, hit)),
    ...result.hits.map((hit) => choice(`rule:${hit.section.id}`, hit)),
  ]
})

const tab = ref<'questions' | 'keywords' | 'contents'>('questions')
const tabs = ['questions', 'keywords', 'contents'] as const
const chosen = ref('')
const opened = shallowRef<Choice>()
const trail = shallowRef<Choice[]>([])
// On a narrow screen, the list or the rule
const reading = ref(false)
const input = useTemplateRef<{ $el: HTMLInputElement }>('input')
const list = useTemplateRef<HTMLElement>('list')

function show(choice: Choice) {
  opened.value = choice
  chosen.value = choice.key
  sectionId.value = choice.section.id
}
function choose(choice: Choice) {
  trail.value = []
  show(choice)
  reading.value = true
}
function openSection(section: Section) {
  choose({ key: `rule:${section.id}`, section })
}
function follow(section: Section) {
  if (opened.value) trail.value = [...trail.value, opened.value]
  show({ key: `rule:${section.id}`, section })
}
function back() {
  const last = trail.value.at(-1)
  if (!last) {
    reading.value = false
    return
  }
  trail.value = trail.value.slice(0, -1)
  show(last)
}

// Typing is enough: the best answer opens by itself
watch(typed, () => {
  reading.value = false
  trail.value = []
  const first = choices.value[0]
  if (first) show(first)
  else {
    opened.value = undefined
    chosen.value = ''
    sectionId.value = ''
  }
  if (list.value) list.value.scrollTop = 0
})
// Arrived by an address: that rule, else the best answer to its words
watch(
  engine,
  (ready) => {
    if (!ready || opened.value) return
    const section = book.value.sections.find(({ id }) => id === sectionId.value)
    if (section) {
      choose(choices.value.find((choice) => choice.section.id === section.id) ?? { key: `rule:${section.id}`, section })
    } else if (choices.value[0]) show(choices.value[0])
  },
  { immediate: true },
)

async function move(step: number) {
  if (!choices.value.length) return
  const at = choices.value.findIndex(({ key }) => key === chosen.value)
  const next = choices.value[Math.max(0, Math.min(choices.value.length - 1, at + step))]!
  trail.value = []
  show(next)
  await nextTick()
  list.value?.querySelector('[aria-current="true"]')?.scrollIntoView({ block: 'nearest' })
}
function read() {
  const current = choices.value.find(({ key }) => key === chosen.value) ?? choices.value[0]
  if (current) choose(current)
}
function focus() {
  input.value?.$el.focus()
  input.value?.$el.select()
}
onMounted(focus)
defineExpose({ focus })
</script>

<template>
  <div class="rules">
    <div class="search" role="search">
      <InputText
        ref="input"
        v-model="typed"
        type="search"
        autocomplete="off"
        :placeholder="t('reference.search')"
        :aria-label="t('reference.search')"
        fluid
        @keydown.down.prevent="move(1)"
        @keydown.up.prevent="move(-1)"
        @keydown.enter.prevent="read"
      />
      <Button
        v-if="typed"
        icon="pi pi-times"
        text
        :aria-label="t('reference.clear')"
        @click="((typed = ''), focus())"
      />
    </div>

    <Message v-if="rulebook.isError.value" severity="error">
      {{ t(player ? 'reference.failedPlayer' : 'reference.failed') }}
    </Message>
    <p v-else-if="!rulebook.data.value" role="status">{{ t('reference.loading') }}</p>
    <Message v-else-if="!book.sections.length" severity="warn">{{ t('reference.noBook') }}</Message>

    <div v-else class="panes" :class="{ reading }">
      <div ref="list" class="list">
        <template v-if="found">
          <p v-for="fix in found.corrections" :key="fix.typed" class="note">
            {{ t('reference.readAs', fix) }}
          </p>
          <p v-if="found.partial" class="note">{{ t('reference.partial') }}</p>
          <p v-if="!choices.length && !found.definitions.length" role="status">{{ t('reference.none') }}</p>

          <section
            v-for="{ table, entry } in found.definitions"
            :key="`${table}:${entry.id}`"
            class="definition"
            data-testid="rule-definition"
          >
            <small>{{ t(`reference.tables.${table}`) }}</small>
            <strong>{{ pick(entry.name, language) }}</strong>
            <RuleText :text="pick(entry.text, language)" plain />
          </section>

          <template v-if="found.questions.length">
            <h3>{{ t('reference.questions') }}</h3>
            <button
              v-for="{ question, hit } in found.questions"
              :key="question.text.en"
              type="button"
              class="row question"
              :aria-current="chosen === `question:${question.text.en}`"
              @click="choose({ key: `question:${question.text.en}`, section: hit.section, passage: hit.passage })"
            >
              {{ pick(question.text, language) }}
            </button>
          </template>

          <h3 v-if="found.hits.length">{{ t('reference.results') }}</h3>
          <button
            v-for="hit in found.hits"
            :key="hit.section.id"
            type="button"
            class="row"
            data-testid="rule-result"
            :aria-current="chosen === `rule:${hit.section.id}`"
            @click="choose({ key: `rule:${hit.section.id}`, section: hit.section, passage: hit.passage })"
          >
            <span class="top">
              <strong>{{ hit.section.title }}</strong>
              <small>{{ t('reference.page', { page: hit.passage.page }) }}</small>
            </span>
            <small v-if="hit.section.parent">{{ hit.section.parent }}</small>
            <RuleText class="excerpt" lang="en" :text="hit.passage.raw" :marks="found.marks" plain />
          </button>
        </template>

        <template v-else>
          <div class="tabs">
            <button v-for="name in tabs" :key="name" type="button" :aria-pressed="tab === name" @click="tab = name">
              {{ t(`reference.tabs.${name}`) }}
            </button>
          </div>

          <template v-if="tab === 'questions'">
            <section v-for="theme in themes" :key="theme">
              <h3>{{ t(`reference.themes.${theme}`) }}</h3>
              <button
                v-for="{ question, hit } in engine?.questions.filter((asked) => asked.question.theme === theme)"
                :key="question.text.en"
                type="button"
                class="row question"
                :aria-current="chosen === `question:${question.text.en}`"
                @click="choose({ key: `question:${question.text.en}`, section: hit.section, passage: hit.passage })"
              >
                {{ pick(question.text, language) }}
              </button>
            </section>
          </template>

          <template v-else-if="tab === 'keywords'">
            <Message v-if="reference.isError.value" severity="error">{{ t('reference.failed') }}</Message>
            <section v-for="table in tables" :key="table">
              <h3>{{ t(`reference.tables.${table}`) }}</h3>
              <div class="keywords">
                <button
                  v-for="(entry, index) in reference.data.value?.[table]"
                  :key="index"
                  type="button"
                  @click="
                    typed = pick(entry.name, language)
                      .replace(/\([^)]*\)|[+:]/g, '')
                      .trim()
                  "
                >
                  {{ pick(entry.name, language) }}
                </button>
              </div>
            </section>
          </template>

          <template v-else>
            <details v-for="chapter in book.chapters" :key="chapter.id">
              <summary>{{ chapter.title }}</summary>
              <button
                v-for="section in chapter.sections"
                :key="section.id"
                type="button"
                class="row top"
                :class="{ sub: section.parent }"
                :aria-current="opened?.section.id === section.id"
                @click="openSection(section)"
              >
                <span>{{ section.title }}</span><small>{{ section.page }}</small>
              </button>
            </details>
          </template>
        </template>
      </div>

      <div class="detail">
        <RuleReader
          v-if="opened"
          :book="book"
          :section="opened.section"
          :passage="opened.passage"
          :marks="found?.marks ?? []"
          :shown="reading"
          :can-go-back="trail.length > 0"
          @open="follow"
          @back="back"
        />
        <p v-else class="hint">
          {{ t('reference.hint') }}
          <template v-if="language !== 'en' && !translated">{{ t('reference.hintEnglish') }}</template>
        </p>
      </div>
    </div>
    <small v-if="player" class="filtered">{{ t('reference.filtered') }}</small>
  </div>
</template>

<style scoped>
.rules {
  container: rules / inline-size;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  height: 100%;
  min-height: 0;
}
.search {
  display: flex;
  gap: 0.25rem;
}
.search input {
  font-size: 1.15rem;
}
.search input::-webkit-search-cancel-button {
  display: none;
}
.panes {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(16rem, 2fr) 3fr;
  grid-template-rows: minmax(0, 1fr);
  border: 1px solid rgb(60 35 15 / 0.25);
  border-radius: 6px;
  overflow: hidden;
}
.list {
  min-height: 0;
  overflow-y: auto;
  padding: 0.5rem;
  color: var(--mid-ink);
  background: #eadcc0;
}
.detail {
  min-height: 0;
  background: #f6ecda;
}
.list h3 {
  margin: 0.8rem 0 0.2rem;
  font-size: 0.9rem;
}
.list button {
  font: inherit;
  color: inherit;
  cursor: pointer;
}
.row {
  display: block;
  box-sizing: border-box;
  width: 100%;
  padding: 0.45rem 0.5rem;
  border: 0;
  border-left: 3px solid transparent;
  border-bottom: 1px solid rgb(60 35 15 / 0.12);
  background: none;
  text-align: left;
}
.row:hover {
  background: rgb(255 255 255 / 0.3);
}
.row[aria-current='true'] {
  border-left-color: var(--mid-crimson);
  background: #f6ecda;
}
.row > small {
  display: block;
  opacity: 0.7;
}
.top {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
}
.top small {
  flex: none;
}
.sub {
  padding-left: 1.4rem;
}
.excerpt {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 3;
  line-clamp: 3;
  font: 0.9rem/1.35 var(--mid-book-font);
}
.definition {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  margin-bottom: 0.5rem;
  padding: 0.5rem 0.6rem;
  border: 1px solid rgb(109 26 26 / 0.4);
  border-radius: 4px;
  background: #f6ecda;
}
.definition small {
  color: var(--mid-crimson);
  text-transform: uppercase;
}
.note {
  margin: 0.2rem 0.4rem;
  font-size: 0.85rem;
  font-style: italic;
}
.tabs,
.keywords {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}
.tabs button,
.keywords button {
  padding: 0.35rem 0.6rem;
  border: 1px solid rgb(109 26 26 / 0.35);
  border-radius: 4px;
  background: none;
}
.keywords button {
  font-size: 0.85rem;
}
.tabs button[aria-pressed='true'] {
  color: var(--mid-cream);
  background: var(--mid-crimson);
}
summary {
  padding: 0.6rem 0.3rem;
  color: var(--mid-crimson);
  font-weight: 600;
  cursor: pointer;
}
.hint {
  margin: 0;
  padding: 3rem 1.5rem;
  color: var(--mid-ink);
  text-align: center;
}
@container rules (max-width: 46rem) {
  .panes {
    grid-template-columns: minmax(0, 1fr);
  }
  .panes:not(.reading) > .detail,
  .panes.reading > .list {
    display: none;
  }
}
.filtered {
  font-size: 0.75rem;
  opacity: 0.75;
}
</style>
