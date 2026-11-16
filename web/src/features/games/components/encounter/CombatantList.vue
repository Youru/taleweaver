<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Tag from 'primevue/tag'
import type { Combatant } from '@/features/catalog'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CombatantState, EncounterState } from '../../api/types'

/** The combatants in play, compact. A row selects the figure: its actions open under the list. */
const props = defineProps<{
  encounter: EncounterState
  cards: Map<string, Combatant>
  /** Selected figure id (combatant instance). */
  selected: string | undefined
  /** Figures the table shows (not under the fog); when given, the others are left out. */
  shown?: Set<string>
}>()
const emit = defineEmits<{ select: [instance: string] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const acting = computed(() => props.encounter.activation?.figures[0])
/**
 * Only the combatants already on the table: one still to place, or under the fog, would tell what is
 * coming. The acting figure is always listed.
 */
const alive = computed(() =>
  props.encounter.combatants.filter(
    (c) =>
      !c.defeated && (c.instance === acting.value || (!!c.position && (!props.shown || props.shown.has(c.instance)))),
  ),
)
const defeated = computed(() => props.encounter.combatants.filter((c) => c.defeated))

function health(figure: CombatantState): number | undefined {
  const value = props.cards.get(figure.card)?.stats?.health
  return typeof value === 'number' ? value : undefined
}

function name(figure: CombatantState): string {
  const card = props.cards.get(figure.card)
  return card ? pick(card.name, language.value) : ''
}

function share(figure: CombatantState): number {
  const max = health(figure)
  return max ? Math.min(100, (figure.damage / max) * 100) : 0
}
</script>

<template>
  <div class="list">
    <p v-if="!alive.length" class="empty">{{ t('play.noCombatant') }}</p>
    <ul>
      <li v-for="figure in alive" :key="figure.instance">
        <button
          type="button"
          class="row"
          :class="{ acting: figure.instance === acting, selected: figure.instance === selected }"
          :aria-pressed="figure.instance === selected"
          @click="emit('select', figure.instance)"
        >
          <span class="number">{{ figure.number || '·' }}</span>
          <span class="who">
            <strong>{{ figure.instance }}</strong>
            <small>{{ name(figure) }}</small>
          </span>
          <span class="tags">
            <Tag v-if="figure.aggro" :value="t('play.aggro')" severity="danger" />
            <Tag v-for="effect in figure.effects" :key="effect" :value="effect" severity="secondary" />
            <Tag v-if="!figure.position" :value="t('play.notPlaced')" severity="warn" />
          </span>
          <span class="hp">
            <span class="hp-text">{{ figure.damage }} / {{ health(figure) ?? '?' }} {{ t('play.health') }}</span>
            <span class="hp-bar" aria-hidden="true"><span :style="{ width: `${share(figure)}%` }" /></span>
          </span>
        </button>
      </li>
    </ul>
    <p v-if="defeated.length" class="defeated">
      {{ t('play.defeatedCombatants') }} : {{ defeated.map((c) => c.instance).join(', ') }}
    </p>
  </div>
</template>

<style scoped>
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.row {
  display: grid;
  grid-template-columns: 1.8rem minmax(0, 1fr) auto;
  grid-template-areas: 'number who hp' 'number tags tags';
  align-items: center;
  gap: 0.1rem 0.5rem;
  width: 100%;
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}
.row:hover {
  border-color: var(--p-primary-color);
}
.row.acting {
  border-color: var(--p-orange-500);
  box-shadow: 0 0 0 1px var(--p-orange-500);
}
.row.selected {
  border-color: var(--p-yellow-400);
  box-shadow: 0 0 0 2px var(--p-yellow-400);
}
.number {
  grid-area: number;
  display: grid;
  place-items: center;
  width: 1.6rem;
  height: 1.6rem;
  border-radius: 50%;
  background: var(--p-red-600);
  color: #fff;
  font-weight: 700;
  font-size: 0.85rem;
}
.who {
  grid-area: who;
  display: flex;
  gap: 0.4rem;
  align-items: baseline;
  min-width: 0;
}
.who small {
  color: var(--p-text-muted-color);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.tags {
  grid-area: tags;
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
.tags:empty {
  display: none;
}
.hp {
  grid-area: hp;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.15rem;
  font-variant-numeric: tabular-nums;
  font-size: 0.85rem;
}
.hp-bar {
  width: 5rem;
  height: 0.3rem;
  border-radius: 1rem;
  background: var(--p-content-border-color);
  overflow: hidden;
}
.hp-bar span {
  display: block;
  height: 100%;
  background: var(--p-red-500);
}
.empty,
.defeated {
  margin: 0.4rem 0 0;
  color: var(--p-text-muted-color);
  font-size: 0.9rem;
}
</style>
