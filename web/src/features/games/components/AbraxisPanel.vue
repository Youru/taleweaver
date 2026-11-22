<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { AbraxisGame, Answer } from '../api/types'

/** Abraxis Battle Dice (rulebook p. 85): the dice are rolled at the table, the GM types what each one missed. */
const props = defineProps<{ game: AbraxisGame; pending: boolean }>()
const emit = defineEmits<{ answer: [answer: Answer] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const playing = computed(() => props.game.players.filter((p) => p.points > 0))
// Never a silent 0: that would be an exact match, and a point won
const misses = ref<Record<string, number | null>>({})
const penalty = ref<number | null>(0)
watch(
  () => props.game.round,
  () => {
    misses.value = {}
    penalty.value = 0
  },
)
const ready = computed(() => playing.value.every((p) => misses.value[p.name] != null))
function validate() {
  emit('answer', { misses: playing.value.map((p) => misses.value[p.name] ?? 0), value: penalty.value ?? 0 })
}
</script>

<template>
  <form class="abraxis" @submit.prevent="validate">
    <h3>{{ pick(game.name, language) || t('abraxis.title') }} · {{ t('abraxis.round', { round: game.round }) }}</h3>
    <p v-if="game.rule" class="rule">{{ pick(game.rule, language) }}</p>
    <p class="help">{{ t('abraxis.help') }}</p>
    <ul>
      <li v-for="player in game.players" :key="player.name" :class="{ out: !player.points }">
        <strong>{{ player.name }}</strong>
        <span>{{ player.points ? t('abraxis.points', { n: player.points }) : t('abraxis.out') }}</span>
        <InputNumber
          v-if="player.points"
          v-model="misses[player.name]"
          :min="0"
          :max="99"
          show-buttons
          :input-style="{ width: '6rem' }"
          :aria-label="t('abraxis.misses', { name: player.name })"
        />
      </li>
    </ul>
    <label class="penalty">
      <span>{{ t('abraxis.penalty') }}</span>
      <InputNumber v-model="penalty" :min="0" :max="10" show-buttons :input-style="{ width: '6rem' }" />
    </label>
    <Button type="submit" :label="t('abraxis.validate')" icon="pi pi-check" :disabled="!ready" :loading="pending" />
  </form>
</template>

<style scoped>
.abraxis {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  border: 2px solid var(--p-primary-color);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
  overflow-y: auto;
}
h3,
p {
  margin: 0;
}
.rule {
  font-style: italic;
}
.help {
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
}
li {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto 7rem;
  align-items: center;
  gap: 0.75rem;
  padding: 0.2rem 0;
}
.out {
  opacity: 0.5;
}
.penalty {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
}
</style>
