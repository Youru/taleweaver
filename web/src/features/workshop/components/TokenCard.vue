<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { assetUrl } from '@/shared/api/assets'
import type { HiddenEntry } from '../lib/printable'

/** A mini card (41 × 63 mm) with the hidden text of a token, handed to the players when it is activated. */
const props = defineProps<{ entry: HiddenEntry; encounter: string; veiled: boolean }>()

const { t } = useI18n()
const veil = computed(() =>
  props.veiled
    ? {
        backgroundImage: `linear-gradient(rgb(245 205 190 / 0.4), rgb(245 205 190 / 0.4)), url("${assetUrl('assets/templates/encounter/content/totem/hide-text.png')}")`,
      }
    : {},
)
</script>

<template>
  <article class="card" :aria-label="t('workshop.print.cardOf', { label: entry.label })">
    <header>
      <h3>{{ entry.label }}</h3>
      <small>{{ encounter }}</small>
    </header>
    <div :class="['text', { veiled }]" :style="veil">
      <p v-for="(block, i) in entry.text" :key="i">
        <strong v-if="block.label">{{ block.label }} </strong>{{ block.text }}
      </p>
    </div>
  </article>
</template>

<style scoped>
.card {
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  gap: 0.05in;
  padding: 0.1in;
  overflow: hidden;
  border: 1.5px solid #6d1a1a;
  border-radius: 0.1in;
  background: #f3ead8;
  color: #1d1712;
  font-family: 'Optima', 'Candara', 'Segoe UI', sans-serif;
  font-size: 7.5pt;
  line-height: 1.2;
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}
header {
  text-align: center;
  border-bottom: 1px solid #6d1a1a;
  padding-bottom: 0.03in;
}
h3 {
  margin: 0;
  font-family: 'Cinzel', 'Trajan Pro', 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
  font-variant: small-caps;
  font-size: 10pt;
}
small {
  font-size: 6.5pt;
  color: #6b5b45;
}
.text {
  flex: 1;
  padding: 0.03in;
}
.text p {
  margin: 0 0 0.04in;
  white-space: pre-line;
}
.veiled {
  background-size: 1.6in auto;
  color: rgb(70 130 175 / 0.6);
}
</style>
