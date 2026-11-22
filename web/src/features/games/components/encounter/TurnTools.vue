<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Select from 'primevue/select'
import type { AdventurerState, EncounterCommand, EncounterState } from '../../api/types'
import { actionCosts, figureName, figuresInPlay, playingNow, reactionKinds } from '../../lib/play'

/**
 * Beside the turn: the SP of whoever plays, who did what is reported, the reaction that interrupts the turn (it
 * resumes when it ends), and the AI step none of whose instructions is possible.
 */
const props = defineProps<{ encounter: EncounterState; party: AdventurerState[]; locked: boolean }>()
/** Who is proposed as the author of what is reported. */
const author = defineModel<string | null>('author', { required: true })
const emit = defineEmits<{
  command: [command: EncounterCommand]
  duel: [figure: string, target?: string]
}>()

const { t } = useI18n()
const playing = computed(() => playingNow(props.encounter))

function staminaOf(figure: string) {
  const name = figureName(figure)
  const held = figure.startsWith('ally:')
    ? props.encounter.allies?.find((a) => a.id === name)
    : props.party.find((a) => a.name === name)
  return held?.stamina ?? 0
}

const NOBODY = '-'
const authors = computed(() => [
  { value: NOBODY, label: t('turnTools.authorUnknown') },
  ...figuresInPlay(props.encounter)
    .filter((id) => id.startsWith('adventurer:') || id.startsWith('ally:'))
    .map((value) => ({ value, label: figureName(value) })),
])
// Proposed from the turn, corrected by the GM
watch(
  () => [props.encounter.round, props.encounter.currentTurn, playing.value[0]].join('|'),
  () => (author.value = playing.value[0] ?? null),
  { immediate: true },
)

const reaction = computed(() => props.encounter.reactions?.at(-1))

const opened = ref(false)
const figures = computed(() => figuresInPlay(props.encounter).map((value) => ({ value, label: figureName(value) })))
const kinds = computed(() => reactionKinds.map((value) => ({ value, label: t(`turnTools.kind.${value}`) })))
const kind = ref<(typeof reactionKinds)[number]>('counter')
const who = ref<string>()
const against = ref<string>()

function react() {
  if (!who.value) return
  emit('command', { type: 'react', figure: who.value, kind: kind.value, against: against.value })
  opened.value = false
}
/** A Redirect is the attacker's Attack on the new target; the others are the reactor's own. */
function resolve() {
  const r = reaction.value
  if (!r) return
  if (r.kind === 'redirect' && r.against) emit('duel', r.against, r.figure)
  else emit('duel', r.figure, r.against ?? undefined)
}
</script>

<template>
  <section class="tools" :aria-label="t('turnTools.title')">
    <div v-if="reaction" class="reaction" role="status">
      <strong>
        {{ t(`turnTools.kind.${reaction.kind}`) }} : {{ figureName(reaction.figure) }}
        <template v-if="reaction.against"> → {{ figureName(reaction.against) }}</template>
      </strong>
      <span>{{ t('turnTools.interrupted') }}</span>
      <Button :label="t('turnTools.resolve')" icon="pi pi-bolt" size="small" :disabled="locked" @click="resolve" />
      <Button
        :label="t('turnTools.resume')"
        icon="pi pi-play"
        size="small"
        severity="secondary"
        :disabled="locked"
        @click="emit('command', { type: 'endReaction' })"
      />
    </div>

    <div v-for="figure in playing" :key="figure" class="row">
      <strong>{{ figureName(figure) }} · {{ staminaOf(figure) }} SP</strong>
      <Button
        v-for="[action, cost] in actionCosts"
        :key="action"
        :label="`${t(`turnTools.action.${action}`)} ${cost}`"
        size="small"
        outlined
        :severity="staminaOf(figure) < cost ? 'danger' : 'secondary'"
        :title="staminaOf(figure) < cost ? t('turnTools.short') : undefined"
        :disabled="locked"
        @click="emit('command', { type: 'spend', figure, action })"
      />
    </div>

    <div class="row">
      <span class="label">{{ t('turnTools.author') }}</span>
      <Select
        :model-value="author ?? NOBODY"
        :options="authors"
        option-label="label"
        option-value="value"
        size="small"
        :aria-label="t('turnTools.author')"
        @update:model-value="(value: string) => (author = value === NOBODY ? null : value)"
      />
      <Button
        :label="t('turnTools.reaction')"
        icon="pi pi-reply"
        size="small"
        text
        :aria-expanded="opened"
        :disabled="locked"
        @click="opened = !opened"
      />
      <Button
        v-if="encounter.lastStep && !reaction"
        :label="t('turnTools.noInstruction')"
        :title="t('turnTools.noInstructionHint')"
        icon="pi pi-ban"
        size="small"
        text
        severity="secondary"
        :disabled="locked"
        @click="emit('command', { type: 'noInstruction' })"
      />
    </div>

    <form v-if="opened" class="row" @submit.prevent="react">
      <Select v-model="kind" :options="kinds" option-label="label" option-value="value" size="small" :aria-label="t('turnTools.reaction')" />
      <Select
        v-model="who"
        :options="figures"
        option-label="label"
        option-value="value"
        size="small"
        :placeholder="t(kind === 'redirect' ? 'turnTools.newTarget' : 'turnTools.who')"
        :aria-label="t(kind === 'redirect' ? 'turnTools.newTarget' : 'turnTools.who')"
      />
      <Select
        v-model="against"
        :options="figures"
        option-label="label"
        option-value="value"
        size="small"
        :placeholder="t(kind === 'redirect' ? 'turnTools.attacker' : 'turnTools.against')"
        :aria-label="t(kind === 'redirect' ? 'turnTools.attacker' : 'turnTools.against')"
      />
      <Button type="submit" :label="t('turnTools.start')" size="small" :disabled="locked || !who" />
    </form>
  </section>
</template>

<style scoped>
.tools {
  flex: none;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  max-height: 14rem;
  overflow-y: auto;
}
.row,
.reaction {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem;
}
.row :deep(.p-button-sm) {
  padding: 0.15rem 0.4rem;
}
.reaction {
  padding: 0.4rem 0.6rem;
  border-left: 3px solid var(--p-orange-400);
}
.label {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
