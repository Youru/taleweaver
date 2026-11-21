<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Badge from 'primevue/badge'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useItems, type CatalogItem } from '@/features/catalog'
import type { CampaignState, EncounterState, ItemCommand, ItemSlot, OwnedItem } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import { allItems, equippedCounts, limitWarnings, samePlace, type Place } from '../lib/inventory'
import ItemCard from './ItemCard.vue'
import ItemDetail from './ItemDetail.vue'
import ItemSearch from './ItemSearch.vue'
import PlayerMat from './PlayerMat.vue'
import { storeToRefs } from 'pinia'
import { usePreferences } from '@/shared/lib/preferences'

/**
 * The party's items, in their own window (like the duel): what each adventurer has equipped and in
 * their Pack, the Party Stash, and what was gained but not handed out yet (loot, rewards). The GM finds
 * the card the players just got in the catalogue and hands it out; picks a card to move, use or sell it.
 */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{
  campaign: CampaignState
  encounter?: EncounterState
  node?: ScenarioNode
  /** The holder to show first (an adventurer's name, "stash", "unassigned"). */
  startWith?: string | null
  busy: boolean
  /** The last command was refused: why. */
  error?: string | null
  /** In a Story Round: the ids of the cards for sale in its Store (the search shows them first). */
  store?: string[] | null
}>()
const emit = defineEmits<{ command: [command: ItemCommand] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const catalog = useItems()
const byId = computed(() => new Map<string, CatalogItem>((catalog.data.value ?? []).map((i) => [i.id, i])))

/** The Store's cards, or the whole catalogue (the GM may switch, to hand out a card the story gives). */
const storeOnly = ref(true)
const searchable = computed(() => {
  const all = catalog.data.value ?? []
  if (!props.store || !storeOnly.value) return all
  const forSale = new Set(props.store)
  return all.filter((i) => forSale.has(i.id))
})

const unassigned = computed(() => props.campaign.unassigned ?? [])
/** The tabs on the left: items to hand out (only when there are some), each adventurer, the stash. */
const holders = computed(() => [
  ...(unassigned.value.length
    ? [{ value: 'unassigned', label: t('inventory.unassigned'), count: unassigned.value.length }]
    : []),
  ...props.campaign.party.map((a) => ({
    value: a.name,
    label: a.name,
    count: (a.equipped?.length ?? 0) + (a.pack?.length ?? 0),
  })),
  { value: 'stash', label: t('inventory.stash'), count: props.campaign.stash?.length ?? 0 },
])
/** Where a card can go. */
const owners = computed(() => [
  ...props.campaign.party.map((a) => ({ value: a.name, label: a.name })),
  { value: 'stash', label: t('inventory.stash') },
  { value: 'unassigned', label: t('inventory.unassigned') },
])

const current = ref<string>('unassigned')
watch(
  () => [open.value, props.startWith] as const,
  ([isOpen, wanted]) => {
    if (!isOpen) return
    const first = holders.value[0]?.value ?? 'stash'
    current.value = wanted && holders.value.some((h) => h.value === wanted) ? wanted : first
    selectedId.value = null
    replacing.value = null
  },
  { immediate: true },
)
// The last item handed out: the tab disappears, show the first adventurer
watch(holders, (list) => {
  if (!list.some((h) => h.value === current.value)) current.value = list[0]?.value ?? 'stash'
})

const adventurer = computed(() => props.campaign.party.find((a) => a.name === current.value))
const sections = computed<{ title: string; place: Place; items: OwnedItem[] }[]>(() => {
  if (current.value === 'unassigned') return [{ title: '', place: { owner: 'unassigned' }, items: unassigned.value }]
  if (current.value === 'stash') return [{ title: '', place: { owner: 'stash' }, items: props.campaign.stash ?? [] }]
  // An adventurer: shown as a player mat (PlayerMat)
  return []
})
const counts = computed(() => (adventurer.value ? equippedCounts(adventurer.value, byId.value) : []))
const warnings = computed(() =>
  (adventurer.value ? limitWarnings(adventurer.value, byId.value, language.value) : []).map((w) =>
    w.kind === 'over'
      ? t('inventory.warnOver', { type: t(`inventory.types.${w.type}`), count: w.count, max: w.max })
      : w.kind === 'pack'
        ? t('inventory.warnPack', { count: w.count, max: w.max })
        : t('inventory.warnRelic', { name: w.name }),
  ),
)

const selectedId = ref<string | null>(null)
const selected = computed(() => allItems(props.campaign).find((l) => l.owned.id === selectedId.value))
// A card that left the party (used, sold): nothing selected any more
watch(selected, (now) => {
  if (!now) selectedId.value = null
})
/** The card still to draw that the search is choosing a card for. */
const replacing = ref<OwnedItem | null>(null)
watch(
  () => props.campaign,
  (campaign) => {
    if (replacing.value && !campaign.unassigned?.some((o) => o.id === replacing.value?.id)) replacing.value = null
  },
)
const addTo = computed(() =>
  current.value === 'unassigned' ? (props.campaign.party[0]?.name ?? 'stash') : current.value,
)

function add(
  item: string,
  owner: string,
  slot: ItemSlot | undefined,
  price: number | undefined,
  replaces: string | undefined,
) {
  emit('command', { type: 'add', item, owner, slot, price, replaces })
}

function isSelected(owned: OwnedItem, place: Place) {
  return selectedId.value === owned.id && !!selected.value && samePlace(selected.value.place, place)
}
</script>

<template>
  <Dialog
    v-model:visible="open"
    modal
    dismissable-mask
    :draggable="false"
    :breakpoints="{ '1000px': '100vw' }"
    class="inventory-dialog"
    :pt="{ content: { class: 'inventory-content' } }"
  >
    <template #header>
      <div class="bar">
        <h2>{{ t('inventory.title') }}</h2>
        <span class="gold"
          ><i class="pi pi-circle-fill" aria-hidden="true" /> {{ t('inventory.gold', { gold: campaign.gold }) }}</span
        >
      </div>
    </template>

    <Message v-if="error" severity="error" size="small">{{ error }}</Message>
    <div v-if="catalog.isPending.value" class="center"><ProgressSpinner :aria-label="t('common.loading')" /></div>
    <Message v-else-if="catalog.isError.value" severity="error">
      {{ t('common.error', { message: catalog.error.value?.message ?? '' }) }}
    </Message>
    <div v-else class="layout">
      <section class="holders">
        <nav class="tabs" :aria-label="t('inventory.holders')">
          <button
            v-for="h in holders"
            :key="h.value"
            type="button"
            :class="{ active: current === h.value, alert: h.value === 'unassigned' }"
            :aria-pressed="current === h.value"
            @click="((current = h.value), (selectedId = null))"
          >
            {{ h.label }}
            <Badge
              v-if="h.count"
              :value="h.count"
              size="small"
              :severity="h.value === 'unassigned' ? 'warn' : 'secondary'"
            />
          </button>
        </nav>

        <p v-if="current === 'unassigned'" class="explain">{{ t('inventory.unassignedHint') }}</p>
        <p v-else-if="current === 'stash'" class="explain">{{ t('inventory.stashHint') }}</p>
        <div v-if="counts.length" class="counts">
          <span v-for="c in counts" :key="c.type" :class="{ over: c.count > c.max }">
            {{ t(`inventory.types.${c.type}`) }} {{ c.count }}/{{ c.max }}
          </span>
        </div>
        <Message v-for="w in warnings" :key="w" severity="warn" size="small">{{ w }}</Message>

        <PlayerMat
          v-if="adventurer"
          :adventurer="adventurer"
          :catalog="byId"
          :is-selected="isSelected"
          @select="selectedId = $event.id"
        />
        <div v-for="section in sections" :key="section.title" class="section">
          <h3 v-if="section.title">{{ section.title }}</h3>
          <div class="grid">
            <ItemCard
              v-for="owned in section.items"
              :key="owned.id"
              :owned="owned"
              :card="owned.item ? byId.get(owned.item) : undefined"
              :selected="isSelected(owned, section.place)"
              @select="selectedId = owned.id"
            />
            <p v-if="!section.items.length" class="empty">{{ t('inventory.empty') }}</p>
          </div>
        </div>
      </section>

      <aside class="panel">
        <ItemDetail
          v-if="selected && !replacing"
          :key="selected.owned.id"
          :owned="selected.owned"
          :card="selected.owned.item ? byId.get(selected.owned.item) : undefined"
          :place="selected.place"
          :owners="owners"
          :encounter="encounter"
          :node="node"
          :busy="busy"
          @command="emit('command', $event)"
          @draw="replacing = selected.owned"
          @close="selectedId = null"
        />
        <div v-if="store && !(selected && !replacing)" class="store-switch">
          <Button
            :label="t('storyRound.storeOnly', { n: store.length })"
            icon="pi pi-shop"
            size="small"
            :severity="storeOnly ? undefined : 'secondary'"
            :aria-pressed="storeOnly"
            @click="storeOnly = true"
          />
          <Button
            :label="t('storyRound.wholeCatalogue')"
            icon="pi pi-book"
            size="small"
            :severity="storeOnly ? 'secondary' : undefined"
            :aria-pressed="!storeOnly"
            @click="storeOnly = false"
          />
        </div>
        <ItemSearch
          v-if="!(selected && !replacing)"
          :items="searchable"
          :gold="campaign.gold"
          :owners="owners"
          :owner="addTo"
          :replacing="replacing"
          :busy="busy"
          @add="add"
          @cancel-replace="replacing = null"
        />
      </aside>
    </div>
  </Dialog>
</template>

<style scoped>
.store-switch {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin-bottom: 0.6rem;
}
.bar {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex: 1;
}
h2 {
  margin: 0;
  font-size: 1.25rem;
}
.gold {
  color: var(--p-text-muted-color);
}
.gold .pi {
  color: var(--p-yellow-500);
  font-size: 0.7rem;
}
.center {
  display: flex;
  justify-content: center;
  padding: 2rem;
}
.layout {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(19rem, 2fr);
  gap: 1rem;
  flex: 1;
  min-height: 0;
}
.holders,
.panel {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  min-height: 0;
  overflow-y: auto;
}
.panel {
  padding-left: 1rem;
  border-left: 1px solid var(--p-content-border-color);
}
.tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}
.tabs button {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.35rem 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 999px;
  background: none;
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.tabs button.active {
  background: var(--p-primary-color);
  border-color: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
}
.tabs button.alert:not(.active) {
  border-color: var(--p-orange-400);
}
.explain,
.empty {
  margin: 0;
  color: var(--p-text-muted-color);
  font-size: 0.85rem;
}
.counts {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  font-size: 0.85rem;
}
.counts .over {
  color: var(--p-red-500);
  font-weight: 600;
}
.section h3 {
  margin: 0 0 0.35rem;
  font-size: 0.95rem;
}
.grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
@media (max-width: 1000px) {
  .layout {
    grid-template-columns: 1fr;
  }
  .panel {
    padding-left: 0;
    border-left: 0;
    border-top: 1px solid var(--p-content-border-color);
    padding-top: 0.75rem;
  }
}
</style>

<style>
.inventory-dialog {
  width: min(78rem, 96vw);
  height: min(52rem, 94vh);
}
.inventory-content {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
@media (max-width: 1000px) {
  .inventory-dialog {
    height: 100vh;
    max-height: 100vh;
    border-radius: 0;
  }
  .inventory-content {
    overflow-y: auto;
  }
}
</style>
