<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import { ApiError } from '@/shared/api/http'
import { useStartGame } from '../api/queries'
import type { GameView } from '../api/types'

const props = defineProps<{ scenario: { id: string; title: string } | null }>()
const emit = defineEmits<{ close: []; started: [game: GameView] }>()

const { t } = useI18n()
const party = ref<string[]>(['', ''])
watch(
  () => props.scenario,
  () => (party.value = ['', '']),
)

const names = computed(() => party.value.map((n) => n.trim()).filter(Boolean))
const valid = computed(
  () => names.value.length > 0 && new Set(names.value.map((n) => n.toLowerCase())).size === names.value.length,
)

const start = useStartGame()
const error = computed(() => {
  const value = start.error.value
  if (!(value instanceof ApiError)) return value ? String(value) : null
  const fieldErrors = Object.values(value.problem?.errors ?? {}).flat()
  return fieldErrors.length ? fieldErrors.join(' ') : value.message
})

function submit() {
  if (!props.scenario || !valid.value) return
  start.mutate({ scenario: props.scenario.id, party: names.value }, { onSuccess: (game) => emit('started', game) })
}
</script>

<template>
  <Dialog
    :visible="!!scenario"
    modal
    :header="t('start.title', { scenario: scenario?.title ?? '' })"
    :style="{ width: '28rem' }"
    @update:visible="(visible: boolean) => !visible && emit('close')"
  >
    <form class="form" @submit.prevent="submit">
      <fieldset>
        <legend>{{ t('start.party') }}</legend>
        <div v-for="(_, index) in party" :key="index" class="row">
          <label :for="`adventurer-${index}`">{{ t('start.adventurer', { n: index + 1 }) }}</label>
          <InputText :id="`adventurer-${index}`" v-model="party[index]" maxlength="40" />
          <Button
            v-if="party.length > 1"
            icon="pi pi-times"
            text
            severity="secondary"
            :aria-label="t('start.remove')"
            @click="party.splice(index, 1)"
          />
        </div>
        <Button v-if="party.length < 6" :label="t('start.add')" icon="pi pi-plus" text @click="party.push('')" />
      </fieldset>
      <Message v-if="error" severity="error">{{ error }}</Message>
      <div class="buttons">
        <Button :label="t('common.cancel')" severity="secondary" text @click="emit('close')" />
        <Button type="submit" :label="t('start.start')" :disabled="!valid" :loading="start.isPending.value" />
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
.row {
  display: grid;
  grid-template-columns: 7rem 1fr auto;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.4rem;
}
.buttons {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
</style>
