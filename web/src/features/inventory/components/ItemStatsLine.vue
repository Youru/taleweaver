<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ItemStats } from '@/features/catalog'
import { dieIcon, GameIcon, statIcon } from '@/features/duel'

/** The icons of a card, as a line: its combat dice, hands, Melee/Ranged icon and stat bonuses. */
const props = defineProps<{ stats: ItemStats | null | undefined }>()
const { t } = useI18n()

const attack = computed(() => {
  const s = props.stats
  if (!s?.attack) return undefined
  return s.attack === 'ranged' ? t('inventory.ranged', { n: s.range ?? '?' }) : t(`inventory.attack.${s.attack}`)
})
const bonuses = computed(() => {
  const s = props.stats
  if (!s) return []
  const sign = (value: number | string) => (typeof value === 'number' && value < 0 ? `${value}` : `+${value}`)
  return [
    { key: 'armor', icon: statIcon('armor'), value: s.armor },
    { key: 'defense', icon: statIcon('defense'), value: s.defense },
    { key: 'hp', icon: statIcon('hp'), value: s.health },
    { key: 'movement', icon: undefined, value: s.movement },
  ]
    .filter((b) => b.value !== undefined && b.value !== null)
    .map((b) => ({ ...b, text: sign(b.value as number | string) }))
})
</script>

<template>
  <span v-if="stats" class="stats">
    <span v-if="stats.dice?.length" class="dice" :aria-label="t('inventory.dice', { dice: stats.dice.join(', ') })">
      <GameIcon v-for="(die, i) in stats.dice" :key="i" :src="dieIcon(die)" size="1.1em" />
    </span>
    <span v-if="stats.hands">{{ t('inventory.hands', { n: stats.hands }, stats.hands) }}</span>
    <span v-if="attack">{{ attack }}</span>
    <span v-for="b in bonuses" :key="b.key" class="bonus">
      <GameIcon :src="b.icon" :label="t(`inventory.stat.${b.key}`)" size="1.1em" />
      <template v-if="!b.icon">{{ t(`inventory.stat.${b.key}`) }}</template>
      {{ b.text }}
    </span>
  </span>
</template>

<style scoped>
.stats {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.15rem 0.6rem;
  font-size: 0.8rem;
}
.dice,
.bonus {
  display: inline-flex;
  align-items: center;
  gap: 0.15rem;
}
</style>
