<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import { useDisciplines, useItems, useItemUpgrades } from '@/features/catalog'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { AdventurerState, CampaignState, ItemCommand, PartyCommand } from '../api/types'
import { activeMembers, disciplineBlock, disciplineCost, respecPrice, xpLeft } from '../lib/storyRound'

/**
 * A Story Round (rulebook p. 13), above its question: the three phases, what the Store sells (the Unique Item
 * drawn for it), and the party between encounters (p. 10-11): who is in the Active Group, who is Injured or
 * Unselectable. The phase buttons are the question below; buying happens in the inventory, opened on the Store.
 */
const props = defineProps<{
  campaign: CampaignState
  node?: ScenarioNode
  busy: boolean
  xpLeniency?: boolean
}>()
const emit = defineEmits<{ party: [command: PartyCommand]; item: [command: ItemCommand]; shop: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const catalog = useItems()
const upgradeCatalog = useItemUpgrades()
const disciplineCatalog = useDisciplines()

// ---- Item Upgrades unlocked (p. 28): for sale at every Shop and Train ----
const upgradeName = (id: string) => pick(upgradeCatalog.data.value?.find((u) => u.id === id)?.name, language.value) || id
const unlocked = computed(() => props.campaign.unlockedUpgrades ?? [])
const lockedUpgrades = computed(() =>
  (upgradeCatalog.data.value ?? [])
    .filter((u) => !unlocked.value.includes(u.id))
    .map((u) => ({ value: u.id, label: pick(u.name, language.value) })),
)
const toUnlock = ref<string | null>(null)

// ---- Training: Disciplines learnt with XP (p. 24, 33) ----
const disciplineName = (id: string) =>
  pick(disciplineCatalog.data.value?.find((d) => d.id === id)?.name, language.value) || id
/** The cards an adventurer may pick: those they cannot learn yet are shown greyed out, with the reason. */
function learnable(adventurer: AdventurerState) {
  const all = disciplineCatalog.data.value ?? []
  // An Exalted Esper is used in an encounter, not learnt
  return all.filter((d) => d.tree !== 'exalted').map((d) => {
    const block = disciplineBlock(d, adventurer, all)
    const why = !block
      ? ''
      : 'learnt' in block
        ? t('storyRound.alreadyLearnt')
        : 'familiar' in block
          ? t(`storyRound.familiar.${block.familiar}`)
          : 'missingLevel' in block
            ? t('storyRound.missingLevel', { level: block.missingLevel })
            : ''
    return {
      value: d.id,
      label: `${pick(d.name, language.value)} · ${t(`storyRound.trees.${d.tree}`, d.tree)} ${d.level ?? ''}${why ? ` (${why})` : ''}`,
      disabled: !!block,
    }
  })
}
/** Re-specing (p. 87): its price depends on the Store of this Story Round. */
const respec = computed(() => respecPrice(props.node?.store))
/** XP Leniency: each adventurer's first Re-Spec is free. */
const respecFor = (name: string) => (props.xpLeniency && !props.campaign.freeRespecs?.includes(name) ? 0 : respec.value)
const forgettable = (a: AdventurerState) =>
  (a.disciplines ?? []).some((d) => !disciplineCatalog.data.value?.find((c) => c.id === d.id)?.story)
const knownCost = (name: string) =>
  disciplineCatalog.data.value?.find((d) => d.id === learning[name]?.discipline)?.cost != null
/** What the GM is about to teach each adventurer: the card, and its XP (printed cost +1 per card of that level). */
const learning = reactive<Record<string, { discipline: string | null; xp: number | null }>>({})
function draft(name: string) {
  return (learning[name] ??= { discipline: null, xp: null })
}
function pickDiscipline(name: string, id: string | null) {
  const entry = draft(name)
  entry.discipline = id
  const adventurer = props.campaign.party.find((a) => a.name === name)
  const card = disciplineCatalog.data.value?.find((d) => d.id === id)
  entry.xp = adventurer && card ? disciplineCost(card, adventurer, disciplineCatalog.data.value ?? []) : null
}
function learn(name: string) {
  const entry = draft(name)
  if (!entry.discipline || entry.xp == null) return
  emit('party', { type: 'learn', adventurer: name, discipline: entry.discipline, xp: entry.xp })
  learning[name] = { discipline: null, xp: null }
}

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
const bounties = computed(() => ({
  completed: props.campaign.bountiesCompleted ?? [],
  attempted: (props.campaign.bountiesAttempted ?? []).filter((b) => !props.campaign.bountiesCompleted?.includes(b)),
}))
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

    <div v-if="round.phase === 'shop'" class="block">
      <h3>{{ t('storyRound.upgradesTitle') }}</h3>
      <p class="hint">{{ t('storyRound.upgradesHint') }}</p>
      <p v-if="unlocked.length" class="chips">
        <Tag v-for="id in unlocked" :key="id" :value="upgradeName(id)" severity="secondary" />
      </p>
      <div class="row">
        <Select
          v-model="toUnlock"
          :options="lockedUpgrades"
          option-label="label"
          option-value="value"
          filter
          :placeholder="t('storyRound.unlockPlaceholder')"
          :aria-label="t('storyRound.unlockPlaceholder')"
          class="grow"
        />
        <Button
          :label="t('storyRound.unlock')"
          icon="pi pi-unlock"
          size="small"
          :disabled="busy || !toUnlock"
          @click="toUnlock && (emit('item', { type: 'unlockUpgrade', upgrade: toUnlock }), (toUnlock = null))"
        />
      </div>
    </div>

    <div v-if="round.phase === 'shop'" class="block">
      <h3>{{ t('storyRound.training') }}</h3>
      <p class="hint">{{ t('storyRound.trainingHint') }}</p>
      <div v-for="a in campaign.party" :key="a.name" class="trainee">
        <p class="who">
          <strong>{{ a.name }}</strong>
          <span class="xp">{{ t('storyRound.xpLeft', { left: xpLeft(a), total: a.xp }) }}</span>
        </p>
        <p v-if="a.disciplines?.length" class="chips">
          <Tag v-for="(d, i) in a.disciplines" :key="`${d.id}-${i}`" severity="secondary">
            {{ disciplineName(d.id) }} ({{ d.xp }} XP)
            <button
              type="button"
              class="forget"
              :aria-label="t('storyRound.forget', { name: disciplineName(d.id) })"
              :disabled="busy"
              @click="emit('party', { type: 'forget', adventurer: a.name, discipline: d.id })"
            >
              <i class="pi pi-times" aria-hidden="true" />
            </button>
          </Tag>
        </p>
        <div class="row">
          <Select
            :model-value="learning[a.name]?.discipline ?? null"
            :options="learnable(a)"
            option-label="label"
            option-value="value"
            option-disabled="disabled"
            filter
            :placeholder="t('storyRound.disciplinePlaceholder')"
            :aria-label="t('storyRound.disciplineFor', { name: a.name })"
            class="grow"
            @update:model-value="pickDiscipline(a.name, $event)"
          />
          <InputNumber
            :model-value="learning[a.name]?.xp ?? null"
            :min="0"
            :max="99"
            :placeholder="t('storyRound.xpCost')"
            :aria-label="t('storyRound.xpCostFor', { name: a.name })"
            input-class="xp-input"
            :disabled="knownCost(a.name)"
            @update:model-value="draft(a.name).xp = $event"
          />
          <Button
            :label="t('storyRound.learn')"
            icon="pi pi-plus"
            size="small"
            :disabled="busy || !learning[a.name]?.discipline || learning[a.name]?.xp == null || (learning[a.name]?.xp ?? 0) > xpLeft(a)"
            @click="learn(a.name)"
          />
          <Button
            v-if="forgettable(a)"
            :label="t('storyRound.respec', { price: respecFor(a.name) })"
            :title="t('storyRound.respecHint', { name: a.name, price: respecFor(a.name) })"
            icon="pi pi-refresh"
            size="small"
            severity="secondary"
            outlined
            :disabled="busy || campaign.gold < respecFor(a.name)"
            @click="emit('party', { type: 'respec', adventurer: a.name })"
          />
        </div>
      </div>
    </div>
    <p v-else-if="attempted.length" class="hint">
      {{ t('storyRound.attempted', { quests: attempted.map((q) => pick(q.label, language) || q.node).join(', ') }) }}
    </p>
    <p v-if="round.phase === 'explore' && (bounties.completed.length || bounties.attempted.length)" class="hint">
      <template v-if="bounties.completed.length">{{ t('storyRound.bountiesWon', { list: bounties.completed.join(', ') }) }} </template>
      <template v-if="bounties.attempted.length">{{ t('storyRound.bountiesLost', { list: bounties.attempted.join(', ') }) }}</template>
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
.row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.grow {
  flex: 1;
  min-width: 12rem;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  margin: 0 0 0.4rem;
}
.trainee + .trainee {
  margin-top: 0.6rem;
}
.who {
  margin: 0 0 0.25rem;
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
}
.forget {
  border: 0;
  background: none;
  cursor: pointer;
  padding: 0 0 0 0.25rem;
  color: inherit;
}
:deep(.xp-input) {
  width: 5rem;
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
