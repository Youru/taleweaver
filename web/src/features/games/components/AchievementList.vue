<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Select from 'primevue/select'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CampaignState } from '../api/types'

/** The encounter's Achievements (rulebook p. 17): the condition is read by all, the hidden text once completed. */
const props = defineProps<{ node: ScenarioNode; scenario: string; campaign: CampaignState; locked: boolean }>()
const emit = defineEmits<{ complete: [achievement: string, adventurer: string] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const names = computed(() => props.campaign.party.map((a) => a.name))
const chosen = ref<Record<string, string | undefined>>({})
const doneBy = (id: string) =>
  props.campaign.achievements?.find((a) => a.id === id && a.node === props.node.id && a.scenario === props.scenario)
    ?.adventurer
</script>

<template>
  <section v-if="node.achievements?.length" class="achievements">
    <h3>{{ t('achievements.title') }}</h3>
    <ul>
      <li v-for="item in node.achievements" :key="item.id">
        <strong><i class="pi pi-trophy" aria-hidden="true" /> {{ pick(item.name, language) }}</strong>
        <p v-if="item.condition">{{ pick(item.condition, language) }}</p>
        <p v-if="doneBy(item.id)" class="done">{{ t('achievements.done', { name: doneBy(item.id) ?? '' }) }}</p>
        <div v-else class="complete">
          <Select
            v-model="chosen[item.id]"
            :options="names"
            :placeholder="t('achievements.who')"
            :aria-label="t('achievements.who')"
            size="small"
          />
          <Button
            :label="t('achievements.complete')"
            size="small"
            outlined
            :disabled="locked || !chosen[item.id]"
            @click="emit('complete', item.id, chosen[item.id]!)"
          />
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
h3 {
  margin: 0.8rem 0 0.4rem;
  font-size: 0.85rem;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  font-size: 0.9rem;
}
li {
  padding: 0.3rem 0;
}
p {
  margin: 0.2rem 0;
}
.done {
  color: var(--p-primary-color);
  font-weight: 600;
}
.complete {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}
</style>
