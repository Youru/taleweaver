<script setup lang="ts">
import { computed, useId } from 'vue'
import { storeToRefs } from 'pinia'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import { pick, type LocalizedText } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { setText } from '../lib/graph'

/** A text of the scenario, edited in the interface language (the other languages are kept). */
const model = defineModel<LocalizedText | undefined>({ required: true })
defineProps<{ label: string; long?: boolean; rows?: number }>()

const id = useId()
const { language } = storeToRefs(usePreferences())
const value = computed({
  get: () => pick(model.value, language.value),
  set: (next: string) => (model.value = setText(model.value, language.value, next)),
})
</script>

<template>
  <div class="field">
    <label :for="id">{{ label }}</label>
    <Textarea v-if="long" :id="id" v-model="value" :rows="rows ?? 4" auto-resize />
    <InputText v-else :id="id" v-model="value" />
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
</style>
