<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Image from 'primevue/image'
import Select from 'primevue/select'
import type { Fighter } from '../lib/fighters'
import { statIcon } from '../lib/icons'
import GameIcon from './GameIcon.vue'

/** Who stands on one side of the duel: portrait, choice of figure, card and health. */
const id = defineModel<string | undefined>({ required: true })
const props = defineProps<{
  fighter: Fighter | undefined
  options: { id: string; label: string }[]
  /** "Attaquant", "Cible"… */
  role: string
  side: 'left' | 'right'
}>()

const { t } = useI18n()
const remaining = computed(() =>
  props.fighter?.health === undefined ? undefined : Math.max(0, props.fighter.health - props.fighter.damage),
)
</script>

<template>
  <header class="head" :class="side">
    <div class="portrait" :class="fighter?.kind">
      <img v-if="fighter?.portrait" :src="fighter.portrait" alt="" draggable="false" />
      <span v-else class="initial">{{ fighter?.name[0] ?? '?' }}</span>
    </div>
    <div class="who">
      <span class="role">{{ role }}</span>
      <Select
        v-model="id"
        :options="options"
        option-label="label"
        option-value="id"
        size="small"
        :aria-label="role"
        :placeholder="t('duel.noTarget')"
        class="choice"
      />
      <span v-if="remaining !== undefined && fighter?.health" class="hp">
        <GameIcon :src="statIcon('hp')" size="1.2em" />
        {{ t('duel.remaining', { n: remaining, max: fighter.health }) }}
        <span class="bar"><span :style="{ width: `${(remaining / fighter.health) * 100}%` }" /></span>
      </span>
      <Image
        v-if="fighter?.card"
        :src="fighter.card"
        :alt="t('duel.card', { name: fighter.name })"
        preview
        class="card"
        image-class="card-thumb"
      />
    </div>
  </header>
</template>

<style scoped>
.head {
  display: flex;
  gap: 0.7rem;
  align-items: flex-start;
}
.head.right {
  flex-direction: row-reverse;
  text-align: right;
}
.portrait {
  flex: none;
  width: 4.5rem;
  height: 5.5rem;
  border-radius: 0.6rem;
  overflow: hidden;
  display: grid;
  place-items: end center;
  background: radial-gradient(circle at 50% 70%, var(--p-red-900), transparent 75%);
}
.portrait.adventurer {
  background: radial-gradient(circle at 50% 70%, var(--p-blue-900), transparent 75%);
}
.portrait img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}
.initial {
  align-self: center;
  font-size: 2rem;
  font-weight: 700;
}
.who {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
  flex: 1;
}
.right .who {
  align-items: flex-end;
}
.role {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--p-text-muted-color);
}
.choice {
  width: 100%;
  max-width: 16rem;
}
.hp {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.85rem;
  font-variant-numeric: tabular-nums;
}
.bar {
  width: 4rem;
  height: 0.35rem;
  border-radius: 1rem;
  background: var(--p-content-border-color);
  overflow: hidden;
}
.bar span {
  display: block;
  height: 100%;
  background: var(--p-green-500);
}
.card :deep(.card-thumb) {
  width: 3.2rem;
  border-radius: 0.25rem;
  cursor: zoom-in;
}
</style>
