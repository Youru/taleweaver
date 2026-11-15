<script setup lang="ts">
import { ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Textarea from 'primevue/textarea'

/**
 * A part of the scenario the workshop has no form for, edited as JSON: it is checked when the field
 * is left, and kept unchanged while it does not parse.
 */
const model = defineModel<unknown>({ required: true })
defineProps<{ label: string; rows?: number }>()

const { t } = useI18n()
const id = useId()
const source = ref('')
const error = ref<string | null>(null)
watch(
  model,
  (value) => {
    source.value = JSON.stringify(value, null, 2) ?? ''
    error.value = null
  },
  { immediate: true, deep: true },
)

function commit() {
  try {
    const parsed: unknown = source.value.trim() ? JSON.parse(source.value) : undefined
    error.value = null
    if (JSON.stringify(parsed) !== JSON.stringify(model.value)) model.value = parsed
  } catch (e) {
    error.value = t('workshop.events.badJson', { message: (e as Error).message })
  }
}
</script>

<template>
  <div class="field">
    <label :for="id">{{ label }}</label>
    <Textarea
      :id="id"
      v-model="source"
      :rows="rows ?? 5"
      auto-resize
      class="json"
      :invalid="!!error"
      spellcheck="false"
      @blur="commit"
    />
    <small v-if="error" class="error">{{ error }}</small>
  </div>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.json {
  font-family: ui-monospace, Consolas, monospace;
  font-size: 0.8rem;
}
.error {
  color: #dc2626;
}
</style>
