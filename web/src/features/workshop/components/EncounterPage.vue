<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import type { DraftNode } from '../api/types'
import type { EncounterSheet } from '../lib/printable'
import { useEndingLabel } from '../lib/endings'
import PrintMap from './PrintMap.vue'

/**
 * One encounter on a US Letter page, after the official Encounter template: red header, italic
 * introduction with the north arrow, the diagram, then setup and end conditions, special rules, and
 * the hidden text of the tokens (red filter, or in the clear for the GM).
 */
const props = defineProps<{
  sheet: EncounterSheet
  node: DraftNode
  pieces: BoardPieces | undefined
  /** Hidden texts under the red noise (players' copy) or readable (GM's copy). */
  veiled: boolean
  /** Page number printed at the bottom (booklet). */
  number?: number
  /** Where each step is printed, to write "pg. X" after a reward. */
  pageOf?: (caption: string) => number | undefined
}>()
const emit = defineEmits<{ overflow: [id: string, overflowing: boolean] }>()

const { t } = useI18n()
const endingLabel = useEndingLabel()
const page = useTemplateRef<HTMLElement>('page')
const art = (path: string) => assetUrl(`assets/templates/encounter/${path}`)
const background = computed(() => ({
  backgroundImage: [art('bg-right.png'), art('bg-left.png')].map((u) => `url("${u}")`).join(', '),
}))
const veil = computed(() => ({
  backgroundImage: `linear-gradient(rgb(245 205 190 / 0.4), rgb(245 205 190 / 0.4)), url("${art('content/totem/hide-text.png')}")`,
}))
const hasMap = computed(() => ((props.node.map as { tiles?: unknown[] } | undefined)?.tiles?.length ?? 0) > 0)

/** Too much for one sheet: the page runs on a second one (the background follows). */
const long = ref(false)
async function measure() {
  await nextTick()
  const el = page.value
  const columns = el?.querySelector('.columns')
  if (!el || !columns) return
  // One Letter sheet, in CSS pixels
  const sheet = 11 * 96
  long.value = columns.getBoundingClientRect().bottom - el.getBoundingClientRect().top > sheet + 2
  emit('overflow', props.sheet.id, long.value)
}
onMounted(measure)
watch(() => [props.sheet, props.veiled], measure, { deep: true })

const nextLabel = (next: string | undefined) => {
  if (!next) return ''
  if (next.startsWith('@')) return endingLabel(next)
  const at = props.pageOf?.(next)
  return at ? t('workshop.print.continueAt', { step: next, number: at }) : t('workshop.print.continue', { step: next })
}
</script>

<template>
  <article
    ref="page"
    class="page"
    :class="{ long }"
    :style="background"
    :aria-label="t('workshop.print.pageOf', { name: sheet.name })"
  >
    <header class="head" :style="{ backgroundImage: `url('${art('top/red-header.png')}')` }">
      <h1>{{ sheet.name }}</h1>
    </header>
    <section class="intro">
      <p>{{ sheet.intro }}</p>
      <div class="north" :style="{ transform: `rotate(${sheet.northArrow}deg)` }">
        <img :src="art('top/vector-smart-object.png')" alt="" />
      </div>
      <span v-if="sheet.location" class="location">{{ t(`workshop.location.${sheet.location}`, sheet.location) }}</span>
    </section>

    <div v-if="hasMap" class="map">
      <PrintMap :node="node" :pieces="pieces" />
    </div>

    <div class="columns">
      <div class="col">
        <section v-if="sheet.setup.length" class="setup">
          <h2 class="setup-title">{{ t('workshop.print.setup') }}</h2>
          <dl>
            <template v-for="line in sheet.setup" :key="line.group">
              <dt>{{ t(`workshop.print.group.${line.group}`) }}</dt>
              <dd v-for="item in line.items" :key="item">{{ item }}</dd>
            </template>
          </dl>
        </section>
        <section v-if="sheet.ends.length">
          <h2>{{ t('workshop.print.endConditions') }}</h2>
          <div v-for="(end, i) in sheet.ends" :key="i" class="end">
            <h3>{{ end.kind === 'win' ? t('workshop.print.win') : t('workshop.print.lose') }}</h3>
            <p>{{ end.text }}</p>
            <div v-if="end.reward || end.next" class="reward">
              <strong>{{ t('workshop.print.reward') }}</strong>
              <p v-if="end.reward" :class="{ veiled }" :style="veiled ? veil : undefined">{{ end.reward }}</p>
              <p v-if="end.next" class="next">{{ nextLabel(end.next) }}</p>
            </div>
          </div>
        </section>
      </div>

      <div class="col">
        <section v-if="sheet.rules.length">
          <h2>{{ t('workshop.print.specialRules') }}</h2>
          <div v-for="(rule, i) in sheet.rules" :key="i" class="rule">
            <h3>{{ rule.name }}</h3>
            <p>{{ rule.text }}</p>
          </div>
        </section>
      </div>

      <div class="col">
        <section v-for="section in sheet.hidden" :key="section.group">
          <h2>{{ t(`workshop.print.group.${section.group}`, section.group) }}</h2>
          <div v-for="entry in section.entries" :key="entry.key" class="hidden-entry">
            <h3>{{ entry.label }}</h3>
            <p v-if="entry.activation" class="activation">{{ entry.activation }}</p>
            <div v-if="entry.text.length" :class="['secret', { veiled }]" :style="veiled ? veil : undefined">
              <p v-for="(line, i) in entry.text" :key="i">{{ line }}</p>
            </div>
          </div>
        </section>
      </div>
    </div>
    <footer v-if="number" class="folio">{{ number }}</footer>
  </article>
</template>

<style scoped>
.page {
  --print-title-font: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  --print-body-font: 'Optima', 'Candara', 'Segoe UI', sans-serif;
  --ink: #1d1712;
  position: relative;
  box-sizing: border-box;
  width: 8.5in;
  height: 11in;
  overflow: hidden;
  background-color: #d8c6a4;
  background-size: cover;
  color: var(--ink);
  font-family: var(--print-body-font);
  font-size: 9.5pt;
  line-height: 1.22;
  display: flex;
  flex-direction: column;
  break-after: page;
  page: sheet;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}
.page.long {
  height: 22in;
  background-size: 100% 11in;
  background-repeat: repeat-y;
}
.head {
  height: 1.04in;
  background: #6d1a1a center / cover;
  display: flex;
  align-items: flex-end;
  padding: 0 0.69in 0.22in;
}
h1 {
  margin: 0;
  color: #f3e9d8;
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-weight: 700;
  font-size: 34pt;
  letter-spacing: 0.02em;
  line-height: 1;
}
.intro {
  position: relative;
  min-height: 1.05in;
  padding: 0.18in 1.65in 0.15in 0.67in;
  background: rgb(120 90 50 / 0.12);
}
.intro p {
  margin: 0;
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-style: italic;
  font-size: 13pt;
  line-height: 1.3;
  white-space: pre-line;
}
.north {
  position: absolute;
  top: 0.2in;
  right: 0.62in;
  width: 0.55in;
}
.north img {
  width: 100%;
}
.location {
  position: absolute;
  right: 0.5in;
  bottom: 0.08in;
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-weight: 700;
  font-size: 9pt;
  letter-spacing: 0.1em;
}
.map {
  margin: 0.2in 0.68in 0;
  height: 2.75in;
  flex: none;
}
.columns {
  display: grid;
  grid-template-columns: 2.25in 2.3in 2.2in;
  gap: 0.2in;
  padding: 0.22in 0.68in 0.4in;
  /* Its full height, even past the sheet: that is what is measured */
  flex: none;
}
.col {
  display: flex;
  flex-direction: column;
  gap: 0.15in;
  min-width: 0;
}
h2 {
  margin: 0 0 0.06in;
  padding-bottom: 0.02in;
  border-bottom: 1.5px solid var(--ink);
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-size: 14pt;
  font-weight: 700;
}
h3 {
  margin: 0.06in 0 0;
  font-size: 9.5pt;
  font-weight: 700;
  text-transform: uppercase;
}
p {
  margin: 0;
  white-space: pre-line;
}
.setup {
  background: rgb(150 115 70 / 0.35);
  border-radius: 0.15in;
  padding: 0.12in 0.15in 0.15in;
  text-align: center;
}
.setup-title {
  border: none;
  font-size: 17pt;
}
.setup dl {
  margin: 0;
}
.setup dt {
  margin-top: 0.06in;
  font-weight: 700;
  text-transform: uppercase;
  font-size: 9.5pt;
}
.setup dd {
  margin: 0;
}
.reward {
  margin-top: 0.06in;
  padding: 0.06in 0.08in;
  border-radius: 0.08in;
  background: rgb(150 115 70 / 0.35);
}
.next {
  font-style: italic;
}
.secret {
  margin-top: 0.04in;
}
.activation {
  font-style: italic;
}
.veiled {
  background-size: 2.2in auto;
  color: rgb(70 130 175 / 0.6);
  padding: 0.04in 0.06in;
}
.folio {
  position: absolute;
  bottom: 0.12in;
  right: 0.5in;
  font-family: var(--print-title-font);
  font-size: 10pt;
}
</style>
