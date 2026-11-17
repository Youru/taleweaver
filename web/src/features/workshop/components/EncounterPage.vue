<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import type { DraftNode } from '../api/types'
import { FIRST_HALF_GROUPS, setupColumns, type EncounterSheet } from '../lib/printable'
import { useEndingLabel } from '../lib/endings'
import { extentOf, mapOf, setupOf, TILE } from '../lib/map'
import PrintMap from './PrintMap.vue'

/**
 * One encounter on US Letter sheets, laid out as the official bounty books (The Cave Sickle Queen):
 * - the setup sheet: red header with the location, italic introduction with the north arrow and the
 *   loot level, the diagram as large as the sheet allows, and the Setup box in three columns. A map
 *   wider than four tiles is cut in two at a tile edge and printed over two sheets ("Large Encounter
 *   Setup"), the setup list split between them;
 * - the "Continued" sheets: end conditions, special rules and the hidden texts of the tokens, flowing
 *   down three columns and onto as many sheets as they need.
 * The text is laid out once as columns running to the right; each Continued sheet shows the next three.
 */
const props = defineProps<{
  sheet: EncounterSheet
  node: DraftNode
  pieces: BoardPieces | undefined
  /** Hidden texts under the red noise (players' copy) or readable (GM's copy). */
  veiled: boolean
  /** Page number of its first sheet (booklet): each sheet prints its own, at the outer corner. */
  firstPage?: number
  /** The page where each step starts, to write "p. X" after a reward. */
  pageOf?: (caption: string) => number | undefined
}>()

const { t } = useI18n()
const endingLabel = useEndingLabel()
const art = (path: string) => assetUrl(`assets/templates/encounter/${path}`)
// The template's two background layers, flattened into one JPEG (scripts/flatten-print-background.mjs):
// as PNG they weighed 14 MB each in every PDF, which readers were slow or failed to open
const background = computed(() => ({ backgroundImage: `url("${art('print-background.jpg')}")` }))
const header = computed(() => ({ backgroundImage: `url('${art('top/red-header.png')}')` }))
const veil = computed(() => ({
  backgroundImage: `linear-gradient(rgb(245 205 190 / 0.4), rgb(245 205 190 / 0.4)), url("${art('content/totem/hide-text.png')}")`,
}))

/** Wider than this many squares (four tiles), the map is printed over two sheets. */
const WIDE = 4 * TILE
const extent = computed(() => extentOf({ map: mapOf(props.node), setup: setupOf(props.node) }))
const hasMap = computed(() => mapOf(props.node).tiles.length > 0)
const parts = computed(() => {
  const setup = props.sheet.setup
  const { x: left, width: right } = extent.value
  if (!hasMap.value || right - left <= WIDE) return [{ columns: undefined, setup, first: true }]
  const cut = left + Math.ceil((right - left) / 2 / TILE) * TILE
  const span = Math.max(cut - left, right - cut)
  return [
    {
      columns: { from: left, to: cut, span },
      setup: setup.filter((l) => FIRST_HALF_GROUPS.includes(l.group)),
      first: true,
    },
    {
      columns: { from: cut, to: right, span },
      setup: setup.filter((l) => !FIRST_HALF_GROUPS.includes(l.group)),
      first: false,
    },
  ]
})

// Three columns of 2.3 in, 0.2 in apart: a Continued sheet shows 7.3 in, and the next one starts 7.5 in further
const PX_PER_IN = 96
const STRIDE = 7.5 * PX_PER_IN
const GAP = 0.2 * PX_PER_IN
const viewport = useTemplateRef<HTMLElement[]>('viewport')
const textSheets = ref(1)
async function measure() {
  await nextTick()
  await document.fonts?.ready
  const el = viewport.value?.[0]
  if (!el) return
  textSheets.value = Math.max(1, Math.ceil((el.scrollWidth + GAP - 2) / STRIDE))
}
onMounted(measure)
watch(() => [props.sheet, props.veiled], measure, { deep: true })

const lootTitle = (group: string) => group === 'loot' && !!props.sheet.lootLevel
const nextLabel = (next: string | undefined) => {
  if (!next) return ''
  if (next.startsWith('@')) return endingLabel(next)
  const at = props.pageOf?.(next)
  return at ? t('workshop.print.continueAt', { step: next, number: at }) : t('workshop.print.continue', { step: next })
}
const endTitle = (end: EncounterSheet['ends'][number]) =>
  end.kind === 'lose'
    ? t('workshop.print.lose')
    : end.alternate
      ? t('workshop.print.alternateWin')
      : t('workshop.print.win')
const sheetCount = computed(() => parts.value.length + textSheets.value)
// The booklet numbers its pages from the sheets of every step
const emit = defineEmits<{ sheets: [id: string, count: number] }>()
watch(sheetCount, (count) => emit('sheets', props.sheet.id, count), { immediate: true })
/** The page number of a sheet of this encounter (0: the first one), when printed in the booklet. */
const folio = (index: number) => (props.firstPage ? props.firstPage + index : undefined)
</script>

<template>
  <article class="encounter" :aria-label="t('workshop.print.pageOf', { name: sheet.name })">
    <!-- Setup sheet(s): the diagram and the Setup box -->
    <div v-for="(part, p) in parts" :key="`setup-${p}`" class="sheet setup-sheet" :style="background">
      <header class="head" :style="header">
        <h1 v-if="part.first">{{ sheet.name }}</h1>
        <h1 v-else>{{ t('workshop.print.largeSetup') }}</h1>
        <span v-if="sheet.location" class="location">{{
          t(`workshop.location.${sheet.location}`, sheet.location)
        }}</span>
      </header>
      <section class="intro" :class="{ bare: !part.first }">
        <p v-if="part.first">{{ sheet.intro }}</p>
        <!-- A large encounter has its north arrow on its second sheet: the intro takes the whole width -->
        <div v-if="!part.first || parts.length === 1" class="compass">
          <img
            class="north"
            :src="art('top/vector-smart-object.png')"
            alt=""
            :style="{ transform: `rotate(${sheet.northArrow}deg)` }"
          />
          <span v-if="sheet.lootLevel" class="pill">{{
            t('workshop.print.lootLevel', { level: sheet.lootLevel })
          }}</span>
        </div>
      </section>

      <div v-if="hasMap" class="map">
        <PrintMap :node="node" :pieces="pieces" :columns="part.columns" />
      </div>

      <section v-if="part.setup.length" class="setup">
        <h2 class="setup-title">
          <span>{{ part.first ? t('workshop.print.setup') : t('workshop.print.setupContinued') }}</span>
        </h2>
        <div class="setup-groups">
          <div v-for="(column, c) in setupColumns(part.setup)" :key="c" class="setup-column">
            <div v-for="cell in column" :key="`${cell.group}-${cell.continued}`" class="setup-group">
              <h4>
                {{ t(`workshop.print.group.${cell.group}`) }}
                <small v-if="cell.continued">{{ t('workshop.print.groupContinued') }}</small>
              </h4>
              <p v-for="item in cell.items" :key="item">{{ item }}</p>
            </div>
          </div>
        </div>
      </section>
      <footer class="foot">
        <span class="continued">{{ t('workshop.print.continued') }}</span>
        <span v-if="folio(p)" :class="['folio', { odd: folio(p)! % 2 }]">{{ folio(p) }}</span>
      </footer>
    </div>

    <!-- Continued sheets: the same columns, three by three -->
    <div v-for="k in textSheets" :key="`text-${k}`" class="sheet text-sheet" :style="background">
      <header class="head" :style="header">
        <h1>
          {{ sheet.name }} <small>{{ t('workshop.print.continuedTitle') }}</small>
        </h1>
        <span v-if="sheet.location" class="location">{{
          t(`workshop.location.${sheet.location}`, sheet.location)
        }}</span>
      </header>
      <div ref="viewport" class="viewport" :aria-hidden="k > 1 ? 'true' : undefined">
        <div class="flow" :style="{ transform: `translateX(${-(k - 1) * STRIDE}px)` }">
          <section v-if="sheet.ends.length">
            <h2>{{ t('workshop.print.endConditions') }}</h2>
            <div v-for="(end, i) in sheet.ends" :key="i" class="end">
              <h3>{{ endTitle(end) }}</h3>
              <p v-if="end.later" class="activation">{{ t('workshop.print.later') }}</p>
              <p :class="{ veiled: veiled && end.later }" :style="veiled && end.later ? veil : undefined">
                {{ end.text }}
              </p>
              <div v-if="end.reward || end.next" class="reward">
                <div class="reward-body">
                  <strong>{{ t('workshop.print.reward') }}</strong>
                  <p v-if="end.reward" :class="{ veiled }" :style="veiled ? veil : undefined">{{ end.reward }}</p>
                </div>
                <p v-if="end.next" class="next">{{ nextLabel(end.next) }}</p>
              </div>
            </div>
          </section>
          <section v-if="sheet.rules.length">
            <h2>{{ t('workshop.print.specialRules') }}</h2>
            <div v-for="(rule, i) in sheet.rules" :key="i" class="rule">
              <h3>{{ rule.name }}</h3>
              <p>{{ rule.text }}</p>
            </div>
          </section>
          <section v-if="sheet.hiddenRules.length">
            <h2>{{ t('workshop.print.hiddenRules') }}</h2>
            <div v-for="(rule, i) in sheet.hiddenRules" :key="i" class="rule">
              <h3>{{ rule.name }}</h3>
              <p :class="{ veiled }" :style="veiled ? veil : undefined">{{ rule.text }}</p>
            </div>
          </section>
          <section v-for="section in sheet.hidden" :key="section.group">
            <h2 class="with-pill">
              <span>{{ t(`workshop.print.group.${section.group}`, section.group) }}</span>
              <span v-if="lootTitle(section.group)" class="pill small">{{
                t('workshop.print.lootLevelShort', { level: sheet.lootLevel })
              }}</span>
            </h2>
            <div v-for="entry in section.entries" :key="entry.key" class="hidden-entry">
              <h3 class="entry-title">
                <img v-if="entry.image" :src="assetUrl(entry.image)" alt="" class="token-icon" />
                <span>{{ entry.label }}</span>
              </h3>
              <p v-if="entry.activation" class="activation">{{ entry.activation }}</p>
              <!-- One box per branch, its mark (A:, PASS:…) in the clear as in the books -->
              <div
                v-for="(block, i) in entry.text"
                :key="i"
                :class="['branch', { long: (block.label?.length ?? 0) > 3 }]"
              >
                <strong v-if="block.label" class="branch-label">{{ block.label }}</strong>
                <p :class="['secret', { veiled }]" :style="veiled ? veil : undefined">{{ block.text }}</p>
              </div>
            </div>
          </section>
        </div>
      </div>
      <footer class="foot">
        <span v-if="parts.length + k < sheetCount" class="continued">{{ t('workshop.print.continued') }}</span>
        <span v-if="folio(parts.length + k - 1)" :class="['folio', { odd: folio(parts.length + k - 1)! % 2 }]">{{
          folio(parts.length + k - 1)
        }}</span>
      </footer>
    </div>
  </article>
</template>

<style scoped>
.encounter {
  --print-title-font: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  --print-body-font: 'Optima', 'Candara', 'Segoe UI', sans-serif;
  --ink: #1d1712;
  --rust: #6d1a1a;
  display: flex;
  flex-direction: column;
  gap: 0.4in;
}
@media print {
  .encounter {
    gap: 0;
  }
}
.sheet {
  position: relative;
  box-sizing: border-box;
  width: 8.5in;
  height: 11in;
  overflow: hidden;
  background-color: #d8c6a4;
  background-size: cover;
  color: var(--ink);
  font-family: var(--print-body-font);
  /* Sizes of the books: body 9 pt, intro 12 pt, title 26 pt, headings 12 pt */
  font-size: 9pt;
  line-height: 1.22;
  display: flex;
  flex-direction: column;
  break-after: page;
  page: sheet;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
  box-shadow: 0 2px 12px rgb(0 0 0 / 0.25);
}
@media print {
  .sheet {
    box-shadow: none;
  }
}

/* Red header: the name on the left, the location on the right */
.head {
  flex: none;
  height: 1.04in;
  background: var(--rust) center / cover;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.3in;
  padding: 0 0.55in 0.2in 0.6in;
}
h1 {
  margin: 0;
  color: #f3e9d8;
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-weight: 700;
  font-size: 26pt;
  letter-spacing: 0.02em;
  line-height: 1;
}
h1 small {
  font-size: 14pt;
}
.location {
  flex: none;
  padding: 0.04in 0.08in;
  border: 1.5px solid #f3e9d8;
  border-radius: 0.04in;
  color: #f3e9d8;
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-weight: 700;
  font-size: 8pt;
  letter-spacing: 0.1em;
}

/* Introduction, then the north arrow and the loot level on its right */
.intro {
  flex: none;
  display: flex;
  gap: 0.3in;
  align-items: flex-start;
  padding: 0.16in 0.55in 0.12in 0.6in;
}
.intro.bare {
  justify-content: center;
}
.intro p {
  flex: 1;
  margin: 0;
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-style: italic;
  font-size: 12pt;
  line-height: 1.28;
  white-space: pre-line;
}
.compass {
  flex: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.05in;
}
.north {
  width: 0.5in;
}
.pill {
  padding: 0.02in 0.1in;
  border-radius: 0.1in;
  background: #2a211a;
  color: #f3e9d8;
  font-size: 7pt;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  white-space: nowrap;
}
.pill.small {
  font-family: var(--print-body-font);
  font-variant: normal;
  font-size: 6.5pt;
}

/* The diagram takes whatever height is left */
.map {
  flex: 1 1 auto;
  min-height: 0;
  margin: 0.06in 0.45in 0;
}

/* Setup box: a rounded panel, the title between two rules, the groups over three columns */
.setup {
  flex: none;
  margin: 0.15in 0.6in 0.42in;
  padding: 0.06in 0.2in 0.12in;
  border-radius: 0.15in;
  background: rgb(150 115 70 / 0.35);
  text-align: center;
}
.setup-title {
  display: flex;
  align-items: center;
  gap: 0.15in;
  margin: 0 0 0.04in;
  border: none;
  font-size: 14pt;
}
.setup-title::before,
.setup-title::after {
  content: '';
  flex: 1;
  border-top: 1.5px solid var(--ink);
}
.setup-groups {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  column-gap: 0.25in;
  font-size: 9pt;
  line-height: 1.2;
}
.setup-group {
  margin-bottom: 0.06in;
}
.setup-group h4 small {
  font-size: 7pt;
}
.setup-group h4 {
  margin: 0;
  font-size: 9pt;
  font-weight: 700;
  text-transform: uppercase;
}

/* Continued sheets: three columns, the text running from one to the next */
.viewport {
  flex: none;
  width: 7.3in;
  height: 9.25in;
  margin: 0.2in 0.6in 0;
  overflow: hidden;
}
.flow {
  width: 7.3in;
  height: 100%;
  column-width: 2.3in;
  column-gap: 0.2in;
  column-fill: auto;
}
.flow section {
  margin-bottom: 0.14in;
}
h2 {
  margin: 0 0 0.05in;
  padding-bottom: 0.02in;
  border-bottom: 1.5px solid var(--ink);
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-size: 12pt;
  font-weight: 700;
  break-after: avoid;
}
h2.with-pill {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
h3 {
  margin: 0.08in 0 0.02in;
  font-size: 9pt;
  font-weight: 700;
  text-transform: uppercase;
  break-after: avoid;
}
.entry-title {
  display: flex;
  align-items: center;
  gap: 0.05in;
}
.token-icon {
  width: 0.2in;
  height: 0.2in;
  border-radius: 50%;
  object-fit: cover;
}
p {
  margin: 0;
  white-space: pre-line;
}
.end {
  break-inside: avoid;
}
.reward {
  margin-top: 0.05in;
  border-radius: 0.08in;
  background: rgb(150 115 70 / 0.35);
  overflow: hidden;
}
.reward-body {
  padding: 0.05in 0.08in;
}
.next {
  padding: 0.04in 0.08in;
  background: rgb(243 233 216 / 0.55);
  font-style: italic;
}
.branch {
  display: flex;
  align-items: flex-start;
  gap: 0.05in;
  margin-top: 0.04in;
}
.branch .secret {
  flex: 1;
  min-width: 0;
}
/* "MASTERFUL PASS:" and the like sit above their box, not beside it */
.branch.long {
  flex-direction: column;
  gap: 0.02in;
}
.branch.long .secret {
  width: 100%;
  box-sizing: border-box;
}
.branch-label {
  flex: none;
  padding-top: 0.03in;
  font-size: 8pt;
  text-transform: uppercase;
}
.activation {
  font-style: italic;
}
.veiled {
  background-size: 2.2in auto;
  color: rgb(70 130 175 / 0.6);
  padding: 0.04in 0.06in;
}

/* Bottom: "Continued ▶" on the right; the page number at the outer corner (left on even pages) */
.foot {
  position: absolute;
  left: 0.6in;
  right: 0.55in;
  bottom: 0.14in;
  display: flex;
  justify-content: space-between;
  font-family: var(--print-title-font);
  font-size: 9pt;
  font-weight: 700;
}
.folio {
  order: -1;
}
.folio.odd {
  order: 1;
  margin-left: 0.2in;
}
/* Alone on the last sheet (no "Continued"), an odd page number still sits at the right corner */
.folio.odd:only-child {
  margin-left: auto;
}
.continued {
  margin-left: auto;
  font-variant: small-caps;
  letter-spacing: 0.05em;
}
</style>
