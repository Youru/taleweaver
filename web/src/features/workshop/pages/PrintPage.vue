<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import { useBoardPieces } from '@/features/table'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useDraft, usePrintPdf } from '../api/queries'
import type { DraftAction, DraftNode } from '../api/types'
import { nameOf } from '../api/workshopApi'
import EncounterPage from '../components/EncounterPage.vue'
import TokenCard from '../components/TokenCard.vue'
import { captionOf, choiceOf, exitsOf } from '../lib/graph'
import { encounterSheet, readingOrder } from '../lib/printable'
import { useEndingLabel } from '../lib/endings'

/**
 * The scenario on paper: one encounter page, the whole booklet (cover, steps numbered §1, §2… in
 * reading order, one page per encounter), or the cards of the hidden texts. "Download the PDF" has
 * the API make the file (shown once complete); "Print" opens the browser's dialog.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const { language } = storeToRefs(usePreferences())
const endingLabel = useEndingLabel()

const scenarioId = computed(() => String(route.query.scenario ?? ''))
const draft = useDraft(scenarioId)
const pieces = useBoardPieces()
const scenario = computed(() => draft.data.value)

type What = 'booklet' | 'encounter' | 'cards'
const what = computed<What>(() => (route.query.node ? 'encounter' : route.query.what === 'cards' ? 'cards' : 'booklet'))
const veiled = ref(route.query.veiled !== '0')
watch(veiled, (v) => router.replace({ query: { ...route.query, veiled: v ? undefined : '0' } }))

const steps = computed(() => (scenario.value ? readingOrder(scenario.value) : []))
const numberOf = (id: string) => steps.value.findIndex((n) => n.id === id) + 1
const numberOfCaption = (caption: string) => {
  const found = steps.value.find((n) => captionOf(n, language.value) === caption)
  return found ? numberOf(found.id) : undefined
}
const colorName = (color: string) => t(`workshop.map.color.${color}`, color)
const sheetOf = (node: DraftNode) =>
  encounterSheet(scenario.value!, node, pieces.data.value, language.value, colorName, (name, color) =>
    t('workshop.print.tokenLabel', { name, color }),
  )

const shown = computed(() => {
  if (!scenario.value) return []
  if (what.value === 'encounter') return scenario.value.nodes.filter((n) => n.id === route.query.node)
  return steps.value
})
const encounters = computed(() => (scenario.value?.nodes ?? []).filter((n) => n.type === 'encounter'))
const options = computed(() => [
  { value: '@booklet', label: t('workshop.print.booklet') },
  { value: '@cards', label: t('workshop.print.cards') },
  ...encounters.value.map((n) => ({
    value: n.id,
    label: `${t('workshop.encounter')} · ${captionOf(n, language.value)}`,
  })),
])
const choice = computed(() =>
  what.value === 'encounter' ? String(route.query.node) : what.value === 'cards' ? '@cards' : '@booklet',
)
function choose(value: string) {
  const query = { scenario: scenarioId.value, veiled: route.query.veiled }
  void router.replace({
    query: value === '@booklet' ? query : value === '@cards' ? { ...query, what: 'cards' } : { ...query, node: value },
  })
}

// Pages whose content does not fit
const overflowing = reactive(new Set<string>())
function onOverflow(id: string, over: boolean) {
  if (over) overflowing.add(id)
  else overflowing.delete(id)
}

/** What a scene leads to, in words: "§3 · The ambush". */
const leads = (node: DraftNode) =>
  exitsOf(node).map((exit) => ({
    label: pick(exit.label, language.value),
    to: exit.target?.startsWith('@')
      ? endingLabel(exit.target)
      : exit.target
        ? `§${numberOf(exit.target)} · ${captionOf(
            scenario.value!.nodes.find((n) => n.id === exit.target)!,
            language.value,
          )}`
        : '—',
  }))
const otherMechanics = (node: DraftNode) =>
  (node.mechanics ?? []).filter((a) => a !== choiceOf(node)).map((a: DraftAction) => actionLine(a))
function actionLine(action: DraftAction): string {
  const amount = typeof action.amount === 'number' ? ` ${action.amount}` : ''
  const label = t(`workshop.events.do.${action.do}`, action.do)
  const text = pick(action.text as Record<string, string> | undefined, language.value)
  return text ? `${label} : ${text}` : `${label}${amount}`
}

/** Every token with a hidden text becomes a card. */
const cards = computed(() =>
  encounters.value.flatMap((node) =>
    sheetOf(node).hidden.flatMap((section) =>
      section.entries
        .filter((e) => e.text.length)
        .map((entry) => ({ key: `${node.id}/${entry.key}`, encounter: captionOf(node, language.value), entry })),
    ),
  ),
)
const cardPages = computed(() => {
  const pages: (typeof cards.value)[] = []
  for (let i = 0; i < cards.value.length; i += 16) pages.push(cards.value.slice(i, i + 16))
  return pages
})

const print = () => window.print()
const pdf = usePrintPdf()
const download = () =>
  pdf.mutate({
    scenario: scenarioId.value,
    node: typeof route.query.node === 'string' ? route.query.node : undefined,
    what: typeof route.query.what === 'string' ? route.query.what : undefined,
    veiled: veiled.value ? undefined : '0',
  })
const backTo = computed(() =>
  scenarioId.value.startsWith('homebrew:')
    ? { name: 'workshop-editor', params: { name: nameOf(scenarioId.value) } }
    : { name: 'workshop' },
)
</script>

<template>
  <main class="print">
    <div class="toolbar no-print">
      <RouterLink :to="backTo" class="back"
        ><i class="pi pi-arrow-left" aria-hidden="true" /> {{ t('workshop.title') }}</RouterLink
      >
      <Select
        :model-value="choice"
        :options="options"
        option-label="label"
        option-value="value"
        :aria-label="t('workshop.print.what')"
        @update:model-value="choose"
      />
      <label class="inline">
        <ToggleSwitch v-model="veiled" input-id="veiled" />
        <span>{{ t('workshop.print.veiled') }}</span>
      </label>
      <span class="spacer" />
      <Button
        :label="pdf.isPending.value ? t('workshop.print.downloading') : t('workshop.print.download')"
        icon="pi pi-download"
        :loading="pdf.isPending.value"
        :disabled="!scenario"
        @click="download"
      />
      <Button :label="t('workshop.print.print')" icon="pi pi-print" severity="secondary" @click="print" />
    </div>
    <p class="hint no-print">{{ t('workshop.print.hint') }}</p>
    <Message v-if="pdf.error.value" severity="error" closable class="no-print" @close="pdf.reset()">
      {{ t('workshop.print.downloadFailed', { message: pdf.error.value.message }) }}
    </Message>
    <Message v-if="overflowing.size" severity="warn" class="no-print">
      {{ t('workshop.print.overflow', { list: [...overflowing].join(', ') }) }}
    </Message>

    <div v-if="draft.isPending.value || pieces.isPending.value" class="center no-print">
      <ProgressSpinner :aria-label="t('common.loading')" />
    </div>
    <Message v-else-if="draft.isError.value" severity="error" class="no-print">
      {{ t('common.error', { message: draft.error.value?.message ?? '' }) }}
    </Message>

    <div v-else-if="scenario" class="sheets">
      <!-- Cards of the hidden texts -->
      <template v-if="what === 'cards'">
        <p v-if="!cards.length" class="no-print">{{ t('workshop.print.noCard') }}</p>
        <section v-for="(page, i) in cardPages" :key="i" class="card-page">
          <TokenCard
            v-for="card in page"
            :key="card.key"
            :entry="card.entry"
            :encounter="card.encounter"
            :veiled="veiled"
          />
        </section>
      </template>

      <template v-else>
        <!-- Cover -->
        <section v-if="what === 'booklet'" class="cover">
          <p class="kind">{{ scenario.kind ? t(`workshop.print.kind.${scenario.kind}`, scenario.kind) : '' }}</p>
          <h1>{{ pick(scenario.title, language) }}</h1>
          <p class="made">{{ t('workshop.print.made') }}</p>
          <ol class="contents">
            <li v-for="(node, i) in steps" :key="node.id">
              <span>§{{ i + 1 }}</span> {{ captionOf(node, language) }}
              <em>{{ node.type === 'encounter' ? t('workshop.encounter') : t('workshop.scene') }}</em>
            </li>
          </ol>
        </section>

        <template v-for="node in shown" :key="node.id">
          <EncounterPage
            v-if="node.type === 'encounter'"
            :sheet="sheetOf(node)"
            :node="node"
            :pieces="pieces.data.value"
            :veiled="veiled"
            :number="what === 'booklet' ? numberOf(node.id) : undefined"
            :page-of="numberOfCaption"
            @overflow="onOverflow"
          />
          <section v-else class="scene">
            <h2>
              <span>§{{ numberOf(node.id) }}</span> {{ captionOf(node, language) }}
            </h2>
            <p class="narrative">{{ pick(node.narrative, language) }}</p>
            <p v-for="(line, i) in otherMechanics(node)" :key="i" class="mechanic">{{ line }}</p>
            <p v-if="choiceOf(node)" class="question">{{ pick(choiceOf(node)!.question, language) }}</p>
            <ul class="leads">
              <li v-for="(lead, i) in leads(node)" :key="i">
                <strong v-if="lead.label">{{ lead.label }} → </strong>
                <template v-else>{{ t('workshop.print.then') }} </template>{{ lead.to }}
              </li>
            </ul>
          </section>
        </template>
      </template>
    </div>
  </main>
</template>

<style scoped>
.print {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 0 1rem 2rem;
}
.toolbar {
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  width: min(100%, 8.5in);
  padding: 0.5rem 0;
  background: var(--p-content-background);
}
.back {
  color: var(--p-text-muted-color);
  text-decoration: none;
  font-size: 0.9rem;
}
.inline {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.9rem;
}
.spacer {
  flex: 1;
}
.hint {
  margin: 0;
  width: min(100%, 8.5in);
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.center {
  padding: 3rem;
}
.sheets {
  display: flex;
  flex-direction: column;
  gap: 0.4in;
  align-items: center;
}
.sheets > * {
  box-shadow: 0 2px 12px rgb(0 0 0 / 0.25);
}
.cover,
.scene {
  box-sizing: border-box;
  width: 8.5in;
  padding: 0.8in 0.9in;
  background: #f3ead8;
  color: #1d1712;
  font-family: 'Optima', 'Candara', 'Segoe UI', sans-serif;
  font-size: 11pt;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}
.cover {
  min-height: 11in;
  background: linear-gradient(#6d1a1a 0 2.6in, #f3ead8 2.6in);
  break-after: page;
}
.cover h1 {
  margin: 0;
  color: #f3e9d8;
  font-family: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-variant: small-caps;
  font-size: 40pt;
  line-height: 1.05;
}
.cover .kind,
.cover .made {
  margin: 0;
  color: #f3e9d8;
  font-variant: small-caps;
  letter-spacing: 0.12em;
}
.cover .made {
  margin-top: 0.15in;
  font-size: 9pt;
  opacity: 0.8;
}
.contents {
  list-style: none;
  margin: 1.2in 0 0;
  padding: 0;
  display: grid;
  gap: 0.08in;
  font-size: 12pt;
}
.contents span,
.scene h2 span {
  display: inline-block;
  min-width: 0.45in;
  font-weight: 700;
  color: #6d1a1a;
}
.contents em {
  margin-left: 0.1in;
  font-size: 9pt;
  color: #6b5b45;
}
.scene {
  break-inside: avoid;
}
.scene h2 {
  margin: 0 0 0.12in;
  font-family: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-variant: small-caps;
  font-size: 18pt;
  border-bottom: 1.5px solid #1d1712;
}
.narrative {
  margin: 0;
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-style: italic;
  font-size: 12pt;
  line-height: 1.35;
  white-space: pre-line;
}
.mechanic,
.question {
  margin: 0.1in 0 0;
  font-weight: 700;
}
.leads {
  margin: 0.08in 0 0;
  padding-left: 1.1em;
}
.card-page {
  box-sizing: border-box;
  width: 8.5in;
  height: 11in;
  padding: 0.45in 0.4in;
  display: grid;
  grid-template-columns: repeat(4, 1.61in);
  grid-auto-rows: 2.48in;
  gap: 0.1in;
  justify-content: center;
  align-content: start;
  background: #fff;
  break-after: page;
  page: sheet;
}
</style>

<style>
@page {
  size: letter;
  margin: 0.5in 0;
}
@page sheet {
  margin: 0;
}
@media print {
  body {
    background: none !important;
  }
  header.top,
  .no-print {
    display: none !important;
  }
  .print {
    padding: 0 !important;
  }
  .print .sheets {
    gap: 0 !important;
  }
  .print .sheets > * {
    box-shadow: none !important;
  }
  .print .scene {
    padding-top: 0.2in;
    padding-bottom: 0.2in;
  }
  .print .cover {
    page: sheet;
  }
}
</style>
