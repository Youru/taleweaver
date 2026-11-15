<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import InputNumber from 'primevue/inputnumber'
import { usePreferences } from '@/shared/lib/preferences'
import type { Dice } from '../api/types'
import type { AdventurerSheet, Fighter } from '../lib/fighters'
import { decimal, percent, typed } from '../lib/format'
import { resolveSpell, spellOdds, type SpellSetup } from '../lib/spell'
import DicePool from './DicePool.vue'
import OddsLine from './OddsLine.vue'

/**
 * A Spell (rulebook p. 53) or a Spell-Like Ability (FORCE X): the Force against the target's
 * Conviction Check.
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

const castingRoll = ref<number | null>(null)
const books = ref(0)
const skull = ref(false)
const convictionRoll = ref<number | null>(null)
const damage = ref(0)

const outcome = computed(() =>
  (spellLike.value || castingRoll.value !== null) && convictionRoll.value !== null
    ? resolveSpell(setup.value, {
        casting: castingRoll.value ?? 0,
        books: books.value,
        skull: skull.value,
        conviction: convictionRoll.value,
      })
    : undefined,
)

function clearRoll() {
  castingRoll.value = convictionRoll.value = null
  books.value = damage.value = 0
  skull.value = false
}

function apply(instance: string, amount: number) {
  emit('apply', instance, amount)
  clearRoll()
}
</script>

<template>
  <div class="spell">
    <section :aria-label="t('duel.before')">
      <label class="check"><Checkbox v-model="spellLike" binary /> {{ t('duel.spellLike') }}</label>
      <DicePool v-if="!spellLike" v-model="casting" :dice="dice" :label="t('duel.castingDie')" :max="1" />
      <label v-if="!spellLike" class="check"><Checkbox v-model="empower" binary /> {{ t('duel.empowerSpell') }}</label>
      <div class="numbers">
        <label>
          {{ spellLike ? t('duel.printedForce') : t('duel.spellX') }}
          <InputNumber
            v-model="force"
            :aria-label="spellLike ? t('duel.printedForce') : t('duel.spellX')"
            :min="0"
            :max="40"
            show-buttons
            size="small"
          />
        </label>
      </div>
      <DicePool v-model="conviction" :dice="dice" :label="t('duel.conviction', { name: target.name })" :max="3" />
      <OddsLine v-if="showOdds" :missing="!odds">
        <template v-if="odds">
          <strong>{{ t('duel.affectedOdds', { p: percent(odds.affected, language) }) }}</strong>
          <span>{{ t('duel.meanForce', { n: decimal(odds.force, language) }) }}</span>
          <span v-if="odds.backfire">{{ t('duel.backfireOdds', { p: percent(odds.backfire, language) }) }}</span>
        </template>
      </OddsLine>
    </section>

    <section :aria-label="t('duel.roll')">
      <h4>{{ t('duel.roll') }}</h4>
      <div class="numbers">
        <label v-if="!spellLike">
          {{ t('duel.castingRoll') }}
          <InputNumber
            v-model="castingRoll"
            :aria-label="t('duel.castingRoll')"
            :min="0"
            :max="20"
            size="small"
            @input="(e) => (castingRoll = typed(e.value))"
          />
        </label>
        <label v-if="empower">
          <span><i class="pi pi-book" /> {{ t('duel.books') }}</span>
          <InputNumber
            v-model="books"
            :aria-label="t('duel.books')"
            :min="0"
            :max="4"
            show-buttons
            size="small"
            @input="(e) => (books = typed(e.value) ?? 0)"
          />
        </label>
        <label>
          {{ t('duel.convictionRoll') }}
          <InputNumber
            v-model="convictionRoll"
            :aria-label="t('duel.convictionRoll')"
            :min="0"
            :max="40"
            size="small"
            @input="(e) => (convictionRoll = typed(e.value))"
          />
        </label>
      </div>
      <label v-if="empower" class="check"><Checkbox v-model="skull" binary /> {{ t('duel.skull') }}</label>

      <template v-if="outcome">
        <p v-if="outcome.backfire" class="verdict bad">{{ t('duel.backfire') }}</p>
        <p v-else class="verdict" :class="{ good: outcome.affected }">
          {{ t('duel.forceAgainst', { force: outcome.force, conviction: convictionRoll ?? 0 }) }}
          <strong>{{ outcome.affected ? t('duel.affected') : t('duel.resisted') }}</strong>
        </p>
        <div class="apply">
          <template v-if="outcome.affected && target.kind === 'combatant'">
            <label class="inline">
              {{ t('duel.magicDamage') }}
              <InputNumber
                v-model="damage"
                :aria-label="t('duel.magicDamage')"
                :min="0"
                :max="99"
                show-buttons
                size="small"
              />
            </label>
            <Button
              :label="t('duel.apply', { n: damage, name: target.id })"
              icon="pi pi-check"
              size="small"
              :disabled="locked || damage <= 0"
              @click="apply(target.id, damage)"
            />
          </template>
          <Button
            v-if="outcome.backfire && attacker.kind === 'combatant'"
            :label="t('duel.apply', { n: 3, name: attacker.id })"
            icon="pi pi-check"
            size="small"
            severity="danger"
            :disabled="locked"
            @click="apply(attacker.id, 3)"
          />
          <Button :label="t('duel.newRoll')" size="small" severity="secondary" text @click="clearRoll" />
        </div>
      </template>
    </section>
  </div>
</template>

<style scoped>
.spell,
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
.numbers {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(7.5rem, 1fr));
  gap: 0.4rem 0.6rem;
}
.numbers label,
.inline {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.numbers :deep(.p-inputnumber),
.numbers :deep(.p-inputnumber-input),
.inline :deep(.p-inputnumber-input) {
  width: 100%;
  min-width: 0;
}
.inline :deep(.p-inputnumber) {
  width: 8rem;
}
.verdict {
  margin: 0;
  padding: 0.4rem 0.6rem;
  border-left: 3px solid var(--p-content-border-color);
}
.verdict.good {
  border-color: var(--p-green-500);
}
.verdict.bad {
  border-color: var(--p-red-500);
}
.apply {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 0.5rem;
}
</style>
