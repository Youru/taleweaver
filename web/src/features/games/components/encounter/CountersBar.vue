<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Counter } from '../../api/types'
import { figureName } from '../../lib/play'

/** The named counters, read by the GM and by the table. */
defineProps<{ counters: Counter[] | undefined }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
</script>

<template>
  <ul v-if="counters?.length" class="counters" :aria-label="t('trackers.counters')">
    <li v-for="counter in counters" :key="counter.id">
      <span>
        {{ pick(counter.name, language) || counter.id }}
        <template v-if="counter.on"> · {{ figureName(counter.on) }}</template>
      </span>
      <strong>{{ counter.value }}</strong>
    </li>
  </ul>
</template>

<style scoped>
.counters {
  flex: none;
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  margin: 0;
  padding: 0;
  list-style: none;
}
li {
  display: flex;
  gap: 0.4rem;
  padding: 0.1rem 0.5rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
</style>
