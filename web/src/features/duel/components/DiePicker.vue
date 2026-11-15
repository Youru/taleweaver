<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Die, DieFace } from '../api/types'
import { dieIcon, inkOn, symbolIcon, type SymbolName } from '../lib/icons'
import GameIcon from './GameIcon.vue'

/** The six faces of one die: the GM taps the one the table rolled. */
const face = defineModel<number | null>({ required: true })
const props = defineProps<{ die: Die | undefined; colour: string; label: string }>()

const { t } = useI18n()

const symbols = (f: DieFace): SymbolName[] => [
  ...Array<SymbolName>(f.star).fill('star'),
  ...Array<SymbolName>(f.shield).fill('shield'),
  ...Array<SymbolName>(f.book).fill('book'),
  ...(f.skull ? (['skull'] as SymbolName[]) : []),
]

function describe(f: DieFace): string {
  const parts = [
    ...(props.colour === 'black' ? [] : [String(f.value)]),
    ...(f.shield ? [t('duel.faceShields', { n: f.shield })] : []),
    ...(f.book ? [t('duel.faceBooks', { n: f.book })] : []),
    ...(f.star ? [t('duel.faceStars', { n: f.star })] : []),
    ...(f.skull ? [t('duel.faceSkull')] : []),
  ]
  return parts.join(', ') || '0'
}

const ink = computed(() => inkOn(props.colour))
</script>

<template>
  <div class="picker" role="radiogroup" :aria-label="label">
    <span class="name">
      <GameIcon :src="dieIcon(colour)" size="1.3em" />
      {{ label }}
    </span>
    <div v-if="die" class="faces">
      <button
        v-for="(f, index) in die.faces"
        :key="index"
        type="button"
        role="radio"
        class="face"
        :class="[colour, ink, { chosen: face === index, dim: face !== null && face !== index }]"
        :aria-checked="face === index"
        :aria-label="describe(f)"
        @click="face = face === index ? null : index"
      >
        <span v-if="colour !== 'black'" class="value">{{ f.value }}</span>
        <span class="symbols">
          <GameIcon v-for="(s, i) in symbols(f)" :key="i" :src="symbolIcon(s, colour)" size="0.95em" />
        </span>
      </button>
    </div>
    <span v-else class="missing">{{ t('duel.unknownDice') }}</span>
  </div>
</template>

<style scoped>
.picker {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.name {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.faces {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 0.35rem;
}
.face {
  aspect-ratio: 1;
  min-width: 2.8rem;
  max-width: 4.6rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.2rem;
  border: 2px solid rgb(0 0 0 / 0.35);
  border-radius: 0.6rem;
  cursor: pointer;
  padding: 0.15rem;
  font: inherit;
  transition:
    transform 0.08s ease,
    opacity 0.12s ease,
    box-shadow 0.12s ease;
}
.face:active {
  transform: scale(0.94);
}
.face.light {
  color: #fff;
}
.face.dark {
  color: #1d1b19;
}
.face.chosen {
  box-shadow:
    0 0 0 3px var(--p-content-background),
    0 0 0 6px var(--p-primary-color);
  transform: scale(1.04);
}
.face.dim {
  opacity: 0.35;
}
.value {
  font-family: Georgia, 'Times New Roman', serif;
  font-weight: 700;
  font-size: 1.45rem;
  line-height: 1.1;
  /* Georgia's old-style figures (3, 4, 5…) drop below the line and ran into the symbols */
  font-variant-numeric: lining-nums;
}
.symbols {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.05rem;
  min-height: 0.95em;
}
.missing {
  font-size: 0.85rem;
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
</style>
