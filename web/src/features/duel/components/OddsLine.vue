<script setup lang="ts">
import { useI18n } from 'vue-i18n'

/** The odds before rolling, or why they cannot be worked out. */
defineProps<{ missing: boolean }>()
const { t } = useI18n()
</script>

<template>
  <p class="odds" role="status" :aria-label="t('duel.odds')">
    <template v-if="missing">{{ t('duel.unknownDice') }}</template>
    <slot v-else />
  </p>
</template>

<style scoped>
.odds {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.2rem 0.8rem;
  margin: 0;
  padding: 0.45rem 0.6rem;
  border-radius: var(--p-content-border-radius);
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-size: 0.9rem;
}
.odds :deep(small) {
  flex-basis: 100%;
  opacity: 0.8;
}
</style>
