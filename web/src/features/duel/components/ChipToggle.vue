<script setup lang="ts">
import GameIcon from './GameIcon.vue'

/** An on/off situation (Flanking, Dodge…), big enough for a finger. */
const on = defineModel<boolean>({ required: true })
defineProps<{ label: string; icon?: string; disabled?: boolean }>()
</script>

<template>
  <button type="button" class="chip" :class="{ on }" :aria-pressed="on" :disabled="disabled" @click="on = !on">
    <GameIcon v-if="icon" :src="icon" size="1.3em" />
    <i v-else class="pi" :class="on ? 'pi-check-circle' : 'pi-circle'" />
    {{ label }}
  </button>
</template>

<style scoped>
.chip {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  min-height: 2.2rem;
  padding: 0.3rem 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 2rem;
  background: transparent;
  color: inherit;
  font: inherit;
  font-size: 0.85rem;
  cursor: pointer;
  transition:
    background 0.12s ease,
    border-color 0.12s ease;
}
.chip:hover:not(:disabled) {
  border-color: var(--p-primary-color);
}
.chip.on {
  background: var(--p-highlight-background);
  border-color: var(--p-primary-color);
  color: var(--p-highlight-color);
}
.chip:disabled {
  opacity: 0.6;
  cursor: default;
}
</style>
