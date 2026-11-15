<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import type { Combatant } from '@/features/catalog'
import type { EncounterCommand, EncounterState } from '@/features/games'
import { useBoardPieces } from '@/features/table'
import { usePreferences } from '@/shared/lib/preferences'
import { useDice } from '../api/queries'
import { distance, fighters, type AdventurerSheet, type Fighter } from '../lib/fighters'
import { useAdventurerSheets } from '../lib/sheets'
import AttackArena from './AttackArena.vue'
import FighterHead from './FighterHead.vue'
import SpellArena from './SpellArena.vue'

/**
 * The duel, in its own window over the game: who attacks whom, the odds if the table wants them,
 * the dice tapped as they landed, and the Damage applied in one click. Nothing else matters while
 * it is open; closing it goes back to the table as it was.
 */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{
  game: string
  encounter: EncounterState
  cards: Map<string, Combatant>
  /** The figure the duel opened with (combatant instance or "adventurer:<name>"). */
  from: string | null
  /** The target the AI picked, if any. */
  to?: string | null
  /** The AI casts a Spell: open on the Spell side. */
  spell?: boolean
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language, showOdds } = storeToRefs(usePreferences())
const dice = useDice()
const pieces = useBoardPieces()
const { sheets, remember } = useAdventurerSheets(() => props.game)

const all = computed(() => fighters(props.encounter, props.cards, sheets.value, language.value, pieces.data.value))
const attackerId = ref<string | undefined>(props.from ?? undefined)
const targetId = ref<string>()
const attacker = computed(() => all.value.find((f) => f.id === attackerId.value))
const target = computed(() => all.value.find((f) => f.id === targetId.value))

/** Opponents first, nearest first, then the others (an ally can be targeted too). */
function targetsOf(from: Fighter | undefined) {
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
}
const targets = computed(() => targetsOf(attacker.value))
const attackers = computed(() => all.value.map((f) => ({ id: f.id, label: f.name })))

// Opening on a figure: it attacks its nearest opponent. A new attacker does the same.
watch(
  () => [open.value, props.from] as const,
  ([isOpen, from]) => {
    if (isOpen && from) attackerId.value = from
  },
  { immediate: true },
)
watch(
  () => [open.value, props.to] as const,
  ([isOpen, to]) => {
    if (isOpen && to && all.value.some((f) => f.id === to)) targetId.value = to
  },
  { immediate: true, flush: 'post' },
)
watch(
  [attackerId, () => all.value.length > 0],
  () => {
    if (!targets.value.some((o) => o.id === targetId.value) || attacker.value?.kind === target.value?.kind)
      targetId.value = targets.value[0]?.id
  },
  { immediate: true },
)

function swap() {
  const [a, b] = [targetId.value, attackerId.value]
  attackerId.value = a
  targetId.value = b
}

const mode = ref<'attack' | 'spell'>('attack')
watch(open, (isOpen) => isOpen && (mode.value = props.spell ? 'spell' : 'attack'), { immediate: true })
const modes = computed(() => [
  { value: 'attack', label: t('duel.attack'), icon: 'pi pi-bolt' },
  { value: 'spell', label: t('duel.spell'), icon: 'pi pi-sparkles' },
])

function apply(instance: string, amount: number) {
  emit('command', { type: 'damage', instance, amount })
}

function keep(fighter: Fighter, patch: Partial<AdventurerSheet>) {
  if (fighter.kind === 'adventurer') remember(fighter.name, patch)
}
</script>

<template>
  <Dialog
    v-model:visible="open"
    modal
    dismissable-mask
    :draggable="false"
    :breakpoints="{ '1000px': '100vw' }"
    class="duel-dialog"
    :pt="{ content: { class: 'duel-content' } }"
  >
    <template #header>
      <div class="bar">
        <h2>{{ t('duel.title') }}</h2>
        <SelectButton
          v-model="mode"
          :options="modes"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          :aria-label="t('duel.kind')"
        >
          <template #option="{ option }">
            <i :class="option.icon" />
            <span>{{ option.label }}</span>
          </template>
        </SelectButton>
        <label class="odds-switch">
          <ToggleSwitch v-model="showOdds" />
          {{ t('duel.showOdds') }}
        </label>
      </div>
    </template>

    <Message v-if="dice.isError.value" severity="warn" size="small">{{ t('duel.noDice') }}</Message>
    <component
      :is="mode === 'attack' ? AttackArena : SpellArena"
      v-if="attacker && target"
      :key="`${mode}|${attacker.id}|${target.id}`"
      :attacker="attacker"
      :target="target"
      :dice="dice.data.value"
      :show-odds="showOdds"
      :locked="locked"
      @apply="apply"
      @remember="keep"
    >
      <template #attacker>
        <FighterHead
          v-model="attackerId"
          :fighter="attacker"
          :options="attackers"
          :role="mode === 'attack' ? t('duel.attacker') : t('duel.caster')"
          side="left"
        />
      </template>
      <template #target>
        <FighterHead v-model="targetId" :fighter="target" :options="targets" :role="t('duel.target')" side="right" />
        <Button
          class="swap"
          icon="pi pi-arrow-right-arrow-left"
          :label="t('duel.swap')"
          size="small"
          text
          @click="swap"
        />
      </template>
    </component>
    <p v-else class="empty">{{ t('duel.noTarget') }}</p>
  </Dialog>
</template>

<style scoped>
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.6rem 1.2rem;
  flex: 1;
}
h2 {
  margin: 0;
  font-size: 1.25rem;
}
.odds-switch {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.9rem;
  margin: 0 0.75rem 0 auto;
}
.swap {
  align-self: flex-end;
}
.empty {
  color: var(--p-text-muted-color);
}
</style>

<style>
.duel-dialog {
  width: min(84rem, 96vw);
  height: min(54rem, 94vh);
}
.duel-content {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
@media (max-width: 1000px) {
  .duel-dialog {
    height: 100vh;
    max-height: 100vh;
    border-radius: 0;
  }
  .duel-content {
    overflow-y: auto;
  }
}
</style>
