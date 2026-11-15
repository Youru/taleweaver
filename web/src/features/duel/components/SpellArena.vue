<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import { usePreferences } from '@/shared/lib/preferences'
import type { Dice } from '../api/types'
import { readFaces } from '../lib/dice'
import type { AdventurerSheet, Fighter } from '../lib/fighters'
import { decimal, percent } from '../lib/format'
import { dieIcon } from '../lib/icons'
import { resolveSpell, spellOdds, type SpellSetup } from '../lib/spell'
import ChipToggle from './ChipToggle.vue'
import DicePool from './DicePool.vue'
import DiePicker from './DiePicker.vue'
import OddsStrip from './OddsStrip.vue'
import CardReminders from './CardReminders.vue'
import StatField from './StatField.vue'

/**
 * A Spell (rulebook p. 53) or a Spell-Like Ability (FORCE X): the caster's Force on the left, the
 * target's Conviction dice on the right, the dice to tap in the middle.
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

const spellLike = ref(!props.attacker.casting)
const casting = ref(props.attacker.casting ? [props.attacker.casting] : [])
const force = ref(6)
const empower = ref(false)
const conviction = ref([...props.target.conviction])

watch(casting, ([die]) => die && emit('remember', props.attacker, { casting: die }))
watch(conviction, (value) => emit('remember', props.target, { conviction: value }))
watch(spellLike, (value) => value && (empower.value = false))

const setup = computed<SpellSetup>(() => ({
  casting: spellLike.value ? null : (casting.value[0] ?? null),
  force: force.value,
  empower: empower.value,
  conviction: conviction.value,
}))
const odds = computed(() => (props.showOdds && props.dice ? spellOdds(setup.value, props.dice) : undefined))
const oddsItems = computed(() => {
  const o = odds.value
  if (!o) return []
  return [
    { label: t('duel.oddsAffected'), value: percent(o.affected, language.value) },
    { label: t('duel.oddsForce'), value: decimal(o.force, language.value) },
    ...(o.backfire ? [{ label: t('duel.oddsBackfire'), value: percent(o.backfire, language.value) }] : []),
  ]
})

// The roll: the caster's dice, then the target's Conviction dice
const castDice = computed(() => [
  ...(setup.value.casting ? [setup.value.casting] : []),
  ...(setup.value.empower ? ['black'] : []),
])
const castFaces = ref<(number | null)[]>([])
const checkFaces = ref<(number | null)[]>([])
const damage = ref(0)
const applied = ref(false)
watch(castDice, (list) => (castFaces.value = list.map(() => null)), { immediate: true })
watch(conviction, (list) => (checkFaces.value = list.map(() => null)), { immediate: true })

const outcome = computed(() => {
  if (!props.dice) return undefined
  const cast = readFaces(castDice.value, castFaces.value, props.dice)
  const check = readFaces(conviction.value, checkFaces.value, props.dice)
  if (!cast || !check) return undefined
  return {
    ...resolveSpell(setup.value, { casting: cast.total, books: cast.book, skull: cast.skull, conviction: check.total }),
    conviction: check.total,
  }
})

function dieName(colour: string, index: number, whose: string): string {
  const name = props.dice?.[colour]?.name
  return t('duel.dieOf', { n: index + 1, die: (name && (name[language.value] ?? name.en)) || colour, name: whose })
}

function newRoll() {
  castFaces.value = castDice.value.map(() => null)
  checkFaces.value = conviction.value.map(() => null)
  damage.value = 0
  applied.value = false
}

function apply(instance: string, amount: number) {
  emit('apply', instance, amount)
  applied.value = true
}
</script>

<template>
  <div class="arena">
    <section class="side left" :aria-label="t('duel.attackerSide')">
      <slot name="attacker" />
      <div class="chips">
        <ChipToggle v-model="spellLike" :label="t('duel.spellLike')" />
      </div>
      <template v-if="!spellLike">
        <DicePool v-model="casting" :dice="dice" :label="t('duel.castingDie')" :max="1" />
        <div class="chips">
          <ChipToggle v-model="empower" :label="t('duel.empowerSpell')" :icon="dieIcon('black')" />
        </div>
      </template>
      <StatField v-model="force" :label="spellLike ? t('duel.printedForce') : t('duel.spellX')" :max="40" />
      <CardReminders :fighter="attacker" />
    </section>

    <section class="center" :aria-label="t('duel.roll')">
      <OddsStrip v-if="showOdds" :items="oddsItems" :missing="!odds ? t('duel.unknownDice') : undefined" />
      <div class="tray-head">
        <h4>{{ t('duel.trayTitle') }}</h4>
      </div>
      <div class="tray">
        <DiePicker
          v-for="(colour, index) in castDice"
          :key="`cast-${index}-${colour}`"
          :model-value="castFaces[index] ?? null"
          :die="dice?.[colour]"
          :colour="colour"
          :label="dieName(colour, index, attacker.name)"
          @update:model-value="(face) => (castFaces[index] = face)"
        />
        <DiePicker
          v-for="(colour, index) in conviction"
          :key="`check-${index}-${colour}`"
          :model-value="checkFaces[index] ?? null"
          :die="dice?.[colour]"
          :colour="colour"
          :label="dieName(colour, index, target.name)"
          @update:model-value="(face) => (checkFaces[index] = face)"
        />
      </div>

      <section v-if="outcome" class="outcome" :class="outcome.affected ? 'hit' : 'miss'" aria-live="polite">
        <p class="verdict">
          <strong class="big">{{
            outcome.backfire ? t('duel.backfireShort') : outcome.affected ? t('duel.affected') : t('duel.resisted')
          }}</strong>
        </p>
        <p v-if="outcome.backfire" class="detail">{{ t('duel.backfire') }}</p>
        <p v-else class="detail">
          {{ t('duel.forceAgainst', { force: outcome.force, conviction: outcome.conviction }) }}
        </p>
        <div class="actions">
          <template v-if="outcome.affected && target.kind === 'combatant'">
            <StatField v-model="damage" :label="t('duel.magicDamage')" :max="99" />
            <Button
              v-if="!applied"
              :label="t('duel.apply', { n: damage, name: target.id })"
              icon="pi pi-check"
              :disabled="locked || damage <= 0"
              @click="apply(target.id, damage)"
            />
          </template>
          <Button
            v-if="outcome.backfire && attacker.kind === 'combatant' && !applied"
            :label="t('duel.apply', { n: 3, name: attacker.id })"
            icon="pi pi-check"
            severity="danger"
            :disabled="locked"
            @click="apply(attacker.id, 3)"
          />
          <span v-if="applied" class="done"><i class="pi pi-check" /> {{ t('duel.applied') }}</span>
          <Button :label="t('duel.newRoll')" icon="pi pi-replay" severity="secondary" outlined @click="newRoll" />
        </div>
      </section>
      <p v-else class="waiting">{{ t('duel.waiting') }}</p>
    </section>

    <section class="side right" :aria-label="t('duel.targetSide')">
      <slot name="target" />
      <DicePool v-model="conviction" :dice="dice" :label="t('duel.conviction')" :max="3" />
      <CardReminders :fighter="target" />
    </section>
  </div>
</template>

<style scoped src="./arena.css"></style>
<style scoped>
.detail {
  margin: 0;
  font-size: 0.9rem;
}
</style>
