<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { assetUrl } from '@/shared/api/assets'
import { pageBackground } from '../lib/pageArt'

/**
 * One scene (a story, an interlude) on US Letter sheets, laid out as the books print their stories
 * (The Cave Sickle Queen, "A Glimpse of Sentience"): red header, the text in two justified columns,
 * each paragraph indented, running onto as many sheets as it needs ("Continued ▶", then the title
 * followed by "(continued)"); where it leads closes the last column in a box.
 * As for the encounters, the text is laid out once as columns running to the right; each sheet shows
 * the next two.
 */
const props = defineProps<{
  id: string
  title: string
  narrative: string
  /** What the GM does, then the question the players answer, in words. */
  mechanics: string[]
  question?: string
  leads: { label?: string; to: string }[]
  /** Page number of its first sheet (booklet): each sheet prints its own, at the outer corner. */
  firstPage?: number
}>()

const { t } = useI18n()
const art = (path: string) => assetUrl(`assets/templates/encounter/${path}`)
const header = computed(() => ({ backgroundImage: `url('${art('top/red-header.png')}')` }))
const paragraphs = computed(() => props.narrative.split('\n').filter((p) => p.trim()))

// Two columns of 3.55 in, 0.2 in apart: a sheet shows 7.3 in, and the next one starts 7.5 in further
const PX_PER_IN = 96
const STRIDE = 7.5 * PX_PER_IN
const GAP = 0.2 * PX_PER_IN
const viewport = useTemplateRef<HTMLElement[]>('viewport')
const sheets = ref(1)
async function measure() {
  await nextTick()
  await document.fonts?.ready
  const el = viewport.value?.[0]
  if (!el) return
  sheets.value = Math.max(1, Math.ceil((el.scrollWidth + GAP - 2) / STRIDE))
}
onMounted(measure)
watch(() => [props.narrative, props.mechanics, props.question, props.leads], measure, { deep: true })

const emit = defineEmits<{ sheets: [id: string, count: number] }>()
watch(sheets, (count) => emit('sheets', props.id, count), { immediate: true })
const folio = (index: number) => (props.firstPage ? props.firstPage + index : undefined)
</script>

<template>
  <article class="scene-pages" :aria-label="t('workshop.print.pageOf', { name: title })">
    <div v-for="k in sheets" :key="k" class="sheet" :style="pageBackground(folio(k - 1))">
      <header class="head" :style="header">
        <h1>
          {{ title }} <small v-if="k > 1">{{ t('workshop.print.continuedTitle') }}</small>
        </h1>
      </header>
      <div ref="viewport" class="viewport" :aria-hidden="k > 1 ? 'true' : undefined">
        <div class="flow" :style="{ transform: `translateX(${-(k - 1) * STRIDE}px)` }">
          <p v-for="(paragraph, i) in paragraphs" :key="i" class="narrative">{{ paragraph }}</p>
          <p v-for="(line, i) in mechanics" :key="`m-${i}`" class="mechanic">{{ line }}</p>
          <p v-if="question" class="question">{{ question }}</p>
          <ul v-if="leads.length" class="leads">
            <li v-for="(lead, i) in leads" :key="i">
              <strong v-if="lead.label">{{ lead.label }} → </strong>
              <template v-else>{{ `${t('workshop.print.then')} ` }}</template>{{ lead.to }}
            </li>
          </ul>
        </div>
      </div>
      <footer class="foot">
        <span v-if="k < sheets" class="continued">{{ t('workshop.print.continued') }}</span>
        <span v-if="folio(k - 1)" :class="['folio', { odd: folio(k - 1)! % 2 }]">{{ folio(k - 1) }}</span>
      </footer>
    </div>
  </article>
</template>

<style scoped>
.scene-pages {
  --print-title-font: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  --print-body-font: 'Optima', 'Candara', 'Segoe UI', sans-serif;
  --ink: #1d1712;
  --rust: #6d1a1a;
  display: flex;
  flex-direction: column;
  gap: 0.4in;
}
@media print {
  .scene-pages {
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
.head {
  flex: none;
  height: 1.04in;
  background: var(--rust) center / cover;
  display: flex;
  align-items: flex-end;
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

/* The story: two justified columns, each paragraph indented, as the books print it */
.viewport {
  flex: none;
  width: 7.3in;
  height: 9.25in;
  margin: 0.25in 0.6in 0;
  overflow: hidden;
}
.flow {
  width: 7.3in;
  height: 100%;
  column-width: 3.55in;
  column-gap: 0.2in;
  column-fill: auto;
}
.narrative {
  margin: 0;
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-style: italic;
  font-size: 10pt;
  line-height: 1.3;
  text-align: justify;
  hyphens: auto;
  text-indent: 1.5em;
}
.mechanic,
.question {
  margin: 0.08in 0 0;
  font-weight: 700;
}
/* Where it leads: a box closing the text, as "Continue to …, Pg. 10" */
.leads {
  margin: 0.15in 0 0;
  padding: 0.06in 0.1in 0.06in 1.6em;
  border-radius: 0.08in;
  background: rgb(150 115 70 / 0.35);
  font-style: italic;
  break-inside: avoid;
}

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
