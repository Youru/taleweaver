<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import { useItems } from '@/features/catalog'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerCommand, PlayerRequest } from '../api/types'

/**
 * The Store of the Story Round (room-spec §5.1, Histoire; §6.2): its cards at their price, the party's gold. A
 * card touched shows in full; buying it is a request for the adventurer followed or for the stash.
 */
const props = defineProps<{
  shop: { item: string; price: number }[]
  gold: number
  adventurer: string
  requests?: PlayerRequest[]
  act?: (command: PlayerCommand) => Promise<void>
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const catalog = computed(() => new Map((items.data.value ?? []).map((i) => [i.id, i])))

const types = ['weapon', 'armor', 'core', 'accessory', 'relic', 'consumable']
const groups = computed(() =>
  types
    .map((type) => ({ type, cards: props.shop.filter((s) => catalog.value.get(s.item)?.type === type) }))
    .filter((g) => g.cards.length),
)

const shown = ref<{ item: string; price: number }>()
const card = computed(() => (shown.value ? catalog.value.get(shown.value.item) : undefined))
const busy = ref(false)
const failure = ref<string>()
const asked = computed(
  () =>
    new Set(
      (props.requests ?? []).flatMap((r) =>
        r.status === 'pending' && r.command.type === 'buyItem' ? [r.command.item] : [],
      ),
    ),
)

async function buy(owner: string) {
  if (!props.act || !shown.value) return
  busy.value = true
  failure.value = undefined
  try {
    await props.act({ type: 'buyItem', item: shown.value.item, owner, price: shown.value.price })
    shown.value = undefined
  } catch (e) {
    failure.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="shop">
    <p class="gold"><i class="pi pi-wallet" aria-hidden="true" /> {{ t('play.gold', { gold }) }}</p>
    <section v-for="group in groups" :key="group.type">
      <h3>{{ t(`room.shop.types.${group.type}`) }}</h3>
      <div class="grid">
        <button
          v-for="offer in group.cards"
          :key="offer.item"
          type="button"
          class="offer"
          :class="{ dear: offer.price > gold, asked: asked.has(offer.item) }"
          @click="((shown = offer), (failure = undefined))"
        >
          <img
            v-if="catalog.get(offer.item)?.image"
            :src="assetUrl(catalog.get(offer.item)?.image)"
            :alt="pick(catalog.get(offer.item)?.name, language)"
            loading="lazy"
          />
          <span class="price">{{ t('room.shop.price', { price: offer.price }) }}</span>
        </button>
      </div>
    </section>
    <Dialog
      :visible="!!shown"
      modal
      dismissable-mask
      :header="pick(card?.name, language)"
      class="card-viewer"
      @update:visible="(v: boolean) => !v && (shown = undefined)"
    >
      <img v-if="card?.image" :src="assetUrl(card.image)" :alt="pick(card.name, language)" />
      <div v-if="shown" class="actions">
        <p class="cost">{{ t('room.shop.cost', { price: shown.price, gold }) }}</p>
        <Message v-if="asked.has(shown.item)" severity="warn" :closable="false">{{
          t('room.request.waiting')
        }}</Message>
        <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
        <template v-if="act">
          <Button
            icon="pi pi-shopping-cart"
            :label="t('room.shop.buyFor', { name: adventurer })"
            :disabled="busy || shown.price > gold"
            @click="buy(adventurer)"
          />
          <Button
            icon="pi pi-inbox"
            :label="t('room.shop.buyForStash')"
            severity="secondary"
            :disabled="busy || shown.price > gold"
            @click="buy('stash')"
          />
        </template>
      </div>
    </Dialog>
  </div>
</template>

<style scoped>
.shop {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 0.75rem 1rem 1rem;
}
.gold {
  margin: 0;
  font-weight: 600;
}
h3 {
  font-size: 1rem;
  margin: 0.25rem 0 0.5rem;
}
.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
}
.offer {
  position: relative;
  padding: 0;
  border: 0;
  background: none;
  cursor: pointer;
}
.offer img {
  width: 100%;
  border-radius: 0.4rem;
  display: block;
}
.offer.dear img {
  opacity: 0.45;
}
.offer.asked img {
  outline: 2px dashed var(--seat, var(--p-primary-color));
}
.price {
  position: absolute;
  right: 0.25rem;
  bottom: 0.25rem;
  padding: 0.1rem 0.4rem;
  border-radius: 999px;
  background: rgb(0 0 0 / 70%);
  color: #fde68a;
  font-size: 0.75rem;
  font-weight: 700;
}
.card-viewer img {
  width: 100%;
  border-radius: 0.5rem;
  margin-bottom: 0.75rem;
}
.actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.actions :deep(.p-button) {
  min-height: 3rem;
}
.cost {
  margin: 0;
}
</style>
