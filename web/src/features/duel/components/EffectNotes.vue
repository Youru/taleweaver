<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Message from 'primevue/message'

/** The Effects of both figures that change this Attack or Spell: what was counted, what is left to the GM. */
defineProps<{ notes: { key: string; effect: string }[] }>()
const { t } = useI18n()
</script>

<template>
  <Message v-if="notes.length" severity="secondary" class="effects" :aria-label="t('duel.effect.title')">
    <strong>{{ t('duel.effect.title') }}</strong>
    <ul>
      <li v-for="note in notes" :key="note.key">
        <strong>{{ note.effect }}</strong> : {{ t(`duel.effect.${note.key}`) }}
      </li>
    </ul>
  </Message>
</template>

<style scoped>
.effects ul {
  margin: 0.2rem 0 0;
  padding-left: 1.1rem;
}
.effects li {
  font-size: 0.9rem;
}
</style>
