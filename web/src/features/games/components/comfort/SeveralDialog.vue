<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { useEffects } from '@/features/catalog'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { EncounterCommand, EncounterState } from '../../api/types'
import { figureName, figuresInPlay } from '../../lib/play'

/** The same on several figures (play-spec M13): one action, one undo. */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{ encounter: EncounterState; locked: boolean }>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const effects = useEffects()

const all = computed(() => figuresInPlay(props.encounter))
const opponents = computed(() =>
  props.encounter.combatants.filter((c) => !c.defeated && c.side !== 'players').map((c) => c.instance),
)
const figures = ref<string[]>([])
const amount = ref<number | null>(1)
const effect = ref<string>()
const options = computed(() =>
  (effects.data.value ?? []).map((e) => ({ value: e.id, label: pick(e.name, language.value) || e.id })),
)
watch(open, () => (figures.value = []))

function send(command: { amount: number } | { effect: string; active: boolean }) {
  emit('command', { type: 'several', figures: figures.value, ...command })
  open.value = false
}
</script>

<template>
  <Dialog v-model:visible="open" modal :header="t('comfort.several.title')" :style="{ width: 'min(32rem, 95vw)' }">
    <div class="row">
      <Button :label="t('comfort.several.opponents')" size="small" text @click="figures = [...opponents]" />
      <Button :label="t('comfort.several.none')" size="small" text @click="figures = []" />
    </div>
    <ul>
      <li v-for="figure in all" :key="figure">
        <label>
          <Checkbox v-model="figures" :value="figure" />
          {{ figureName(figure) }}
        </label>
      </li>
    </ul>
    <div class="row">
      <InputNumber v-model="amount" :min="1" :max="99" show-buttons :aria-label="t('comfort.several.amount')" />
      <Button
        :label="t('comfort.several.damage')"
        severity="danger"
        :disabled="locked || !figures.length || !amount"
        @click="send({ amount: amount ?? 0 })"
      />
      <Button
        :label="t('comfort.several.heal')"
        severity="success"
        outlined
        :disabled="locked || !figures.length || !amount"
        @click="send({ amount: -(amount ?? 0) })"
      />
    </div>
    <div class="row">
      <Select
        v-model="effect"
        :options="options"
        option-label="label"
        option-value="value"
        filter
        :placeholder="t('comfort.several.effect')"
      />
      <Button
        :label="t('comfort.several.add')"
        :disabled="locked || !figures.length || !effect"
        @click="send({ effect: effect ?? '', active: true })"
      />
      <Button
        :label="t('comfort.several.remove')"
        severity="secondary"
        :disabled="locked || !figures.length || !effect"
        @click="send({ effect: effect ?? '', active: false })"
      />
    </div>
  </Dialog>
</template>

<style scoped>
ul {
  max-height: 40vh;
  overflow-y: auto;
  margin: 0.4rem 0;
  padding: 0;
  list-style: none;
}
label {
  display: flex;
  gap: 0.5rem;
  padding: 0.2rem 0;
}
.row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.5rem;
}
</style>
