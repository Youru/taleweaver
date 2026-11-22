<script setup lang="ts">
import { computed } from 'vue'
import { readInline, type Piece } from '../lib/inline'

/** A line of the rulebook: bold, icons, words found, links to other rules. Never HTML from a file. */
const props = defineProps<{
  text: string
  marks?: string[]
  /** Inside a button, a link stays plain text. */
  plain?: boolean
}>()
const emit = defineEmits<{ follow: [link: NonNullable<Piece['link']>] }>()
const pieces = computed(() => readInline(props.text, props.marks))
</script>

<template>
  <span>
    <template v-for="(piece, index) in pieces" :key="index">
      <img v-if="piece.icon" class="icon" :src="piece.icon" :alt="piece.text" :title="piece.text" />
      <button v-else-if="piece.link && !plain" type="button" class="link" @click="emit('follow', piece.link)">
        {{ piece.text }}
      </button>
      <component :is="piece.mark ? 'mark' : 'span'" v-else :class="{ bold: piece.bold }">{{ piece.text }}</component>
    </template>
  </span>
</template>

<style scoped>
.icon {
  height: 1.15em;
  width: auto;
  vertical-align: -0.2em;
}
.bold {
  font-weight: 700;
}
mark {
  padding: 0;
  border-radius: 2px;
  background: rgb(232 184 66 / 0.45);
  color: inherit;
}
.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--mid-crimson);
  font: inherit;
  text-decoration: underline;
  cursor: pointer;
}
</style>
