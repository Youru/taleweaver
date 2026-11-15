<script setup lang="ts">
import InputNumber from 'primevue/inputnumber'
import { typed } from '../lib/format'
import GameIcon from './GameIcon.vue'

/** A number the GM adjusts with big − / + buttons (or types), with its game icon. */
const value = defineModel<number | null>({ required: true })
withDefaults(defineProps<{ label: string; icon?: string; min?: number; max?: number; nullable?: boolean }>(), {
  icon: undefined,
  min: 0,
  max: 99,
  nullable: false,
})
</script>

<template>
  <label class="stat">
    <span class="label"><GameIcon :src="icon" size="1.3em" /> {{ label }}</span>
    <InputNumber
      v-model="value"
      :aria-label="label"
      :min="min"
      :max="max"
      show-buttons
      button-layout="horizontal"
      size="small"
      increment-button-icon="pi pi-plus"
      decrement-button-icon="pi pi-minus"
      @input="(e) => (value = nullable ? typed(e.value) : (typed(e.value) ?? 0))"
    />
  </label>
</template>

<style scoped>
.stat {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  font-size: 0.85rem;
}
.label {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}
.stat :deep(.p-inputnumber) {
  width: 7.5rem;
  flex: none;
}
.stat :deep(.p-inputnumber-input) {
  width: 100%;
  min-width: 0;
  text-align: center;
  font-variant-numeric: tabular-nums;
}
</style>
