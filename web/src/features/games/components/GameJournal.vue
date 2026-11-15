<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { usePreferences } from '@/shared/lib/preferences'
import type { LogEntry } from '../api/types'
import { formatLog } from '../lib/formatLog'

const props = defineProps<{ log: LogEntry[] }>()

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

// Keep the latest lines in view.
const list = ref<HTMLElement | null>(null)
watch(
  () => props.log.length,
  async () => {
    await nextTick()
    list.value?.scrollTo({ top: list.value.scrollHeight })
  },
  { immediate: true },
)
</script>

<template>
  <section class="journal" :aria-label="t('play.journal')">
    <h2>{{ t('play.journal') }}</h2>
    <ol ref="list">
      <li v-for="(line, index) in lines" :key="index" :class="line.tone">
        <strong v-if="line.title">{{ line.title }}</strong>
        <p v-if="line.text">{{ line.text }}</p>
      </li>
    </ol>
  </section>
</template>

<style scoped>
.journal {
  display: flex;
  flex-direction: column;
  min-height: 0;
}
h2 {
  margin: 0 0 0.5rem;
  font-size: 1.1rem;
}
ol {
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
.narrative {
  border-left-color: var(--p-primary-color);
  background: var(--p-content-hover-background);
}
.narrative p {
  font-family: Georgia, serif;
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
.muted {
  color: var(--p-text-muted-color);
  font-size: 0.85rem;
  border-left-color: transparent;
}
</style>
