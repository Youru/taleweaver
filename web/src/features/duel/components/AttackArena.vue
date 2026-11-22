<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import { usePreferences } from '@/shared/lib/preferences'
import type { Dice } from '../api/types'
import type { SymbolAbility } from '../lib/abilities'
import { attackOdds, hasBlackDie, resolveAttack, rolledDice, type AttackRoll, type AttackSetup } from '../lib/attack'
import { readFaces } from '../lib/dice'
import { effectMods } from '../lib/effects'
import type { AdventurerSheet, Fighter } from '../lib/fighters'
import { decimal, percent } from '../lib/format'
import { dieIcon, statIcon, symbolIcon, type SymbolName } from '../lib/icons'
import ChipToggle from './ChipToggle.vue'
import DicePool from './DicePool.vue'
import DiePicker from './DiePicker.vue'
import GameIcon from './GameIcon.vue'
import ManualRoll from './ManualRoll.vue'
import OddsStrip from './OddsStrip.vue'
import CardReminders from './CardReminders.vue'
import EffectNotes from './EffectNotes.vue'
import StatField from './StatField.vue'

/**
 * An Attack, laid out like the table: the attacker's pool and situation on the left, the target's
 * Defense and Armor on the right, and in the middle the odds, the dice to tap as they landed, and
 * the Damage (rulebook p. 41).
 */
const props = defineProps<{
  attacker: Fighter
  target: Fighter
  dice: Dice | undefined
  showOdds: boolean
  locked: boolean
}>()
const emit = defineEmits<{
  apply: [instance: string, amount: number]
  /** Targeted without Damage: it counts for Aggro. */
  targeted: [instance: string]
  remember: [fighter: Fighter, patch: Partial<AdventurerSheet>]
  spend: [figure: Fighter, action: string]
}>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

// Before the roll (the dialog re-creates the arena when the attacker or the target changes)
const pool = ref([...props.attacker.pool])
const empower = ref(false)
const flank = ref(false)
const throughAlly = ref(false)
const hindering = ref(false)
const otherToHit = ref(0)
const physical = ref(0)
const pierce = ref(props.attacker.pierce ?? 0)
const defense = ref(props.target.defense)
const armor = ref(props.target.armor ?? 0)
const dodge = ref(false)

watch(pool, (value) => emit('remember', props.attacker, { pool: value }))
watch([defense, armor], ([d, a]) => emit('remember', props.target, { defense: d, armor: a }))

const alwaysEmpowered = computed(() => hasBlackDie({ pool: pool.value }))
/** What the Effects on both figures change (Darkness, Barrier, Courage, Nemesis, Wilt…). */
const mods = computed(() => effectMods(props.attacker.effects, props.target.effects))
watch(
  () => mods.value.noDodge,
  (noDodge) => {
    if (noDodge) dodge.value = false
  },
  { immediate: true },
)
const setup = computed<AttackSetup | undefined>(() =>
  defense.value === null
    ? undefined
    : {
        pool: pool.value,
        empower: empower.value || alwaysEmpowered.value,
        toHit:
          (flank.value ? 1 : 0) -
          (throughAlly.value ? 1 : 0) -
          (hindering.value ? 1 : 0) +
          otherToHit.value +
          mods.value.toHit,
        physical: physical.value + mods.value.physical,
        pierce: pierce.value,
        abilities: props.attacker.abilities,
        defense: defense.value,
        armor: armor.value + mods.value.armor,
        dodge: dodge.value && !mods.value.noDodge,
        irreducible: mods.value.irreducible,
        symbols: mods.value.symbols,
      },
)
const remaining = computed(() =>
  props.target.health === undefined ? undefined : Math.max(0, props.target.health - props.target.damage),
)
const odds = computed(() =>
  props.showOdds && setup.value && props.dice ? attackOdds(setup.value, props.dice, remaining.value) : undefined,
)
const oddsItems = computed(() => {
  const o = odds.value
  if (!o) return []
  return [
    { label: t('duel.oddsHit'), value: percent(o.hit, language.value) },
    { label: t('duel.oddsWound'), value: percent(o.wound, language.value) },
    { label: t('duel.oddsMean'), value: decimal(o.expected, language.value) },
    ...(o.defeat === undefined ? [] : [{ label: t('duel.oddsDefeat'), value: percent(o.defeat, language.value) }]),
  ]
})

// The roll: one face per die, tapped as it landed (or typed, in manual mode)
const rolled = computed(() => rolledDice({ pool: pool.value, empower: empower.value || alwaysEmpowered.value }))
const faces = ref<(number | null)[]>([])
const dodgeFace = ref<number | null>(null)
const manual = ref(false)
const typedRoll = ref<AttackRoll | null>(null)
const applied = ref(false)
watch(rolled, (list) => (faces.value = list.map(() => null)), { immediate: true })

const roll = computed<AttackRoll | undefined>(() => {
  if (manual.value) return typedRoll.value ?? undefined
  if (!props.dice) return undefined
  const read = readFaces(rolled.value, faces.value, props.dice)
  if (!read) return undefined
  const dodged = dodge.value && dodgeFace.value !== null ? props.dice.black?.faces[dodgeFace.value] : undefined
  if (dodge.value && !dodged) return undefined
  return { ...read, dodgeShields: dodged?.shield ?? 0, dodgeSkull: dodged?.skull ?? false }
})
const outcome = computed(() => (setup.value && roll.value ? resolveAttack(setup.value, roll.value) : undefined))
const picked = computed(() => faces.value.filter((f) => f !== null).length + (dodgeFace.value === null ? 0 : 1))
const toPick = computed(() => rolled.value.length + (dodge.value ? 1 : 0))
const sum = computed(() =>
  props.dice
    ? faces.value.reduce<number>(
        (total, f, i) => total + (f === null ? 0 : (props.dice?.[rolled.value[i] ?? '']?.faces[f]?.value ?? 0)),
        0,
      )
    : 0,
)

function dieName(colour: string, index: number): string {
  const name = props.dice?.[colour]?.name
  return t('duel.dieN', { n: index + 1, die: (name && (name[language.value] ?? name.en)) || colour })
}

function newRoll() {
  faces.value = rolled.value.map(() => null)
  dodgeFace.value = null
  typedRoll.value = null
  applied.value = false
  paid.value = false
}

/** Dodge and a paid Empower of this roll, and whether they are counted. */
const paid = ref(false)
const costs = computed(() => [
  ...(empower.value && !alwaysEmpowered.value && props.attacker.kind === 'adventurer'
    ? [{ figure: props.attacker, action: 'empower' }]
    : []),
  ...(dodge.value && !mods.value.noDodge && props.target.kind === 'adventurer'
    ? [{ figure: props.target, action: 'dodge' }]
    : []),
])
function pay() {
  if (paid.value) return
  costs.value.forEach(({ figure, action }) => emit('spend', figure, action))
  paid.value = true
}

function apply() {
  if (!outcome.value) return
  pay()
  emit('apply', props.target.id, outcome.value.final)
  applied.value = true
}

function markTargeted() {
  pay()
  emit('targeted', props.target.id)
  applied.value = true
}

function costIcons(ability: SymbolAbility): SymbolName[] {
  return [
    ...Array<SymbolName>(ability.cost.star).fill('star'),
    ...Array<SymbolName>(ability.cost.shield).fill('shield'),
    ...Array<SymbolName>(ability.cost.book).fill('book'),
  ]
}
</script>

<template>
  <div class="arena">
    <section class="side left" :aria-label="t('duel.attackerSide')">
      <slot name="attacker" />
      <DicePool v-model="pool" :dice="dice" :label="t('duel.pool')" />
      <div class="chips">
        <ChipToggle
          v-model="empower"
          :label="alwaysEmpowered ? t('duel.alwaysEmpowered') : t('duel.empower')"
          :icon="dieIcon('black')"
          :disabled="alwaysEmpowered"
        />
      </div>
      <h4>{{ t('duel.situation') }}</h4>
      <div class="chips">
        <ChipToggle v-model="flank" :label="t('duel.flank')" />
        <ChipToggle v-model="throughAlly" :label="t('duel.throughAlly')" />
        <ChipToggle v-model="hindering" :label="t('duel.hindering')" />
      </div>
      <StatField v-model="otherToHit" :label="t('duel.otherToHit')" :min="-10" :max="10" />
      <StatField v-model="physical" :label="t('duel.physical')" :max="20" />
      <StatField v-model="pierce" :label="t('duel.pierce')" :max="10" />
      <template v-if="attacker.abilities.length">
        <h4>{{ t('duel.symbols') }}</h4>
        <ul class="abilities">
          <li v-for="(ability, index) in attacker.abilities" :key="index" :class="{ counted: ability.boost }">
            <span class="cost">
              <GameIcon v-for="(s, i) in costIcons(ability)" :key="i" :src="symbolIcon(s)" size="1.05em" />
            </span>
            <span>{{ ability.text }}</span>
          </li>
        </ul>
      </template>
      <CardReminders :fighter="attacker" />
    </section>

    <section class="center" :aria-label="t('duel.roll')">
      <EffectNotes :notes="mods.notes" />
      <OddsStrip
        v-if="showOdds"
        :items="oddsItems"
        :note="odds ? t('duel.needed', { n: odds.needed }) : undefined"
        :missing="defense === null ? t('duel.needDefense') : !odds ? t('duel.unknownDice') : undefined"
      />

      <div class="tray-head">
        <h4>{{ manual ? t('duel.manualTitle') : t('duel.trayTitle') }}</h4>
        <span v-if="!manual" class="progress">{{ t('duel.picked', { n: picked, of: toPick, sum }) }}</span>
        <Button
          :label="manual ? t('duel.byFaces') : t('duel.manual')"
          size="small"
          text
          @click="((manual = !manual), newRoll())"
        />
      </div>
      <ManualRoll v-if="manual" v-model="typedRoll" :empowered="empower || alwaysEmpowered" :dodge="dodge" />
      <div v-else class="tray">
        <DiePicker
          v-for="(colour, index) in rolled"
          :key="`${index}-${colour}`"
          :model-value="faces[index] ?? null"
          :die="dice?.[colour]"
          :colour="colour"
          :label="dieName(colour, index)"
          @update:model-value="(face) => (faces[index] = face)"
        />
        <DiePicker
          v-if="dodge"
          v-model="dodgeFace"
          :die="dice?.black"
          colour="black"
          :label="t('duel.dodgeOf', { name: target.name })"
        />
      </div>

      <section v-if="outcome" class="outcome" :class="outcome.hit ? 'hit' : 'miss'" aria-live="polite">
        <p class="verdict">
          <template v-if="outcome.hit">
            <strong class="big">{{ outcome.final }}</strong>
            {{ t('duel.damageDealt', outcome.final) }}
          </template>
          <strong v-else class="big">{{ t('duel.missed') }}</strong>
        </p>
        <ol class="steps" :aria-label="t('duel.outcome')">
          <li v-if="outcome.skull">{{ t('duel.stepSkull') }}</li>
          <li v-else-if="!outcome.hit">
            {{ t('duel.stepMiss', { attack: outcome.attack, defense: outcome.defense }) }}
          </li>
          <template v-else>
            <li>
              {{
                t('duel.stepHit', { attack: outcome.attack, defense: outcome.defense, difference: outcome.difference })
              }}
            </li>
            <li v-for="(spent, index) in outcome.spent" :key="index">
              <span class="cost">
                <GameIcon v-for="(s, i) in costIcons(spent.ability)" :key="i" :src="symbolIcon(s)" size="1em" /> </span
              >{{ t('duel.stepSpend', { times: spent.times, text: spent.ability.text }) }}
            </li>
            <li>{{ t('duel.stepArmor', { physical: outcome.physical, armor: outcome.armor }) }}</li>
            <li v-if="outcome.magic">{{ t('duel.stepMagic', { magic: outcome.magic }) }}</li>
            <li v-if="outcome.irreducible">{{ t('duel.stepIrreducible', { n: outcome.irreducible }) }}</li>
          </template>
          <li v-if="!outcome.hit || outcome.final === 0" class="note">{{ t('duel.counter') }}</li>
          <li v-if="outcome.hit" class="note">{{ t('duel.difference', { n: outcome.difference }) }}</li>
        </ol>
        <div class="actions">
          <template v-if="target.kind === 'combatant' && (outcome.final > 0 || attacker.kind !== 'combatant')">
            <Button
              v-if="!applied && outcome.final === 0"
              :label="t('duel.targeted')"
              icon="pi pi-check"
              :disabled="locked"
              @click="markTargeted"
            />
            <Button
              v-else-if="!applied"
              :label="t('duel.apply', { n: outcome.final, name: target.id })"
              icon="pi pi-check"
              :disabled="locked"
              @click="apply"
            />
            <span v-else class="done"
              ><i class="pi pi-check" /> {{ t(outcome.final > 0 ? 'duel.applied' : 'duel.noted') }}</span
            >
          </template>
          <span v-else-if="outcome.final > 0">{{
            t('duel.adventurerTakes', { n: outcome.final, name: target.name })
          }}</span>
          <Button :label="t('duel.newRoll')" icon="pi pi-replay" severity="secondary" outlined @click="newRoll" />
        </div>
      </section>
      <p v-else class="waiting">{{ t('duel.waiting') }}</p>
    </section>

    <section class="side right" :aria-label="t('duel.targetSide')">
      <slot name="target" />
      <StatField v-model="defense" :label="t('duel.defense')" :icon="statIcon('defense')" :max="40" nullable />
      <StatField v-model="armor" :label="t('duel.armor')" :icon="statIcon('armor')" :max="20" />
      <div class="chips">
        <ChipToggle v-if="!mods.noDodge" v-model="dodge" :label="t('duel.dodge')" :icon="dieIcon('black')" />
      </div>
      <CardReminders :fighter="target" />
    </section>
  </div>
</template>

<style scoped src="./arena.css"></style>
