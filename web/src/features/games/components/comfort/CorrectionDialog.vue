<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { EncounterCommand, GameState } from '../../api/types'
import { shortId } from '../../lib/ids'

/** "Correct" (play-spec M3): what the app did not foresee, written in the journal and undone like any action. */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{ state: GameState; nodes: readonly ScenarioNode[]; busy: boolean; error?: string | null }>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const fight = computed(() => (props.state.encounter && !props.state.encounter.ended ? props.state.encounter : undefined))
const fields = computed(() =>
  [
    'gold',
    'xp',
    'flag',
    ...(fight.value ? ['urgency', 'trackOut', 'trackIn', 'spawn'] : []),
    ...(fight.value?.round ? ['round'] : []),
    'node',
  ].map((value) => ({ value, label: t(`comfort.correct.field.${value}`) })),
)
const field = ref('gold')
const who = ref<string>()
const amount = ref<number | null>(0)
const name = ref('')
const text = ref('')

const party = computed(() => props.state.campaign.party.map((a) => a.name))
const flags = computed(() => Object.keys(props.state.campaign.variables ?? {}))
const families = computed(() => {
  const found = new Set(fight.value?.combatants.map((c) => c.family))
  JSON.stringify(props.nodes).replace(/"family":"([^"]+)"/g, (all, family: string) => {
    found.add(family)
    return all
  })
  return [...found].sort()
})
const choices = computed(() => {
  const e = fight.value
  if (field.value === 'xp') return party.value.map((value) => ({ value, label: value }))
  if (field.value === 'spawn') return families.value.map((value) => ({ value, label: value }))
  if (field.value === 'node')
    return props.nodes.map((n) => ({ value: n.id, label: pick(n.name ?? n.title, language.value) || n.id }))
  if (!e) return []
  const current = e.round ? e.initiativeTrack[e.currentTurn] : undefined
  const cards =
    field.value === 'trackOut'
      ? e.initiativeTrack.filter((card) => card !== current)
      : [
          ...new Set([
            ...e.adventurers.map((a) => `adventurer:${a.name}`),
            ...(e.allies ?? []).filter((a) => !a.defeated).map((a) => a.trackCard),
            ...e.combatants.filter((c) => !c.defeated).flatMap((c) => c.initiativeCards),
          ]),
        ].filter((card) => !e.initiativeTrack.includes(card))
  return cards.map((value) => ({ value, label: shortId(value) }))
})
const hasChoice = computed(() => ['xp', 'spawn', 'node', 'trackOut', 'trackIn'].includes(field.value))
const hasNumber = computed(() => ['gold', 'xp', 'urgency', 'round', 'spawn'].includes(field.value))

/** The value as it is now, to be changed. */
function reset() {
  const { campaign } = props.state
  if (!hasChoice.value || !choices.value.some((c) => c.value === who.value)) who.value = undefined
  amount.value =
    field.value === 'gold'
      ? campaign.gold
      : field.value === 'xp'
        ? (campaign.party.find((a) => a.name === who.value)?.xp ?? 0)
        : field.value === 'urgency'
          ? (fight.value?.urgency ?? 0)
          : field.value === 'round'
            ? (fight.value?.round ?? 1)
            : 1
}
watch([open, field], () => {
  if (!fields.value.some((f) => f.value === field.value)) field.value = 'gold'
  who.value = undefined
  reset()
})
watch(who, () => field.value === 'xp' && reset())
watch(name, (value) => {
  const now = props.state.campaign.variables?.[value]
  text.value = now === undefined ? '' : String(now)
})

const command = computed<EncounterCommand | undefined>(() => {
  const value = amount.value ?? undefined
  switch (field.value) {
    case 'gold':
    case 'urgency':
    case 'round':
      return value === undefined ? undefined : { type: 'correct', field: field.value, value }
    case 'xp':
      return who.value && value !== undefined ? { type: 'correct', field: 'xp', instance: who.value, value } : undefined
    case 'flag':
      return name.value.trim() ? { type: 'correct', field: 'flag', item: name.value, text: text.value } : undefined
    case 'spawn':
      return who.value && value ? { type: 'correct', field: 'spawn', item: who.value, value } : undefined
    case 'node':
      return who.value ? { type: 'correct', field: 'node', item: who.value } : undefined
    default:
      return who.value
        ? { type: 'correct', field: 'track', card: who.value, active: field.value === 'trackIn' }
        : undefined
  }
})
function send() {
  if (!command.value) return
  emit('command', command.value)
  open.value = false
}
</script>

<template>
  <Dialog v-model:visible="open" modal :header="t('comfort.correct.title')" :style="{ width: 'min(30rem, 95vw)' }">
    <div class="form">
      <Select
        v-model="field"
        :options="fields"
        option-label="label"
        option-value="value"
        :aria-label="t('comfort.correct.what')"
      />
      <Select
        v-if="hasChoice"
        v-model="who"
        :options="choices"
        option-label="label"
        option-value="value"
        filter
        :placeholder="t('comfort.correct.choose')"
        :aria-label="t('comfort.correct.choose')"
      />
      <template v-if="field === 'flag'">
        <InputText v-model="name" list="correction-flags" :placeholder="t('comfort.correct.flagName')" />
        <datalist id="correction-flags">
          <option v-for="flag in flags" :key="flag" :value="flag" />
        </datalist>
        <InputText v-model="text" :placeholder="t('comfort.correct.flagValue')" />
      </template>
      <InputNumber
        v-if="hasNumber"
        v-model="amount"
        show-buttons
        :min="field === 'round' || field === 'spawn' ? 1 : 0"
        :max="field === 'spawn' ? 12 : field === 'urgency' ? 9 : 9999"
        :aria-label="t('comfort.correct.value')"
      />
      <Message v-if="field === 'node'" severity="warn" size="small">{{ t('comfort.correct.nodeWarning') }}</Message>
      <p v-else class="muted">{{ t('comfort.correct.note') }}</p>
      <Message v-if="error" severity="error" size="small">{{ error }}</Message>
      <Button icon="pi pi-check" :label="t('comfort.correct.apply')" :disabled="busy || !command" @click="send" />
    </div>
  </Dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
.muted {
  margin: 0;
  color: var(--p-text-muted-color);
  font-size: 0.85rem;
}
</style>
