<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import InputNumber from 'primevue/inputnumber'
import { usePreferences } from '@/shared/lib/preferences'
import type { Dice } from '../api/types'
import { attackOdds, hasBlackDie, resolveAttack, type AttackSetup } from '../lib/attack'
import type { AdventurerSheet, Fighter } from '../lib/fighters'
import { decimal, percent, typed } from '../lib/format'
import DicePool from './DicePool.vue'
import OddsLine from './OddsLine.vue'

/**
 * An Attack, step by step (rulebook p. 41): the pool and what changes it, the odds before rolling,
 * then the roll as the table reads it, and the Damage it deals.
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
  remember: [fighter: Fighter, patch: Partial<AdventurerSheet>]
}>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

// Before the roll (the panel re-creates this form when the attacker or the target changes)
const pool = ref([...props.attacker.pool])
const empower = ref(false)
const flank = ref(false)
const throughAlly = ref(false)
const hindering = ref(false)
const otherToHit = ref(0)
const physical = ref(0)
const pierce = ref(0)
const defense = ref(props.target.defense)
const armor = ref(props.target.armor ?? 0)
const dodge = ref(false)

watch(pool, (value) => emit('remember', props.attacker, { pool: value }))
watch([defense, armor], ([d, a]) => emit('remember', props.target, { defense: d, armor: a }))

const alwaysEmpowered = computed(() => hasBlackDie({ pool: pool.value }))
const setup = computed<AttackSetup | undefined>(() =>
  defense.value === null
    ? undefined
    : {
        pool: pool.value,
        empower: empower.value || alwaysEmpowered.value,
        toHit: (flank.value ? 1 : 0) - (throughAlly.value ? 1 : 0) - (hindering.value ? 1 : 0) + otherToHit.value,
        physical: physical.value,
        pierce: pierce.value,
        abilities: props.attacker.abilities,
        defense: defense.value,
        armor: armor.value,
        dodge: dodge.value,
      },
)
const remaining = computed(() =>
  props.target.health === undefined ? undefined : Math.max(0, props.target.health - props.target.damage),
)
const odds = computed(() =>
  props.showOdds && setup.value && props.dice ? attackOdds(setup.value, props.dice, remaining.value) : undefined,
)

// The roll
const total = ref<number | null>(null)
const shield = ref(0)
const book = ref(0)
const star = ref(0)
const skull = ref(false)
const dodgeShields = ref(0)
const dodgeSkull = ref(false)

const outcome = computed(() =>
  setup.value && total.value !== null
    ? resolveAttack(setup.value, {
        total: total.value,
        shield: shield.value,
        book: book.value,
        star: star.value,
        skull: skull.value,
        dodgeShields: dodgeShields.value,
        dodgeSkull: dodgeSkull.value,
      })
    : undefined,
)

function clearRoll() {
  total.value = null
  shield.value = book.value = star.value = dodgeShields.value = 0
  skull.value = dodgeSkull.value = false
}

function apply() {
  if (outcome.value) emit('apply', props.target.id, outcome.value.final)
  clearRoll()
}
</script>

<template>
  <div class="attack">
    <section :aria-label="t('duel.before')">
      <DicePool v-model="pool" :dice="dice" :label="t('duel.pool')" />
      <label class="check">
        <Checkbox v-model="empower" binary :disabled="alwaysEmpowered" />
        {{ alwaysEmpowered ? t('duel.alwaysEmpowered') : t('duel.empower') }}
      </label>
      <div class="grid">
        <label class="check"><Checkbox v-model="flank" binary /> {{ t('duel.flank') }}</label>
        <label class="check"><Checkbox v-model="throughAlly" binary /> {{ t('duel.throughAlly') }}</label>
        <label class="check"><Checkbox v-model="hindering" binary /> {{ t('duel.hindering') }}</label>
        <label class="check"><Checkbox v-model="dodge" binary /> {{ t('duel.dodge') }}</label>
      </div>
      <div class="numbers">
        <label>
          {{ t('duel.otherToHit') }}
          <InputNumber
            v-model="otherToHit"
            :aria-label="t('duel.otherToHit')"
            :min="-20"
            :max="20"
            show-buttons
            size="small"
          />
        </label>
        <label>
          {{ t('duel.physical') }}
          <InputNumber
            v-model="physical"
            :aria-label="t('duel.physical')"
            :min="0"
            :max="50"
            show-buttons
            size="small"
          />
        </label>
        <label>
          {{ t('duel.pierce') }}
          <InputNumber v-model="pierce" :aria-label="t('duel.pierce')" :min="0" :max="20" show-buttons size="small" />
        </label>
        <label>
          {{ t('duel.defense') }}
          <InputNumber v-model="defense" :aria-label="t('duel.defense')" :min="0" :max="40" show-buttons size="small" />
        </label>
        <label>
          {{ t('duel.armor') }}
          <InputNumber v-model="armor" :aria-label="t('duel.armor')" :min="0" :max="20" show-buttons size="small" />
        </label>
      </div>
      <p v-if="defense === null" class="hint">{{ t('duel.needDefense') }}</p>
      <OddsLine v-else-if="showOdds" :missing="!odds">
        <template v-if="odds">
          <strong>{{ t('duel.hitOdds', { p: percent(odds.hit, language) }) }}</strong>
          <span>{{ t('duel.woundOdds', { p: percent(odds.wound, language) }) }}</span>
          <span>{{ t('duel.expected', { n: decimal(odds.expected, language) }) }}</span>
          <span v-if="odds.defeat !== undefined">{{
            t('duel.defeatOdds', { p: percent(odds.defeat, language) })
          }}</span>
          <small>{{ t('duel.needed', { n: odds.needed }) }}</small>
        </template>
      </OddsLine>
    </section>

    <section class="roll" :aria-label="t('duel.roll')">
      <h4>{{ t('duel.roll') }}</h4>
      <div class="numbers">
        <label>
          {{ t('duel.total') }}
          <InputNumber
            v-model="total"
            :aria-label="t('duel.total')"
            :min="0"
            :max="80"
            size="small"
            @input="(e) => (total = typed(e.value))"
          />
        </label>
        <label>
          <span><i class="pi pi-shield" /> {{ t('duel.shields') }}</span>
          <InputNumber
            v-model="shield"
            :aria-label="t('duel.shields')"
            :min="0"
            :max="30"
            show-buttons
            size="small"
            @input="(e) => (shield = typed(e.value) ?? 0)"
          />
        </label>
        <label>
          <span><i class="pi pi-book" /> {{ t('duel.books') }}</span>
          <InputNumber
            v-model="book"
            :aria-label="t('duel.books')"
            :min="0"
            :max="30"
            show-buttons
            size="small"
            @input="(e) => (book = typed(e.value) ?? 0)"
          />
        </label>
        <label>
          <span><i class="pi pi-star" /> {{ t('duel.stars') }}</span>
          <InputNumber
            v-model="star"
            :aria-label="t('duel.stars')"
            :min="0"
            :max="30"
            show-buttons
            size="small"
            @input="(e) => (star = typed(e.value) ?? 0)"
          />
        </label>
        <label v-if="dodge">
          {{ t('duel.dodgeShields') }}
          <InputNumber
            v-model="dodgeShields"
            :aria-label="t('duel.dodgeShields')"
            :min="0"
            :max="4"
            show-buttons
            size="small"
            @input="(e) => (dodgeShields = typed(e.value) ?? 0)"
          />
        </label>
      </div>
      <label v-if="empower || alwaysEmpowered" class="check">
        <Checkbox v-model="skull" binary /> {{ t('duel.skull') }}
      </label>
      <label v-if="dodge" class="check"><Checkbox v-model="dodgeSkull" binary /> {{ t('duel.dodgeSkull') }}</label>

      <ol v-if="outcome" class="steps" :aria-label="t('duel.outcome')">
        <li v-if="outcome.skull">{{ t('duel.stepSkull') }}</li>
        <li v-else-if="!outcome.hit">{{ t('duel.stepMiss', { attack: outcome.attack, defense: outcome.defense }) }}</li>
        <template v-else>
          <li>
            {{
              t('duel.stepHit', { attack: outcome.attack, defense: outcome.defense, difference: outcome.difference })
            }}
          </li>
          <li v-for="(spent, index) in outcome.spent" :key="index">
            {{ t('duel.stepSpend', { times: spent.times, text: spent.ability.text }) }}
          </li>
          <li>{{ t('duel.stepArmor', { physical: outcome.physical, armor: outcome.armor }) }}</li>
          <li v-if="outcome.magic">{{ t('duel.stepMagic', { magic: outcome.magic }) }}</li>
          <li class="final">{{ t('duel.stepFinal', { final: outcome.final }) }}</li>
        </template>
        <li v-if="!outcome.hit || outcome.final === 0" class="note">{{ t('duel.counter') }}</li>
        <li v-if="outcome.hit" class="note">{{ t('duel.difference', { n: outcome.difference }) }}</li>
      </ol>
      <div v-if="outcome" class="apply">
        <Button
          v-if="target.kind === 'combatant' && outcome.final > 0"
          :label="t('duel.apply', { n: outcome.final, name: target.id })"
          icon="pi pi-check"
          size="small"
          :disabled="locked"
          @click="apply"
        />
        <span v-else-if="outcome.final > 0">{{
          t('duel.adventurerTakes', { n: outcome.final, name: target.name })
        }}</span>
        <Button :label="t('duel.newRoll')" size="small" severity="secondary" text @click="clearRoll" />
      </div>
    </section>
  </div>
</template>

<style scoped>
.attack,
section {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
h4 {
  margin: 0.4rem 0 0;
  font-size: 0.85rem;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}
.check {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.9rem;
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
  gap: 0.3rem 0.6rem;
}
.numbers {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(7.5rem, 1fr));
  gap: 0.4rem 0.6rem;
}
.numbers label {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.numbers :deep(.p-inputnumber) {
  width: 100%;
}
.numbers :deep(.p-inputnumber-input) {
  width: 100%;
  min-width: 0;
}
.hint {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.steps {
  margin: 0;
  padding-left: 1.2rem;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  font-size: 0.92rem;
}
.final {
  font-weight: 700;
}
.note {
  color: var(--p-text-muted-color);
  list-style: none;
  margin-left: -1.2rem;
  font-size: 0.85rem;
}
.apply {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}
</style>
