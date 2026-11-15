<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Dice } from '../api/types'
import { dieColours } from '../lib/dice'
import { dieIcon } from '../lib/icons'
import GameIcon from './GameIcon.vue'

/** A pool of dice by colour: tap a die to take it out, "+" to pick one to add. */
const pool = defineModel<string[]>({ required: true })
const props = defineProps<{ dice: Dice | undefined; label: string; max?: number }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const adding = ref(false)

function name(colour: string): string {
  return colour === '*' ? t('duel.unknownDie') : pick(props.dice?.[colour]?.name, language.value) || colour
}

function remove(index: number) {
  pool.value = pool.value.filter((_, i) => i !== index)
}

function add(colour: string) {
  if (pool.value.length < (props.max ?? 6)) pool.value = [...pool.value, colour]
  adding.value = false
}
</script>

<template>
  <div class="pool" role="group" :aria-label="label">
    <span class="label">{{ label }}</span>
    <span class="dice">
      <button
        v-for="(colour, index) in pool"
        :key="index"
        type="button"
        class="die"
        :class="{ unknown: colour === '*' }"
        :title="name(colour)"
        :aria-label="t('duel.removeDie', { die: name(colour) })"
        @click="remove(index)"
      >
        <GameIcon v-if="colour !== '*'" :src="dieIcon(colour)" size="1.9rem" />
        <span v-else>?</span>
      </button>
      <button
        v-if="pool.length < (max ?? 6)"
        type="button"
        class="die more"
        :aria-label="t('duel.addDieMenu')"
        :aria-expanded="adding"
        @click="adding = !adding"
      >
        <i class="pi pi-plus" />
      </button>
    </span>
    <span v-if="adding" class="choices">
      <button
        v-for="colour in dieColours"
        :key="colour"
        type="button"
        class="die"
        :title="name(colour)"
        :aria-label="t('duel.addDie', { die: name(colour) })"
        @click="add(colour)"
      >
        <GameIcon :src="dieIcon(colour)" size="1.6rem" />
      </button>
    </span>
  </div>
</template>

<style scoped>
.pool {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.label {
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.dice,
.choices {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem;
}
.choices {
  padding: 0.35rem;
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-hover-background);
}
.die {
  display: grid;
  place-items: center;
  min-width: 2.2rem;
  min-height: 2.2rem;
  padding: 0.1rem;
  border: none;
  border-radius: 0.4rem;
  background: transparent;
  color: var(--p-text-muted-color);
  cursor: pointer;
  font: inherit;
  font-weight: 700;
}
.die:hover {
  background: var(--p-content-hover-background);
}
.dice .die:not(.more):hover {
  outline: 2px solid var(--p-red-500);
}
.unknown,
.more {
  border: 2px dashed var(--p-content-border-color);
}
</style>
