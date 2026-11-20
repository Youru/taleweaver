<script setup lang="ts">
import { computed, inject } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BoardPieces } from '@/features/table'
import type { DraftNode } from '../api/types'
import type { DraftMap, DraftSetup } from '../lib/map'
import { setupColumns, type SetupLine } from '../lib/printable'
import { pageBackground, templateArt } from '../lib/pageArt'
import PrintMap from './PrintMap.vue'
import { OUTSIDE_RANGE } from '../lib/pageRange'

/**
 * One diagram of an encounter on a sheet of its own, as the books print them after the encounters
 * (The Cave Sickle Queen, pp. 26-41): green header "Diagram 3", the north arrow, the board with what
 * is already on the table faded and what the diagram lays bright, its Setup box (only its own pieces)
 * and its special encounter rules.
 */
const props = defineProps<{
  /** "Diagram 3". */
  title: string
  node: DraftNode
  board: { map: DraftMap; setup: DraftSetup }
  /** The tiles and terrain of the diagrams it builds on. */
  under: DraftMap[]
  setup: SetupLine[]
  rules: { name: string; text: string }[]
  pieces: BoardPieces | undefined
  northArrow: number
  /** Its page number in the booklet. */
  folio?: number
}>()

const { t } = useI18n()
const outside = inject(OUTSIDE_RANGE, () => false)
const header = computed(() => ({ backgroundImage: `url('${templateArt('top/red-header.png')}')` }))
// A narrow box when the diagram lays few kinds of pieces, as in the books
const columns = computed(() => setupColumns(props.setup, Math.min(3, Math.max(1, props.setup.length))))
const bullets = (text: string) => text.replace(/\s+◆/g, '\n◆')
</script>

<template>
  <article class="diagram" :aria-label="t('workshop.print.pageOf', { name: title })">
    <div class="sheet" :class="{ outside: outside(folio) }" :style="pageBackground(folio)">
      <header class="head" :style="header">
        <h1>{{ title }}</h1>
      </header>
      <div class="compass">
        <img
          class="north"
          :src="templateArt('top/vector-smart-object.png')"
          alt=""
          :style="{ transform: `rotate(${northArrow}deg)` }"
        />
      </div>
      <div class="map">
        <PrintMap :node="node" :pieces="pieces" :diagram="board" :under="under" :max-square="0.55" />
      </div>
      <div v-if="setup.length || rules.length" class="bottom">
        <section v-if="setup.length" class="setup">
          <h2 class="setup-title">
            <span>{{ t('workshop.print.setup') }}</span>
          </h2>
          <div class="setup-groups" :style="{ gridTemplateColumns: `repeat(${columns.length}, auto)` }">
            <div v-for="(column, c) in columns" :key="c">
              <div v-for="cell in column" :key="`${cell.group}-${cell.continued}`" class="setup-group">
                <h4>{{ t(`workshop.print.group.${cell.group}`) }}</h4>
                <p v-for="item in cell.items" :key="item">{{ item }}</p>
              </div>
            </div>
          </div>
        </section>
        <section v-if="rules.length" class="rules">
          <h2>{{ t('workshop.print.specialRules') }}</h2>
          <div v-for="(rule, i) in rules" :key="i" class="rule">
            <h3>{{ rule.name }}</h3>
            <!-- Each ◆ point on a line of its own, as the books print them -->
            <p>{{ bullets(rule.text) }}</p>
          </div>
        </section>
      </div>
      <footer class="foot">
        <span v-if="folio" :class="['folio', { odd: folio % 2 }]">{{ folio }}</span>
      </footer>
    </div>
  </article>
</template>

<style scoped>
.diagram {
  --print-title-font: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  --print-body-font: 'Optima', 'Candara', 'Segoe UI', sans-serif;
  --ink: #1d1712;
  --moss: #557a22;
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

/* Green header, the title centred: the red header's texture over moss green */
.head {
  flex: none;
  height: 1.04in;
  background-color: var(--moss);
  background-position: center;
  background-size: cover;
  background-blend-mode: luminosity;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding-bottom: 0.2in;
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
.compass {
  flex: none;
  display: flex;
  justify-content: center;
  padding: 0.14in 0 0.06in;
}
.north {
  width: 0.5in;
}
.map {
  flex: 1 1 auto;
  min-height: 0;
  margin: 0.06in 0.45in 0;
}

/* The Setup box and the special rules side by side under the board */
.bottom {
  flex: none;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  gap: 0.25in;
  margin: 0.15in 0.6in 0.42in;
}
.setup,
.rules {
  padding: 0.06in 0.2in 0.12in;
  border-radius: 0.15in;
  background: rgb(150 115 70 / 0.35);
}
.setup {
  flex: none;
  text-align: center;
}
.rules {
  flex: 1;
  background: rgb(243 233 216 / 0.7);
}
.setup-title {
  display: flex;
  align-items: center;
  gap: 0.15in;
  margin: 0 0 0.04in;
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-size: 14pt;
}
.setup-title::before,
.setup-title::after {
  content: '';
  flex: 1;
  min-width: 0.3in;
  border-top: 1.5px solid var(--ink);
}
.setup-groups {
  display: grid;
  justify-content: center;
  column-gap: 0.25in;
  font-size: 9pt;
  line-height: 1.2;
}
.setup-group {
  margin-bottom: 0.06in;
}
.setup-group h4 {
  margin: 0;
  font-size: 9pt;
  font-weight: 700;
  text-transform: uppercase;
}
.setup-group p {
  margin: 0;
}
.rules h2 {
  margin: 0.04in 0 0.06in;
  border-bottom: 1.5px solid var(--ink);
  font-family: var(--print-title-font);
  font-variant: small-caps;
  font-size: 12pt;
}
.rule h3 {
  margin: 0.04in 0 0.02in;
  font-size: 9pt;
  text-transform: uppercase;
}
.rule p {
  margin: 0;
  white-space: pre-line;
  text-align: justify;
}

.foot {
  position: absolute;
  left: 0.6in;
  right: 0.55in;
  bottom: 0.14in;
  display: flex;
  font-family: var(--print-title-font);
  font-size: 9pt;
  font-weight: 700;
}
.folio.odd {
  margin-left: auto;
}
</style>
