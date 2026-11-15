<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Fighter } from '../lib/fighters'

/** What the card says to keep in mind (passives, Follow Up symbols), folded away. */
defineProps<{ fighter: Fighter }>()
const { t } = useI18n()
</script>

<template>
  <details v-if="fighter.reminders.length" class="reminders">
    <summary>{{ t('duel.reminders') }}</summary>
    <dl>
      <template v-for="(reminder, index) in fighter.reminders" :key="index">
        <dt>{{ reminder.name }}</dt>
        <dd>{{ reminder.text }}</dd>
      </template>
    </dl>
  </details>
</template>

<style scoped>
.reminders {
  font-size: 0.85rem;
}
summary {
  cursor: pointer;
  color: var(--p-text-muted-color);
}
dl {
  margin: 0.3rem 0 0;
}
dt {
  font-weight: 600;
}
dd {
  margin: 0 0 0.35rem;
  color: var(--p-text-muted-color);
}
</style>
