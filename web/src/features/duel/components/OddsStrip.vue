<script setup lang="ts">
import { useI18n } from 'vue-i18n'

/** The odds before rolling, as a few big figures; or why they cannot be worked out. */
defineProps<{ items: { label: string; value: string }[]; note?: string; missing?: string }>()
const { t } = useI18n()
</script>

<template>
  <section class="odds" role="status" :aria-label="t('duel.odds')">
    <p v-if="missing" class="missing">{{ missing }}</p>
    <template v-else>
      <dl>
        <div v-for="item in items" :key="item.label">
          <dt>{{ item.label }}</dt>
          <dd>{{ item.value }}</dd>
        </div>
      </dl>
      <p v-if="note" class="note">{{ note }}</p>
    </template>
  </section>
</template>

<style scoped>
.odds {
  padding: 0.6rem 0.8rem;
  border-radius: var(--p-content-border-radius);
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
}
dl {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-around;
  gap: 0.4rem 1.2rem;
  margin: 0;
}
dl div {
  display: flex;
  flex-direction: column-reverse;
  align-items: center;
}
dt {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  opacity: 0.85;
}
dd {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.note,
.missing {
  margin: 0.3rem 0 0;
  text-align: center;
  font-size: 0.8rem;
  opacity: 0.85;
}
.missing {
  margin: 0;
}
</style>
