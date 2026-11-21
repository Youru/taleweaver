<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import type { CatalogItem } from '@/features/catalog'
import type { ItemSlot, OwnedItem } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { itemTiers, itemTypes, searchItems } from '../lib/inventory'
import ItemStatsLine from './ItemStatsLine.vue'

/**
 * Find the card the players just got (bought, looted, drawn for an Item Loot) and hand it out:
 * to an adventurer (equipped or in the Pack), to the Party Stash, or set aside to hand out later.
 */
const props = defineProps<{
  items: CatalogItem[]
  gold: number
  /** Who can receive it: adventurers' names, "stash", "unassigned". */
  owners: { value: string; label: string }[]
  /** Where the card goes unless the GM says otherwise (the holder shown on the left). */
  owner: string
  /** The card still to draw this card is (Item Loot…): its tier and type preset the filters. */
  replacing: OwnedItem | null
  busy: boolean
}>()
const emit = defineEmits<{
  add: [
    item: string,
    owner: string,
    slot: ItemSlot | undefined,
    price: number | undefined,
    replaces: string | undefined,
  ]
  cancelReplace: []
}>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const query = ref('')
const type = ref<string | null>(null)
const tier = ref<string | null>(null)
const types = computed(() => itemTypes.map((value) => ({ value, label: t(`inventory.type.${value}`) })))
const tiers = computed(() => itemTiers.map((value) => ({ value, label: t(`inventory.tier.${value}`) })))
watch(
  () => props.replacing,
  (owned) => {
    if (!owned) return
    tier.value = owned.tiers?.length === 1 ? (owned.tiers[0] ?? null) : null
    type.value = owned.type ?? null
  },
  { immediate: true },
)

const results = computed(() =>
  searchItems(props.items, { query: query.value, type: type.value, tier: tier.value }, language.value),
)
const chosen = ref<CatalogItem | null>(null)
watch(results, (list) => {
  if (chosen.value && !list.includes(chosen.value)) chosen.value = null
})

const target = ref(props.owner)
watch(
  () => props.owner,
  (owner) => (target.value = owner),
)
const slot = ref<ItemSlot>('equipped')
const slots = computed(() => [
  { value: 'equipped', label: t('inventory.equipped') },
  { value: 'pack', label: t('inventory.pack') },
])
const toAdventurer = computed(() => target.value !== 'stash' && target.value !== 'unassigned')
/** Bought at the shop: its price comes out of the party's Gold. */
const pay = ref(false)
const price = ref<number | null>(null)
watch(chosen, (card) => (price.value = card?.cost ?? null))
const tooExpensive = computed(() => pay.value && (price.value ?? 0) > props.gold)

function add() {
  if (!chosen.value) return
  emit(
    'add',
    chosen.value.id,
    target.value,
    toAdventurer.value ? slot.value : undefined,
    pay.value && price.value ? price.value : undefined,
    props.replacing?.id,
  )
  chosen.value = null
  query.value = ''
}
</script>

<template>
  <section class="search" :aria-label="t('inventory.addTitle')">
    <h3>{{ replacing ? t('inventory.drawTitle') : t('inventory.addTitle') }}</h3>
    <Message v-if="replacing" severity="warn" size="small" class="replacing">
      <span>{{ t('inventory.drawHint') }}</span>
      <Button :label="t('common.cancel')" size="small" text @click="emit('cancelReplace')" />
    </Message>
    <IconField>
      <InputIcon class="pi pi-search" />
      <InputText
        v-model="query"
        :placeholder="t('inventory.searchPlaceholder')"
        :aria-label="t('inventory.search')"
        fluid
        autofocus
      />
    </IconField>
    <div class="filters">
      <Select
        v-model="type"
        :options="types"
        option-label="label"
        option-value="value"
        show-clear
        :placeholder="t('inventory.allTypes')"
        :aria-label="t('inventory.typeFilter')"
        size="small"
      />
      <Select
        v-model="tier"
        :options="tiers"
        option-label="label"
        option-value="value"
        show-clear
        :placeholder="t('inventory.allTiers')"
        :aria-label="t('inventory.tierFilter')"
        size="small"
      />
    </div>
    <ul class="results" role="listbox" :aria-label="t('inventory.results')">
      <li v-for="card in results" :key="card.id">
        <button
          type="button"
          role="option"
          :aria-selected="chosen?.id === card.id"
          :class="{ chosen: chosen?.id === card.id }"
          @click="chosen = card"
        >
          <img v-if="card.image" :src="assetUrl(card.image)" alt="" loading="lazy" />
          <span class="label">
            <strong>{{ pick(card.name, language) }}</strong>
            <small>
              {{ t(`inventory.type.${card.type}`, card.type) }}
              <template v-if="card.tier"> · {{ t(`inventory.tier.${card.tier}`, card.tier) }}</template>
              <template v-if="card.unique"> · {{ t('inventory.unique') }}</template>
            </small>
            <ItemStatsLine :stats="card.stats" />
          </span>
          <span v-if="card.cost" class="cost">{{ t('inventory.gold', { gold: card.cost }) }}</span>
        </button>
      </li>
      <li v-if="!results.length" class="empty">{{ t('inventory.noResult') }}</li>
    </ul>

    <div v-if="chosen" class="give">
      <div class="chosen-card">
        <img v-if="chosen.image" :src="assetUrl(chosen.image)" :alt="pick(chosen.name, language)" />
        <div class="text">
          <ItemStatsLine :stats="chosen.stats" />
          <p>{{ pick(chosen.text, language) }}</p>
        </div>
      </div>
      <label class="field">
        <span>{{ t('inventory.giveTo') }}</span>
        <Select v-model="target" :options="owners" option-label="label" option-value="value" size="small" />
      </label>
      <SelectButton
        v-if="toAdventurer"
        v-model="slot"
        :options="slots"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        size="small"
        :aria-label="t('inventory.slot')"
      />
      <div v-if="!replacing" class="pay">
        <Checkbox v-model="pay" binary input-id="inventory-pay" />
        <label for="inventory-pay">{{ t('inventory.pay') }}</label>
        <InputNumber
          v-if="pay"
          v-model="price"
          :min="0"
          :max="100000"
          size="small"
          :input-style="{ width: '5rem' }"
          :aria-label="t('inventory.price')"
          suffix=" or"
        />
        <small>{{ t('inventory.partyGold', { gold }) }}</small>
      </div>
      <Message v-if="tooExpensive" severity="error" size="small">{{ t('inventory.tooExpensive') }}</Message>
      <Button
        icon="pi pi-plus"
        :label="replacing ? t('inventory.thisCard') : t('inventory.add')"
        :disabled="busy || tooExpensive"
        @click="add"
      />
    </div>
  </section>
</template>

<style scoped>
.search {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-height: 0;
}
h3 {
  margin: 0;
  font-size: 1rem;
}
.replacing :deep(.p-message-text) {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.filters {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
}
.filters > * {
  flex: 1;
  min-width: 8rem;
}
.results {
  list-style: none;
  margin: 0;
  padding: 0;
  overflow-y: auto;
  min-height: 8rem;
  max-height: 18rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0.4rem;
}
.results button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.25rem 0.5rem;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}
.results button:hover {
  background: var(--p-content-hover-background);
}
.results button.chosen {
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
}
.results img {
  width: 2rem;
  aspect-ratio: 5 / 7;
  object-fit: cover;
  border-radius: 0.2rem;
}
.label {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}
.label small,
.empty,
.pay small {
  color: var(--p-text-muted-color);
}
.empty {
  padding: 0.5rem;
}
.cost {
  font-size: 0.85rem;
  white-space: nowrap;
}
.give {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding-top: 0.25rem;
}
.chosen-card {
  display: flex;
  gap: 0.6rem;
}
.chosen-card img {
  width: 7rem;
  border-radius: 0.3rem;
  align-self: flex-start;
}
.text p {
  margin: 0.3rem 0 0;
}
.text {
  margin: 0;
  white-space: pre-line;
  font-size: 0.8rem;
  max-height: 10rem;
  overflow-y: auto;
}
.field {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.pay {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
</style>
