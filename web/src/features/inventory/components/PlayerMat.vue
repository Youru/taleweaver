<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '@/features/catalog'
import type { AdventurerState, OwnedItem } from '@/features/games'
import { playerMat, type Place } from '../lib/inventory'
import ItemCard from './ItemCard.vue'

/**
 * An adventurer's equipment as on the table: a mat with a space per card the rules allow
 * (2 Weapons, 1 Armor, 1 Core, 1 Accessory, 3 Relics, 3 Consumables), empty spaces drawn,
 * cards over the limit in red; the Pack below with its spaces.
 */
const props = defineProps<{
  adventurer: AdventurerState
  catalog: Map<string, CatalogItem>
  isSelected: (owned: OwnedItem, place: Place) => boolean
}>()
const emit = defineEmits<{ select: [owned: OwnedItem] }>()
const { t } = useI18n()

const mat = computed(() => playerMat(props.adventurer, props.catalog))
const equippedPlace = computed<Place>(() => ({ owner: props.adventurer.name, slot: 'equipped' }))
const packPlace = computed<Place>(() => ({ owner: props.adventurer.name, slot: 'pack' }))
const label = (type: string) => (type === 'other' ? t('inventory.otherType') : t(`inventory.types.${type}`))
</script>

<template>
  <div class="mat">
    <header>
      <h3>{{ adventurer.name }}</h3>
      <span class="xp">{{ t('inventory.xp', { xp: adventurer.xp }) }}</span>
    </header>
    <div class="zones">
      <section v-for="row in mat.rows" :key="row.type" class="zone" :class="row.type">
        <h4>{{ label(row.type) }}</h4>
        <div class="slots">
          <template v-for="slot in row.slots" :key="slot.key">
            <div v-if="slot.owned" class="filled" :class="{ extra: slot.extra }">
              <ItemCard
                :owned="slot.owned"
                :card="slot.owned.item ? catalog.get(slot.owned.item) : undefined"
                :selected="isSelected(slot.owned, equippedPlace)"
                @select="emit('select', slot.owned)"
              />
            </div>
            <div v-else class="empty" :aria-label="t('inventory.emptySlot', { type: label(row.type) })" />
          </template>
        </div>
      </section>
    </div>
    <section class="zone pack">
      <h4>{{ t('inventory.packCount', { n: `${adventurer.pack?.length ?? 0}/${mat.packSpaces}` }) }}</h4>
      <div class="slots">
        <template v-for="slot in mat.pack" :key="slot.key">
          <div v-if="slot.owned" class="filled" :class="{ extra: slot.extra }">
            <ItemCard
              :owned="slot.owned"
              :card="slot.owned.item ? catalog.get(slot.owned.item) : undefined"
              :selected="isSelected(slot.owned, packPlace)"
              @select="emit('select', slot.owned)"
            />
          </div>
          <div v-else class="empty" :aria-label="t('inventory.emptySlot', { type: t('inventory.pack') })" />
        </template>
      </div>
    </section>
  </div>
</template>

<style scoped>
.mat {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 0.8rem 1rem 1rem;
  border: 3px double var(--mid-crimson);
  border-radius: 0.6rem;
  background:
    linear-gradient(rgb(251 245 232 / 0.55), rgb(227 210 174 / 0.55)),
    var(--mid-parchment) center / cover;
  box-shadow: inset 0 0 2.5rem rgb(60 35 15 / 0.25);
}
header {
  display: flex;
  align-items: baseline;
  gap: 0.75rem;
  margin: -0.8rem -1rem 0;
  padding: 0.35rem 1rem;
  background: var(--mid-banner) center / cover, var(--mid-crimson);
  border-radius: 0.4rem 0.4rem 0 0;
}
header h3 {
  margin: 0;
  color: var(--mid-cream);
  font-size: 1.1rem;
}
.xp {
  color: var(--mid-cream);
  font-size: 0.8rem;
  opacity: 0.85;
}
.zones {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
}
.zone {
  padding: 0.4rem 0.5rem 0.5rem;
  border: 1px solid rgb(109 26 26 / 0.35);
  border-radius: 0.45rem;
  background: rgb(255 250 240 / 0.35);
}
h4 {
  margin: 0 0 0.35rem;
  font-family: var(--mid-title-font);
  font-variant: small-caps;
  font-size: 0.85rem;
  color: var(--mid-crimson);
  letter-spacing: 0.04em;
}
.slots {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}
.slots > div {
  width: 4.8rem;
}
.filled :deep(.item-card) {
  width: 100%;
}
.filled.extra :deep(.item-card) {
  border-color: var(--p-red-500);
  background: rgb(220 38 38 / 0.12);
}
.empty {
  aspect-ratio: 5 / 7;
  border: 2px dashed rgb(109 26 26 / 0.35);
  border-radius: 0.4rem;
  background: rgb(109 26 26 / 0.04);
}
</style>
