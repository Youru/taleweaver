<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import type { Combatant } from '@/features/catalog'
import type { EncounterCommand, EncounterState } from '@/features/games'
import { usePreferences } from '@/shared/lib/preferences'
import { useDice } from '../api/queries'
import { distance, fighters, type AdventurerSheet, type Fighter } from '../lib/fighters'
import { useAdventurerSheets } from '../lib/sheets'
import AttackForm from './AttackForm.vue'
import SpellForm from './SpellForm.vue'

/**
 * A duel worked out with the table's dice: who attacks whom, the odds if the table wants them,
 * then the roll and the Damage, applied to the combatant in one click.
 */
const props = defineProps<{
  game: string
  encounter: EncounterState
  cards: Map<string, Combatant>
  /** The figure the duel opened with (combatant instance or "adventurer:<name>"). */
  from: string
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand]; close: [] }>()

const { t } = useI18n()
const { language, showOdds } = storeToRefs(usePreferences())
const dice = useDice()
const { sheets, remember } = useAdventurerSheets(() => props.game)

const all = computed(() => fighters(props.encounter, props.cards, sheets.value, language.value))
const attackerId = ref(props.from)
watch(
  () => props.from,
  (value) => (attackerId.value = value),
)
const attacker = computed(() => all.value.find((f) => f.id === attackerId.value))

/** Opponents first, nearest first, then the others (an ally can be targeted too). */
const targets = computed(() => {
  const from = attacker.value
  if (!from) return []
  return all.value
    .filter((f) => f.id !== from.id)
    .map((f) => ({ fighter: f, range: distance(from.position, f.position), opponent: f.kind !== from.kind }))
    .sort(
      (a, b) =>
        Number(b.opponent) - Number(a.opponent) ||
        (a.range ?? Infinity) - (b.range ?? Infinity) ||
        a.fighter.name.localeCompare(b.fighter.name),
    )
    .map(({ fighter, range }) => ({
      id: fighter.id,
      label: range === undefined ? fighter.name : t('duel.atRange', { name: fighter.name, range }),
    }))
})
const targetId = ref<string>()
watch(
  targets,
  (list) => {
    if (!list.some((o) => o.id === targetId.value)) targetId.value = list[0]?.id
  },
  { immediate: true },
)
// A new attacker faces its nearest opponent.
watch(attackerId, () => (targetId.value = targets.value[0]?.id))
const target = computed(() => all.value.find((f) => f.id === targetId.value))

const mode = ref<'attack' | 'spell'>('attack')
const modes = computed(() => [
  { value: 'attack', label: t('duel.attack') },
  { value: 'spell', label: t('duel.spell') },
])
const attackers = computed(() => all.value.map((f) => ({ id: f.id, label: f.name })))

function apply(instance: string, amount: number) {
  emit('command', { type: 'damage', instance, amount })
}

function keep(fighter: Fighter, patch: Partial<AdventurerSheet>) {
  if (fighter.kind === 'adventurer') remember(fighter.name, patch)
}

const reminders = computed(() => [attacker.value, target.value].filter((f): f is Fighter => !!f?.reminders.length))
</script>

<template>
  <section class="duel" :aria-label="t('duel.title')">
    <header>
      <h3>{{ t('duel.title') }}</h3>
      <label class="odds-switch">
        <ToggleSwitch v-model="showOdds" />
        {{ t('duel.showOdds') }}
      </label>
      <Button icon="pi pi-times" text rounded size="small" :aria-label="t('duel.close')" @click="emit('close')" />
    </header>

    <SelectButton v-model="mode" :options="modes" option-label="label" option-value="value" :allow-empty="false" />
    <div class="who">
      <label>
        {{ mode === 'attack' ? t('duel.attacker') : t('duel.caster') }}
        <Select v-model="attackerId" :options="attackers" option-label="label" option-value="id" size="small" />
      </label>
      <label>
        {{ t('duel.target') }}
        <Select
          v-model="targetId"
          :options="targets"
          option-label="label"
          option-value="id"
          size="small"
          :placeholder="t('duel.noTarget')"
        />
      </label>
    </div>

    <Message v-if="dice.isError.value" severity="warn" size="small">{{ t('duel.noDice') }}</Message>
    <template v-if="attacker && target">
      <AttackForm
        v-if="mode === 'attack'"
        :key="`attack|${attacker.id}|${target.id}`"
        :attacker="attacker"
        :target="target"
        :dice="dice.data.value"
        :show-odds="showOdds"
        :locked="locked"
        @apply="apply"
        @remember="keep"
      />
      <SpellForm
        v-else
        :key="`spell|${attacker.id}|${target.id}`"
        :attacker="attacker"
        :target="target"
        :dice="dice.data.value"
        :show-odds="showOdds"
        :locked="locked"
        @apply="apply"
        @remember="keep"
      />
    </template>

    <details v-if="reminders.length" class="reminders">
      <summary>{{ t('duel.reminders') }}</summary>
      <template v-for="fighter in reminders" :key="fighter.id">
        <h4>{{ fighter.name }}</h4>
        <dl>
          <template v-for="(reminder, index) in fighter.reminders" :key="index">
            <dt>{{ reminder.name }}</dt>
            <dd>{{ reminder.text }}</dd>
          </template>
        </dl>
      </template>
    </details>
  </section>
</template>

<style scoped>
.duel {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
header {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}
h3 {
  margin: 0;
  flex: 1;
  font-size: 1.05rem;
}
.odds-switch {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.85rem;
}
.who {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}
.who label {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
  min-width: 0;
}
.reminders {
  font-size: 0.85rem;
}
.reminders summary {
  cursor: pointer;
  color: var(--p-text-muted-color);
}
.reminders h4 {
  margin: 0.5rem 0 0.2rem;
  font-size: 0.85rem;
}
.reminders dl {
  margin: 0;
}
.reminders dt {
  font-weight: 600;
}
.reminders dd {
  margin: 0 0 0.3rem;
  color: var(--p-text-muted-color);
}
</style>
