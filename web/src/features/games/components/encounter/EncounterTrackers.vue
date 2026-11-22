<script setup lang="ts">
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Select from 'primevue/select'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CampaignState, EncounterCommand, EncounterState } from '../../api/types'
import { shortId } from '../../lib/ids'
import { figureName, figuresInPlay } from '../../lib/play'

/** Counters, what is carried, effects for later, the track dealt, the Espers an opponent may summon. */
const props = defineProps<{
  campaign: CampaignState
  encounter: EncounterState
  node: ScenarioNode | undefined
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const figures = computed(() => figuresInPlay(props.encounter).map((value) => ({ value, label: figureName(value) })))
const carried = computed(() => props.campaign.carried ?? [])
const target = reactive<Record<string, string | undefined>>({})

function carry(id: string) {
  emit('command', { type: 'carry', id, figure: target[id] })
  target[id] = undefined
}

const espers = computed(() =>
  (props.node?.encounterEspers ?? []).flatMap((esper) =>
    props.encounter.combatants
      .filter((c) => !c.defeated && c.instance === esper.summoner)
      .map((c) => ({ card: esper.ref, summoner: c.instance })),
  ),
)
</script>

<template>
  <section class="trackers" :aria-label="t('trackers.title')">
    <Button
      v-if="!encounter.round && !encounter.trackDealt"
      :label="t('trackers.deal')"
      :title="t('trackers.dealHint')"
      icon="pi pi-clone"
      size="small"
      severity="secondary"
      :disabled="locked"
      @click="emit('command', { type: 'deal' })"
    />

    <template v-if="campaign.counters?.length">
      <h4>{{ t('trackers.counters') }}</h4>
      <div v-for="counter in campaign.counters" :key="counter.id" class="line">
        <span>
          {{ pick(counter.name, language) || counter.id }}
          <small v-if="counter.on">({{ figureName(counter.on) }})</small>
        </span>
        <Button
          icon="pi pi-minus"
          text
          rounded
          size="small"
          :aria-label="t('trackers.less', { name: counter.id })"
          :disabled="locked"
          @click="emit('command', { type: 'counter', id: counter.id, by: -1 })"
        />
        <strong>{{ counter.value }}</strong>
        <Button
          icon="pi pi-plus"
          text
          rounded
          size="small"
          :aria-label="t('trackers.more', { name: counter.id })"
          :disabled="locked"
          @click="emit('command', { type: 'counter', id: counter.id, by: 1 })"
        />
      </div>
    </template>

    <template v-if="carried.length">
      <h4>{{ t('trackers.carried') }}</h4>
      <div v-for="thing in carried" :key="thing.id" class="line">
        <span>
          {{ pick(thing.name, language) || thing.id }} :
          <template v-if="thing.lost">{{ t('trackers.lost') }}</template>
          <template v-else-if="thing.carrier">{{ figureName(thing.carrier) }}</template>
          <template v-else-if="thing.position">
            {{ t('trackers.onSquare', { x: thing.position.x, y: thing.position.y }) }}
          </template>
          <template v-else>{{ t('trackers.toPlace') }}</template>
        </span>
        <template v-if="!thing.lost">
          <Select
            v-model="target[thing.id]"
            :options="figures"
            option-label="label"
            option-value="value"
            size="small"
            :placeholder="t('trackers.givenTo')"
            :aria-label="t('trackers.givenTo')"
          />
          <Button :label="t('trackers.take')" size="small" text :disabled="locked || !target[thing.id]" @click="carry(thing.id)" />
          <Button
            v-if="thing.carrier"
            :label="t('trackers.drop')"
            size="small"
            text
            :disabled="locked"
            @click="emit('command', { type: 'carry', id: thing.id })"
          />
          <Button
            :label="t('trackers.lose')"
            size="small"
            text
            severity="danger"
            :disabled="locked"
            @click="emit('command', { type: 'carry', id: thing.id, lost: true })"
          />
        </template>
      </div>
    </template>

    <template v-if="encounter.delayed?.length">
      <h4>{{ t('trackers.later') }}</h4>
      <p v-for="effect in encounter.delayed" :key="effect.id" class="line">
        <strong>{{ t(`trackers.at.${effect.at}`, { round: effect.round }) }}</strong>
        <span>{{ pick(effect.text, language) || effect.id }}</span>
      </p>
    </template>

    <template v-if="espers.length">
      <h4>{{ t('trackers.espers') }}</h4>
      <Button
        v-for="esper in espers"
        :key="`${esper.summoner}/${esper.card}`"
        :label="t('trackers.summon', { summoner: esper.summoner, esper: shortId(esper.card) })"
        size="small"
        severity="secondary"
        :disabled="locked"
        @click="emit('command', { type: 'summonEsper', card: esper.card, summoner: esper.summoner })"
      />
    </template>
  </section>
</template>

<style scoped>
.trackers {
  margin-bottom: 0.6rem;
}
h4 {
  margin: 0.5rem 0 0.2rem;
}
.line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.25rem 0.5rem;
  margin: 0.15rem 0;
}
</style>
