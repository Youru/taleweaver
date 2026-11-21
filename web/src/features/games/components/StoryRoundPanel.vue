<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import { useItems } from '@/features/catalog'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CampaignState, PartyCommand } from '../api/types'
import { activeMembers } from '../lib/storyRound'

/**
 * A Story Round (rulebook p. 13), above its question: the three phases, what the Store sells (the Unique Item
 * drawn for it), and the party between encounters (p. 10-11): who is in the Active Group, who is Injured or
 * Unselectable. The phase buttons are the question below; buying happens in the inventory, opened on the Store.
 */
const props = defineProps<{
  campaign: CampaignState
  node?: ScenarioNode
  busy: boolean
}>()
const emit = defineEmits<{ party: [command: PartyCommand]; shop: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const catalog = useItems()

const round = computed(() => props.campaign.storyRound)
const phases = ['shop', 'explore', 'ventureForth'] as const
const phaseIndex = computed(() => (round.value?.phase === 'explore' ? 1 : 0))

const name = computed(() => pick(props.node?.name ?? props.node?.title, language.value) || props.node?.id || '')
const uniques = computed(() =>
  (props.campaign.storeUniques?.[round.value?.node ?? ''] ?? []).map((id) => ({
    id,
    name: pick(catalog.data.value?.find((i) => i.id === id)?.name, language.value) || id,
    bought: props.campaign.items.includes(id),
  })),
)
/** "All Mundane items", "All Accessories": the Store's lines in words. */
const lines = computed(() =>
  (props.node?.store?.items ?? []).map((line) => {
    const tiers = (line.tiers ?? []).map((tier) => t(`workshop.loot.${tier}`, tier)).join(', ')
    const types = (line.types ?? []).map((type) => t(`inventory.types.${type}`, type)).join(', ')
    return [tiers, types].filter(Boolean).join(' · ') || t('storyRound.everything')
  }),
)
const attempted = computed(() =>
  (props.node?.sideQuests ?? []).filter((q) => props.campaign.sideQuestsAttempted?.includes(q.id ?? q.node)),
)

const active = computed(() => new Set(activeMembers(props.campaign)))
function toggleActive(name: string, on: boolean) {
  const now = activeMembers(props.campaign)
  const next = on ? [...now, name] : now.filter((n) => n !== name)
  // Keep the party's order
  emit('party', { type: 'activeGroup', adventurers: props.campaign.party.map((a) => a.name).filter((n) => next.includes(n)) })
}
</script>

<template>
  <section v-if="round" class="story-round" :aria-label="t('storyRound.title', { name })">
    <header>
      <span class="kicker">{{ t('storyRound.kicker') }}</span>
      <h2>{{ name }}</h2>
      <ol class="phases">
        <li v-for="(phase, i) in phases" :key="phase" :class="{ done: i < phaseIndex, current: i === phaseIndex }">
          {{ t(`storyRound.phase.${phase}`) }}
        </li>
      </ol>
    </header>

    <div v-if="round.phase === 'shop'" class="block">
      <h3>{{ t('storyRound.store') }}</h3>
      <ul class="lines">
        <li v-for="line in lines" :key="line">{{ t('storyRound.all', { what: line }) }}</li>
        <li v-if="node?.store?.upgrades">{{ t('storyRound.upgrades') }}</li>
        <li v-for="u in uniques" :key="u.id">
          {{ t('storyRound.unique', { name: u.name }) }}
          <Tag v-if="u.bought" :value="t('storyRound.bought')" severity="secondary" />
        </li>
      </ul>
      <p class="hint">{{ t('storyRound.shopHint') }}</p>
      <Button :label="t('storyRound.openStore')" icon="pi pi-shop" :disabled="busy" @click="emit('shop')" />
    </div>
    <p v-else-if="attempted.length" class="hint">
      {{ t('storyRound.attempted', { quests: attempted.map((q) => pick(q.label, language) || q.node).join(', ') }) }}
    </p>

    <div class="block">
      <h3>{{ t('storyRound.party') }}</h3>
      <p class="hint">{{ t('storyRound.partyHint') }}</p>
      <Message v-if="active.size > 4" severity="warn" size="small">
        {{ t('storyRound.tooMany', { n: active.size }) }}
      </Message>
      <table class="party">
        <thead>
          <tr>
            <th scope="col">{{ t('storyRound.adventurer') }}</th>
            <th scope="col">{{ t('storyRound.activeGroup') }}</th>
            <th scope="col">{{ t('storyRound.injured') }}</th>
            <th scope="col">{{ t('storyRound.unselectable') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="a in campaign.party" :key="a.name">
            <th scope="row">{{ a.name }} <span class="xp">{{ t('play.xp', { xp: a.xp }) }}</span></th>
            <td>
              <Checkbox
                binary
                :model-value="active.has(a.name)"
                :disabled="busy || a.injured || a.unselectable || (!active.has(a.name) && active.size >= 4)"
                :aria-label="t('storyRound.inGroup', { name: a.name })"
                @update:model-value="toggleActive(a.name, $event)"
              />
            </td>
            <td v-for="status in ['injured', 'unselectable'] as const" :key="status">
              <Checkbox
                binary
                :model-value="!!a[status]"
                :disabled="busy"
                :aria-label="t(`storyRound.${status}Of`, { name: a.name })"
                @update:model-value="emit('party', { type: 'status', adventurer: a.name, status, value: $event })"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.story-round {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
}
header {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.25rem 1rem;
}
.kicker {
  width: 100%;
  font-variant: small-caps;
  color: var(--p-primary-color);
  font-weight: 600;
}
h2 {
  margin: 0;
  font-size: 1.3rem;
  flex: 1;
}
h3 {
  margin: 0 0 0.3rem;
  font-size: 1rem;
}
.phases {
  display: flex;
  gap: 0.3rem;
  margin: 0;
  padding: 0;
  list-style: none;
  counter-reset: phase;
}
.phases li {
  counter-increment: phase;
  padding: 0.15rem 0.6rem;
  border-radius: 999px;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
  border: 1px solid var(--p-content-border-color);
}
.phases li::before {
  content: counter(phase) '. ';
}
.phases li.current {
  color: var(--p-primary-contrast-color);
  background: var(--p-primary-color);
  border-color: var(--p-primary-color);
}
.phases li.done {
  text-decoration: line-through;
}
.lines {
  margin: 0 0 0.4rem;
  padding-left: 1.2rem;
}
.lines li {
  margin: 0.1rem 0;
}
.hint {
  margin: 0 0 0.4rem;
  font-size: 0.9rem;
  color: var(--p-text-muted-color);
}
.party {
  border-collapse: collapse;
}
.party th,
.party td {
  padding: 0.25rem 0.75rem 0.25rem 0;
  text-align: left;
}
.party td {
  text-align: center;
}
.party thead th {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.xp {
  font-weight: normal;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
