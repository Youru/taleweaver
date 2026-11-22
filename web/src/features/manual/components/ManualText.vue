<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { piecesOf } from '../lib/manual'

/** A text of the manual, with the interface's labels and the links to other sections. */
const props = defineProps<{ text: string; titles: Map<string, string> }>()
const { t } = useI18n()
const pieces = computed(() => piecesOf(props.text))
</script>

<template>
  <span>
    <template v-for="(piece, i) in pieces" :key="i">
      <RouterLink v-if="'section' in piece" :to="{ hash: `#${piece.section}` }">{{
        titles.get(piece.section)
      }}</RouterLink>
      <strong v-else-if="'label' in piece" class="label">{{ t(piece.label) }}</strong>
      <template v-else>{{ piece.text }}</template>
    </template>
  </span>
</template>

<style scoped>
.label {
  color: var(--mid-crimson);
}
a {
  color: var(--mid-crimson);
  font-weight: 600;
}
</style>
