<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Select from 'primevue/select'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftScenario } from '../api/types'
import { captionOf, endingsOf } from '../lib/graph'

/** Where an exit leads: a node of the scenario or an ending. */
const target = defineModel<string | undefined>({ required: true })
const props = defineProps<{ scenario: DraftScenario; label: string; from: string }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const endingLabel = (id: string) =>
  id === '@victory'
    ? t('workshop.endVictory')
    : id === '@defeat'
      ? t('workshop.endDefeat')
      : id === '@currentStoryRound'
        ? t('workshop.endStoryRound')
        : id
const options = computed(() => [
  ...props.scenario.nodes
    .filter((n) => n.id !== props.from)
    .map((n) => ({
      value: n.id,
      label: `${n.type === 'encounter' ? t('workshop.encounter') : t('workshop.scene')} · ${captionOf(n, language.value)}`,
    })),
  ...endingsOf(props.scenario).map((id) => ({ value: id, label: endingLabel(id) })),
])
</script>

<template>
  <Select
    v-model="target"
    :options="options"
    option-label="label"
    option-value="value"
    :placeholder="t('workshop.nowhere')"
    :aria-label="label"
    show-clear
    class="target"
  />
</template>

<style scoped>
.target {
  width: 100%;
}
</style>
