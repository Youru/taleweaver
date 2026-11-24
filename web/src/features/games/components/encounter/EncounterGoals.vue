<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CampaignState, EncounterCommand, EncounterState, GameOptions } from '../../api/types'
import { activeOptions, playedByHand } from '../../lib/options'
import AchievementList from '../AchievementList.vue'

/** What the encounter is about: how it ends, its tokens, its special rules. */
const props = defineProps<{
  encounter: EncounterState
  node: ScenarioNode | undefined
  /** Reports are refused right now. */
  locked: boolean
  /** With the scenario read: the Achievements can be completed. */
  campaign?: CampaignState
  scenario?: string
  options?: GameOptions | null
}>()
const reminders = computed(() => activeOptions(props.options).filter(playedByHand))
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const endConditions = computed(() =>
  (props.node?.endConditions ?? []).filter((e) => props.encounter.activeEndConditions.includes(e.id)),
)
/** Tokens with an action in this encounter, on the board or still to come. */
const tokens = computed(() => {
  const onBoard = new Set(props.encounter.tokens.filter((tk) => !tk.removed).map((tk) => tk.key))
  return Object.entries(props.node?.tokens ?? {})
    .map(([key, rule]) => ({ key, rule, onBoard: onBoard.has(key) }))
    .sort((a, b) => Number(b.onBoard) - Number(a.onBoard) || a.key.localeCompare(b.key))
})
</script>

<template>
  <div class="goals">
    <template v-if="node?.gmNote">
      <h3>{{ t('play.gmNote') }}</h3>
      <p class="gm-note">{{ pick(node.gmNote, language) }}</p>
    </template>
    <h3>{{ t('play.endConditions') }}</h3>
    <ul>
      <li v-for="end in endConditions" :key="end.id" class="end">
        <Tag
          :value="end.kind === 'win' ? t('play.win') : t('play.lose')"
          :severity="end.kind === 'win' ? 'success' : 'danger'"
        />
        <span>{{ pick(end.text, language) }}</span>
        <Button
          :label="t('play.reached')"
          size="small"
          outlined
          :disabled="locked"
          @click="emit('command', { type: 'endCondition', endCondition: end.id })"
        />
      </li>
    </ul>

    <AchievementList
      v-if="node && campaign && scenario"
      :node="node"
      :scenario="scenario"
      :campaign="campaign"
      :locked="locked"
      @complete="(achievement, adventurer) => emit('command', { type: 'achievement', achievement, adventurer })"
    />
    <template v-if="reminders.length">
      <h3>{{ t('options.reminders') }}</h3>
      <dl>
        <template v-for="key in reminders" :key="key">
          <dt>{{ t(`options.${key}.name`) }}</dt>
          <dd>{{ t(`options.${key}.text`) }}</dd>
        </template>
      </dl>
    </template>

    <h3>{{ t('play.tokens') }}</h3>
    <ul>
      <li v-for="token in tokens" :key="token.key" class="token" :class="{ gone: !token.onBoard }">
        <strong>{{ token.key }}</strong>
        <span>{{ pick(token.rule.activation, language) }}</span>
        <Button
          :label="t('play.activate')"
          size="small"
          severity="secondary"
          :disabled="locked || !token.onBoard"
          :aria-label="t('table.token', { token: token.key })"
          @click="emit('command', { type: 'activateToken', token: token.key })"
        />
      </li>
    </ul>

    <template v-if="node?.specialRules?.length">
      <h3>{{ t('play.specialRules') }}</h3>
      <dl>
        <template v-for="rule in node.specialRules" :key="rule.id">
          <dt>{{ pick(rule.name, language) }}</dt>
          <dd>{{ pick(rule.text, language) }}</dd>
        </template>
      </dl>
    </template>
  </div>
</template>

<style scoped>
.gm-note {
  margin: 0;
  padding: 0.4rem 0.6rem;
  border-left: 3px solid var(--p-primary-color);
  white-space: pre-line;
}
h3 {
  margin: 0.8rem 0 0.4rem;
  font-size: 0.85rem;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}
h3:first-child {
  margin-top: 0;
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
li {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.9rem;
}
.token.gone {
  opacity: 0.5;
}
dl {
  margin: 0;
  font-size: 0.9rem;
}
dt {
  font-weight: 600;
}
dd {
  margin: 0 0 0.5rem;
  color: var(--p-text-muted-color);
}
</style>
