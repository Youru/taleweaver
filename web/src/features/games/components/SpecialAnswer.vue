<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Answer, Prompt } from '../api/types'

/** The answers that are more than a button: the Active Group, a total rolled, the Damage of Poison. */
const props = defineProps<{ prompt: Prompt; pending: boolean }>()
const emit = defineEmits<{ answer: [answer: Answer] }>()

const { t, te } = useI18n()
const { language } = storeToRefs(usePreferences())

const chosen = ref<string[]>(props.prompt.options.slice(0, props.prompt.max ?? 0).map((o) => o.id))
const wanted = computed(() => props.prompt.max ?? 4)

const testers = computed(() => props.prompt.testers ?? [])
const tester = ref(testers.value.length === 1 ? testers.value[0]?.name : undefined)
const checks = computed(() => (props.prompt.checks?.length ? props.prompt.checks : [{ skill: null, difficulty: null }]))
const skill = ref(0)
const total = ref<number | null>(null)
const skillName = (id: string | null | undefined) => (id ? (te(`skill.${id}`) ? t(`skill.${id}`) : id) : '')
const checkOptions = computed(() =>
  checks.value.map((check, value) => ({
    value,
    label: [skillName(check.skill), check.difficulty].filter(Boolean).join(' ') || t('prompt.total'),
  })),
)
/** "2 purple dice + 3": what to roll, before the Passives of the cards. */
const toRoll = computed(() => {
  const id = checks.value[skill.value]?.skill?.toLowerCase()
  const value = id ? testers.value.find((x) => x.name === tester.value)?.skills[id] : undefined
  return value === undefined ? t('prompt.toRollUnknown') : t('prompt.toRoll', { n: value })
})

const poison = computed(
  () => props.prompt.kind === 'status' && props.prompt.action?.effects?.[0] === 'poison',
)
const dealt = ref<number | null>(null)
</script>

<template>
  <form
    v-if="prompt.kind === 'activeGroup'"
    class="special"
    @submit.prevent="emit('answer', { adventurers: chosen })"
  >
    <label v-for="option in prompt.options" :key="option.id" class="member">
      <Checkbox v-model="chosen" :value="option.id" :disabled="!chosen.includes(option.id) && chosen.length >= wanted" />
      {{ pick(option.label, language) || option.id }}
    </label>
    <span class="hint">{{ t('prompt.groupHint', { n: chosen.length, max: wanted }) }}</span>
    <Button type="submit" :label="t('prompt.confirmGroup')" :disabled="pending || chosen.length !== wanted" />
  </form>

  <form
    v-else-if="prompt.kind === 'skillCheck'"
    class="special"
    @submit.prevent="total !== null && emit('answer', { option: `total-${skill}`, value: total, instance: tester })"
  >
    <Select
      v-if="testers.length > 1"
      v-model="tester"
      :options="testers"
      option-label="name"
      option-value="name"
      :placeholder="t('prompt.whoTests')"
      :aria-label="t('prompt.whoTests')"
    />
    <strong v-else-if="tester">{{ tester }}</strong>
    <Select
      v-if="checkOptions.length > 1"
      v-model="skill"
      :options="checkOptions"
      option-label="label"
      option-value="value"
      :aria-label="t('prompt.whichSkill')"
    />
    <span v-else>{{ checkOptions[0]?.label }}</span>
    <span class="hint">{{ toRoll }}</span>
    <InputNumber v-model="total" :min="0" :max="999" :input-style="{ width: '5rem' }" :aria-label="t('prompt.total')" />
    <Button type="submit" :label="t('prompt.total')" :disabled="pending || total === null || (testers.length > 1 && !tester)" />
  </form>

  <form
    v-else-if="poison"
    class="special"
    @submit.prevent="dealt !== null && emit('answer', { option: 'apply', value: dealt })"
  >
    <InputNumber v-model="dealt" :min="0" :max="99" :input-style="{ width: '5rem' }" :aria-label="t('prompt.dealt')" />
    <Button type="submit" :label="t('prompt.dealt')" :disabled="pending || dealt === null" />
  </form>
</template>

<style scoped>
.special {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.9rem;
  margin-bottom: 0.6rem;
}
.member {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}
.hint {
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
}
</style>
