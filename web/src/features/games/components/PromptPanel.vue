<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Answer, Prompt } from '../api/types'
import SpecialAnswer from './SpecialAnswer.vue'

const props = defineProps<{
  prompt: Prompt
  pending: boolean
  /** What the board suggests for a yes/no question, and why. */
  suggestion?: { option: string; reason: string }
}>()
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

const fixedLabels: Record<string, string> = {
  yes: 'prompt.yes',
  no: 'prompt.no',
  continue: 'prompt.continue',
  apply: 'prompt.apply',
  skip: 'prompt.skip',
  skull: 'prompt.skull',
}
const buttons = computed(() =>
  props.prompt.kind === 'activeGroup'
    ? []
    : // Poison is applied with the number typed
      props.prompt.options.filter((o) => o.id !== 'apply' || props.prompt.action?.effects?.[0] !== 'poison'),
)
/** A long list (a reward to choose) is a list to search. */
const asList = computed(() => buttons.value.length > 12)
const listed = computed(() => buttons.value.map((o) => ({ value: o.id, label: label(o) })))
const picked = ref<string>()
function label(option: Prompt['options'][number]): string {
  if (option.label) return pick(option.label, language.value)
  const key = fixedLabels[option.id]
  return key ? t(key) : option.id
}

const value = ref<number | null>(0)

/** The suggested option stands out; without a suggestion, the first one gets the focus. */
const focused = computed(() => {
  const index = buttons.value.findIndex((o) => o.id === props.suggestion?.option)
  return index < 0 ? 0 : index
})
function severity(option: Prompt['options'][number]) {
  if (props.suggestion) return option.id === props.suggestion.option ? undefined : 'secondary'
  return option.id === 'no' || option.id === 'fail' ? 'secondary' : undefined
}
</script>

<template>
  <section class="prompt" role="region" :aria-label="t(`prompt.${prompt.kind}`)">
    <p v-if="heading" class="heading">{{ heading }}</p>
    <p v-if="question" class="question">{{ question }}</p>
    <p v-if="prompt.decider" class="decider">{{ t('prompt.decides', { name: prompt.decider }) }}</p>
    <SpecialAnswer :prompt="prompt" :pending="pending" @answer="emit('answer', $event)" />
    <p v-if="suggestion" class="suggestion">
      <i class="pi pi-lightbulb" aria-hidden="true" />
      {{ t('ai.suggestion', { answer: label({ id: suggestion.option }) }) }} — {{ suggestion.reason }}
    </p>
    <form v-if="prompt.kind === 'number'" class="options" @submit.prevent="emit('answer', { value: value ?? 0 })">
      <InputNumber v-model="value" :min="0" :max="999" input-id="prompt-number" :aria-label="question" />
      <Button type="submit" :label="t('prompt.number')" :loading="pending" />
    </form>
    <form v-else-if="asList" class="options" @submit.prevent="picked && emit('answer', { option: picked })">
      <Select
        v-model="picked"
        :options="listed"
        option-label="label"
        option-value="value"
        filter
        :aria-label="question || t(`prompt.${prompt.kind}`)"
      />
      <Button type="submit" :label="t('prompt.number')" :disabled="pending || !picked" />
    </form>
    <div v-else class="options">
      <Button
        v-for="(option, index) in buttons"
        :key="option.id"
        :label="label(option)"
        :severity="severity(option)"
        :outlined="!!suggestion && option.id !== suggestion.option"
        :autofocus="index === focused"
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
.decider {
  margin: -0.4rem 0 0.6rem;
  font-weight: 600;
  color: var(--p-primary-color);
}
.suggestion {
  margin: -0.4rem 0 0.75rem;
  font-size: 0.9rem;
  color: var(--p-text-muted-color);
}
.options {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
