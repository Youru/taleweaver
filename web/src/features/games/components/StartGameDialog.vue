<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import { ApiError } from '@/shared/api/http'
import { useStartGame } from '../api/queries'
import type { GameOptions, GameView } from '../api/types'
import { activeOptions, optionGroups, optionsToSend, playedByHand } from '../lib/options'

const props = defineProps<{ scenario: { id: string; title: string } | null }>()
const emit = defineEmits<{ close: []; started: [game: GameView] }>()

const { t } = useI18n()
watch(
  () => props.scenario,
  () => {
    options.value = {}
    optionsOpen.value = false
  },
)
/** All off by default (roadmap §8.4). */
const options = ref<GameOptions>({})
const optionsOpen = ref(false)
const optionCount = computed(() => activeOptions(options.value).length)
const limits = computed(() =>
  [5, 4, 3].map((value) => ({ value, label: t(`options.urgencyLimit.${value}`, { n: value }) })),
)

const start = useStartGame()
const error = computed(() => {
  const value = start.error.value
  if (!(value instanceof ApiError)) return value ? String(value) : null
  const fieldErrors = Object.values(value.problem?.errors ?? {}).flat()
  return fieldErrors.length ? fieldErrors.join(' ') : value.message
})

function submit() {
  if (!props.scenario) return
  start.mutate(
    // Nobody named: the party is prepared within the scenario's rules, with the players (roadmap §5.2)
    { scenario: props.scenario.id, party: [], options: optionsToSend(options.value) },
    { onSuccess: (game) => emit('started', game) },
  )
}
</script>

<template>
  <Dialog
    :visible="!!scenario"
    modal
    :header="t('start.title', { scenario: scenario?.title ?? '' })"
    :style="{ width: optionsOpen ? 'min(40rem, 95vw)' : '28rem' }"
    @update:visible="(visible: boolean) => !visible && emit('close')"
  >
    <form class="form" @submit.prevent="submit">
      <p class="next">{{ t('start.next') }}</p>
      <Button
        class="more"
        :icon="optionsOpen ? 'pi pi-minus' : 'pi pi-plus'"
        :label="optionCount ? t('options.buttonCount', { n: optionCount }) : t('options.button')"
        severity="secondary"
        text
        :aria-expanded="optionsOpen"
        @click="optionsOpen = !optionsOpen"
      />
      <div v-if="optionsOpen" class="options">
        <fieldset v-for="{ group, keys } in optionGroups" :key="group">
          <legend>{{ t(`options.groups.${group}`) }}</legend>
          <template v-for="key in keys" :key="key">
            <label class="option">
              <ToggleSwitch v-model="options[key]" />
              <span>
                <strong>{{ t(`options.${key}.name`) }}</strong>
                <small>{{ t(`options.${key}.text`) }}</small>
                <small v-if="playedByHand(key)" class="by-hand">{{ t('options.byHand') }}</small>
              </span>
            </label>
            <SelectButton
              v-if="key === 'noPlayerElimination' && options.noPlayerElimination"
              :model-value="options.urgencyLimit ?? 4"
              :options="limits"
              option-label="label"
              option-value="value"
              :allow-empty="false"
              class="limit"
              @update:model-value="(value: number) => (options.urgencyLimit = value)"
            />
          </template>
        </fieldset>
      </div>
      <Message v-if="error" severity="error">{{ error }}</Message>
      <div class="buttons">
        <Button :label="t('common.cancel')" severity="secondary" text @click="emit('close')" />
        <Button type="submit" :label="t('start.prepare')" :loading="start.isPending.value" />
      </div>
    </form>
  </Dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
fieldset {
  border: none;
  padding: 0;
  margin: 0;
}
legend {
  font-weight: 600;
  margin-bottom: 0.5rem;
}
.next {
  margin: 0;
}
.more {
  align-self: flex-start;
}
.options {
  max-height: 45vh;
  overflow-y: auto;
}
.option {
  display: flex;
  gap: 0.75rem;
  padding: 0.35rem 0;
  cursor: pointer;
}
.option > :first-child {
  flex: none;
}
.option small {
  display: block;
  color: var(--p-text-muted-color);
}
.option .by-hand {
  font-style: italic;
}
.limit {
  margin: 0.25rem 0 0.5rem 3rem;
}
.buttons {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
</style>
