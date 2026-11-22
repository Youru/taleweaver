<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import ChipToggle from './ChipToggle.vue'
import StatField from './StatField.vue'

/** The end of a duel: the Damage (the GM may correct it) and the one button that plays it out. */
const amount = defineModel<number>('amount', { required: true })
const token = defineModel<boolean>('token', { required: true })
defineProps<{
  label: string
  /** The outcome deals Damage: it can be changed (Resistances, an Ability…). */
  editable: boolean
  /** Summon tokens the target's summoner may discard (2 less Damage each). */
  tokens: number
  locked: boolean
  sending: boolean
}>()
const emit = defineEmits<{ confirm: []; reset: [] }>()

const { t } = useI18n()
</script>

<template>
  <div class="resolve">
    <StatField v-if="editable" v-model="amount" :label="t('duel.toApply')" :max="999" />
    <ChipToggle v-if="editable && tokens && amount > 0" v-model="token" :label="t('allies.useToken', { n: tokens })" />
    <div class="buttons">
      <Button
        :label="label"
        icon="pi pi-check"
        :loading="sending"
        :disabled="locked || sending"
        @click="emit('confirm')"
      />
      <Button
        :label="t('duel.newRoll')"
        icon="pi pi-replay"
        severity="secondary"
        text
        :disabled="sending"
        @click="emit('reset')"
      />
    </div>
    <p v-if="locked && !sending" class="why">{{ t('duel.answerFirst') }}</p>
  </div>
</template>

<style scoped>
/* In view however many dice there are */
.resolve {
  position: sticky;
  bottom: -0.2rem;
  z-index: 1;
  flex: none;
  margin-top: auto;
  background: var(--p-dialog-background);
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.6rem 0 0.2rem;
  border-top: 1px solid var(--p-content-border-color);
}
/* There the window's padded content scrolls, not the column: down to its edge */
@media (max-width: 1000px) {
  .resolve {
    bottom: -1.25rem;
    padding-bottom: 1.25rem;
  }
}
.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.why {
  margin: 0;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
</style>
