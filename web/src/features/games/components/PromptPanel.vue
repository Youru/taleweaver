<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Answer, Prompt } from '../api/types'

const props = defineProps<{ prompt: Prompt; pending: boolean }>()
const emit = defineEmits<{ answer: [answer: Answer] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const heading = computed(() =>
  props.prompt.kind === 'aiCondition'
    ? t('prompt.aiCondition', { instance: props.prompt.instance ?? '' })
    : props.prompt.kind === 'spawnPossible'
      ? t('prompt.spawnPossible')
      : '',
)
const question = computed(() => pick(props.prompt.question, language.value))

const fixedLabels: Record<string, string> = { yes: 'prompt.yes', no: 'prompt.no', continue: 'prompt.continue' }
function label(option: Prompt['options'][number]): string {
  if (option.label) return pick(option.label, language.value)
  const key = fixedLabels[option.id]
  return key ? t(key) : option.id
}

const value = ref<number | null>(0)
</script>

<template>
  <section class="prompt" role="region" :aria-label="t(`prompt.${prompt.kind}`)">
    <p v-if="heading" class="heading">{{ heading }}</p>
    <p v-if="question" class="question">{{ question }}</p>
    <form v-if="prompt.kind === 'number'" class="options" @submit.prevent="emit('answer', { value: value ?? 0 })">
      <InputNumber v-model="value" :min="0" :max="999" input-id="prompt-number" :aria-label="question" />
      <Button type="submit" :label="t('prompt.number')" :loading="pending" />
    </form>
    <div v-else class="options">
      <Button
        v-for="(option, index) in prompt.options"
        :key="option.id"
        :label="label(option)"
        :severity="option.id === 'no' || option.id === 'fail' ? 'secondary' : undefined"
        :autofocus="index === 0"
        :disabled="pending"
        @click="emit('answer', { option: option.id })"
      />
    </div>
  </section>
</template>

<style scoped>
.prompt {
  border: 2px solid var(--p-primary-color);
  border-radius: var(--p-content-border-radius);
  padding: 0.75rem 1rem;
  background: var(--p-content-background);
}
.heading {
  margin: 0;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.question {
  margin: 0.25rem 0 0.75rem;
  font-size: 1.15rem;
  font-weight: 600;
}
.options {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
