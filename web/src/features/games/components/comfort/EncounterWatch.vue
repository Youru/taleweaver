<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CampaignState, EncounterCommand, EncounterState, GameOptions } from '../../api/types'
import { proposals, remindersFor, urgencyComing } from '../../lib/comfort'
import { adventurerOf } from '../../lib/play'

/** What the GM would forget (play-spec M9 to M12): Ready, the rules of the moment, Urgency, what looks won. */
const props = defineProps<{
  encounter: EncounterState
  node: ScenarioNode | undefined
  campaign: CampaignState
  scenario: string
  options?: GameOptions | null
  unplaced: number
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const getsReady = computed(() => props.encounter.round === 0 && !props.encounter.ready)
const reminders = computed(() => remindersFor(props.encounter, props.node))
const coming = computed(() => urgencyComing(props.encounter, props.options))
const waived = computed(() => !!props.encounter.roundLog?.waived)
const found = computed(() =>
  proposals(props.encounter, props.node).filter(
    (p) =>
      p.kind === 'end' ||
      !props.campaign.achievements?.some(
        (a) => a.scenario === props.scenario && a.node === props.node?.id && a.id === p.id,
      ),
  ),
)
const names = computed(() => props.encounter.adventurers.map((a) => a.name))
const chosen = ref<string>()
/** The achiever proposed: who plays, else the first one. */
const achiever = computed({
  get: () =>
    chosen.value ?? adventurerOf(props.encounter.initiativeTrack[props.encounter.currentTurn]) ?? names.value[0],
  set: (value) => (chosen.value = value),
})
function labelOf(kind: 'end' | 'achievement', id: string) {
  const text =
    kind === 'end'
      ? props.node?.endConditions.find((e) => e.id === id)?.text
      : props.node?.achievements?.find((a) => a.id === id)?.name
  return pick(text, language.value) || id
}
</script>

<template>
  <div class="watch">
    <Message v-if="getsReady" severity="info" size="small">
      <strong>{{ t('comfort.ready.title') }}</strong>
      <ul>
        <li>{{ t('comfort.ready.rules', { n: node?.specialRules?.length ?? 0 }) }}</li>
        <li>{{ unplaced ? t('comfort.ready.unplaced', { n: unplaced }) : t('comfort.ready.placed') }}</li>
        <li>{{ encounter.trackDealt ? t('comfort.ready.dealt') : t('comfort.ready.willDeal') }}</li>
      </ul>
      <Button
        icon="pi pi-check"
        :label="t('comfort.ready.confirm')"
        size="small"
        :disabled="locked"
        @click="emit('command', { type: 'ready' })"
      />
    </Message>
    <Message v-if="coming" severity="warn" size="small">
      <span>{{ t('comfort.urgency.coming', { next: encounter.urgency + 1 }) }}</span>
      <Button
        :label="t('comfort.urgency.waive')"
        size="small"
        text
        :disabled="locked"
        @click="emit('command', { type: 'correct', field: 'noUrgency', active: true })"
      />
    </Message>
    <Message v-else-if="waived" severity="secondary" size="small">
      <span>{{ t('comfort.urgency.waived') }}</span>
      <Button
        :label="t('comfort.urgency.restore')"
        size="small"
        text
        :disabled="locked"
        @click="emit('command', { type: 'correct', field: 'noUrgency', active: false })"
      />
    </Message>
    <Message v-for="proposal in found" :key="proposal.kind + proposal.id" severity="success" size="small">
      <span>{{ t(`comfort.proposal.${proposal.kind}`, { name: labelOf(proposal.kind, proposal.id) }) }}</span>
      <Select
        v-if="proposal.kind === 'achievement'"
        v-model="achiever"
        :options="names"
        size="small"
        :aria-label="t('comfort.proposal.by')"
      />
      <Button
        :label="t('comfort.proposal.confirm')"
        size="small"
        :disabled="locked || (proposal.kind === 'achievement' && !achiever)"
        @click="
          emit(
            'command',
            proposal.kind === 'end'
              ? { type: 'endCondition', endCondition: proposal.id }
              : { type: 'achievement', achievement: proposal.id, adventurer: achiever ?? '' },
          )
        "
      />
    </Message>
    <details v-if="reminders.length" class="reminders">
      <summary>{{ t('comfort.reminders', { names: reminders.map((r) => pick(r.name, language)).join(', ') }) }}</summary>
      <p v-for="rule in reminders" :key="rule.id">
        <strong>{{ pick(rule.name, language) }}</strong> {{ pick(rule.text, language) }}
      </p>
    </details>
  </div>
</template>

<style scoped>
.watch {
  display: flex;
  flex-direction: column;
  flex: none;
  gap: 0.4rem;
}
.watch:empty {
  display: none;
}
ul {
  margin: 0.2rem 0;
  padding-left: 1.1rem;
}
.reminders {
  padding: 0.4rem 0.6rem;
  border-left: 3px solid var(--p-primary-color);
  font-size: 0.9rem;
}
.reminders p {
  margin: 0.4rem 0 0;
  white-space: pre-line;
}
</style>
