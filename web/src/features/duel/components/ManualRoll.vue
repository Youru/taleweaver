<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Checkbox from 'primevue/checkbox'
import InputNumber from 'primevue/inputnumber'
import type { AttackRoll } from '../lib/attack'
import { typed } from '../lib/format'
import { symbolIcon } from '../lib/icons'
import GameIcon from './GameIcon.vue'

/** The roll typed in: a total and symbol counts, for rerolls or symbols coming from elsewhere. */
const roll = defineModel<AttackRoll | null>({ required: true })
defineProps<{ empowered: boolean; dodge: boolean }>()

const { t } = useI18n()
const total = ref<number | null>(roll.value?.total ?? null)
const shield = ref(roll.value?.shield ?? 0)
const book = ref(roll.value?.book ?? 0)
const star = ref(roll.value?.star ?? 0)
const skull = ref(roll.value?.skull ?? false)
const dodgeShields = ref(roll.value?.dodgeShields ?? 0)
const dodgeSkull = ref(roll.value?.dodgeSkull ?? false)

watch([total, shield, book, star, skull, dodgeShields, dodgeSkull], () => {
  roll.value =
    total.value === null
      ? null
      : {
          total: total.value,
          shield: shield.value,
          book: book.value,
          star: star.value,
          skull: skull.value,
          dodgeShields: dodgeShields.value,
          dodgeSkull: dodgeSkull.value,
        }
})
</script>

<template>
  <div class="manual">
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
      <span><GameIcon :src="symbolIcon('shield')" /> {{ t('duel.shields') }}</span>
      <InputNumber
        v-model="shield"
        :aria-label="t('duel.shields')"
        :min="0"
        :max="30"
        size="small"
        @input="(e) => (shield = typed(e.value) ?? 0)"
      />
    </label>
    <label>
      <span><GameIcon :src="symbolIcon('book')" /> {{ t('duel.books') }}</span>
      <InputNumber
        v-model="book"
        :aria-label="t('duel.books')"
        :min="0"
        :max="30"
        size="small"
        @input="(e) => (book = typed(e.value) ?? 0)"
      />
    </label>
    <label>
      <span><GameIcon :src="symbolIcon('star')" /> {{ t('duel.stars') }}</span>
      <InputNumber
        v-model="star"
        :aria-label="t('duel.stars')"
        :min="0"
        :max="30"
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
        size="small"
        @input="(e) => (dodgeShields = typed(e.value) ?? 0)"
      />
    </label>
    <label v-if="empowered" class="check"><Checkbox v-model="skull" binary /> {{ t('duel.skull') }}</label>
    <label v-if="dodge" class="check"><Checkbox v-model="dodgeSkull" binary /> {{ t('duel.dodgeSkull') }}</label>
  </div>
</template>

<style scoped>
.manual {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(7rem, 1fr));
  gap: 0.5rem 0.7rem;
}
label {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.check {
  flex-direction: row;
  align-items: center;
  gap: 0.4rem;
}
.manual :deep(.p-inputnumber),
.manual :deep(.p-inputnumber-input) {
  width: 100%;
  min-width: 0;
}
</style>
