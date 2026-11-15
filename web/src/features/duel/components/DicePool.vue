<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { Dice } from '../api/types'
import { dieColours } from '../lib/dice'

/** A pool of dice by colour: tap a die to take it out, a swatch to add one. */
const pool = defineModel<string[]>({ required: true })
const props = defineProps<{ dice: Dice | undefined; label: string; max?: number }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

function name(colour: string): string {
  return colour === '*' ? t('duel.unknownDie') : pick(props.dice?.[colour]?.name, language.value) || colour
}

function remove(index: number) {
  pool.value = pool.value.filter((_, i) => i !== index)
}

function add(colour: string) {
  if (pool.value.length < (props.max ?? 6)) pool.value = [...pool.value, colour]
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
        :class="colour === '*' ? 'unknown' : colour"
        :title="name(colour)"
        :aria-label="t('duel.removeDie', { die: name(colour) })"
        @click="remove(index)"
      >
        {{ colour === '*' ? '?' : '' }}
      </button>
      <span v-if="!pool.length" class="empty">—</span>
    </span>
    <span class="add">
      <button
        v-for="colour in dieColours"
        :key="colour"
        type="button"
        class="swatch"
        :class="colour"
        :title="name(colour)"
        :aria-label="t('duel.addDie', { die: name(colour) })"
        @click="add(colour)"
      />
    </span>
  </div>
</template>

<style scoped>
.pool {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem 0.6rem;
}
.label {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
  min-width: 6.5rem;
}
.dice,
.add {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
.add {
  margin-left: auto;
}
.die,
.swatch {
  border: 1px solid rgb(0 0 0 / 0.35);
  cursor: pointer;
  padding: 0;
  font: inherit;
  font-weight: 700;
}
.die {
  width: 1.6rem;
  height: 1.6rem;
  border-radius: 0.35rem;
  color: #fff;
}
.swatch {
  width: 1rem;
  height: 1rem;
  border-radius: 0.2rem;
  opacity: 0.8;
}
.swatch:hover {
  opacity: 1;
}
.die:hover {
  outline: 2px solid var(--p-red-500);
}
.empty {
  color: var(--p-text-muted-color);
}
.purple {
  background: #7a3ec2;
}
.white {
  background: #f4f4f4;
}
.orange {
  background: #e85404;
}
.teal {
  background: #00c4b4;
}
.red {
  background: #a3233f;
}
.green {
  background: #97d70b;
}
.grey {
  background: #5b6670;
}
.blue {
  background: #1b45bb;
}
.black {
  background: #2e2a27;
}
.unknown {
  background: transparent;
  border-style: dashed;
  color: var(--p-text-muted-color);
}
</style>
