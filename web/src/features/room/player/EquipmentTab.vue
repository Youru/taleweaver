<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import { useItems, useItemUpgrades } from '@/features/catalog'
import type { AdventurerState, OwnedItem } from '@/features/games'
import { ItemCard, PlayerMat } from '@/features/inventory'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerCommand, PlayerRequest, PlayerView } from '../api/types'
import RequestList from '../components/RequestList.vue'

/**
 * The player mat of the adventurer: tap a card to see it in full, front or back, and ask for what to do with it
 * (room-spec §6.2): equip it, put it in the pack or the stash, hand it to another member, flip it, use it.
 * Without `act`, read only.
 */
const props = defineProps<{
  adventurer: AdventurerState
  gold: number
  stash: OwnedItem[]
  /** The Item Upgrades for sale now. */
  upgrades?: PlayerView['upgrades']
  /** The other members of the party, who may be handed a card. */
  others?: string[]
  /** The seat's requests (pending ones first to cancel). */
  requests?: PlayerRequest[]
  act?: (command: PlayerCommand) => Promise<void>
  cancel?: (request: string) => Promise<void>
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const catalog = computed(() => new Map((items.data.value ?? []).map((i) => [i.id, i])))

const shown = ref<OwnedItem>()
const back = ref(false)
const card = computed(() => (shown.value?.item ? catalog.value.get(shown.value.item) : undefined))
const image = computed(() =>
  assetUrl(back.value ? (card.value?.imageBack ?? card.value?.image) : card.value?.image, 1280),
)

function show(owned: OwnedItem) {
  shown.value = owned
  back.value = owned.flipped ?? false
  failure.value = undefined
}

/** Where the card shown is: on the mat, in the pack, in the stash. */
const place = computed(() => {
  const id = shown.value?.id
  if ((props.adventurer.equipped ?? []).some((i) => i.id === id)) return 'equipped'
  if ((props.adventurer.pack ?? []).some((i) => i.id === id)) return 'pack'
  return 'stash'
})
const waiting = computed(() =>
  (props.requests ?? []).some(
    (r) => r.status === 'pending' && 'item' in r.command && r.command.item === shown.value?.id,
  ),
)
const itemName = (id: string) => {
  const owned = [...(props.adventurer.equipped ?? []), ...(props.adventurer.pack ?? []), ...props.stash].find(
    (i) => i.id === id,
  )
  return pick(catalog.value.get(owned?.item ?? id)?.name, language.value) || id
}

const upgradeCards = useItemUpgrades()
const upgradeName = (id: string) =>
  pick(upgradeCards.data.value?.find((u) => u.id === id)?.name, language.value) || (id.split('/').pop() ?? id)
const offers = computed(() =>
  (props.upgrades ?? []).flatMap(({ upgrade, prices }) => {
    const price = prices[card.value?.tier ?? '']
    return price === undefined || shown.value?.upgrades?.includes(upgrade) ? [] : [{ upgrade, price }]
  }),
)

const busy = ref(false)
const failure = ref<string>()
async function ask(command: PlayerCommand) {
  if (!props.act) return
  busy.value = true
  failure.value = undefined
  try {
    await props.act(command)
    shown.value = undefined
  } catch (e) {
    failure.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
function move(owner: string, slot?: 'equipped' | 'pack') {
  if (shown.value) void ask({ type: 'moveItem', item: shown.value.id, owner, slot })
}
</script>

<template>
  <div class="equipment">
    <p class="gold"><i class="pi pi-wallet" aria-hidden="true" /> {{ t('play.gold', { gold }) }}</p>
    <section v-if="requests?.length" class="mine">
      <h3>{{ t('room.request.mine') }}</h3>
      <RequestList :requests="[...requests].reverse()" :item-name="itemName" @cancel="(id) => cancel?.(id)" />
    </section>
    <PlayerMat :adventurer="props.adventurer" :catalog="catalog" :is-selected="() => false" @select="show" />
    <section v-if="stash.length">
      <h3>{{ t('room.player.stash') }}</h3>
      <div class="stash">
        <ItemCard
          v-for="owned in stash"
          :key="owned.id"
          :owned="owned"
          :card="owned.item ? catalog.get(owned.item) : undefined"
          :selected="false"
          @select="show(owned)"
        />
      </div>
    </section>
    <Dialog
      :visible="!!shown"
      modal
      dismissable-mask
      :header="pick(card?.name, language) || t('room.player.cardToDraw')"
      class="card-viewer"
      @update:visible="(v: boolean) => !v && (shown = undefined)"
    >
      <img v-if="image" :src="image" :alt="pick(card?.name, language)" />
      <p v-if="shown?.upgrades?.length" class="attached">
        <i class="pi pi-wrench" aria-hidden="true" /> {{ shown.upgrades.map(upgradeName).join(', ') }}
      </p>
      <Button
        v-if="card?.imageBack"
        icon="pi pi-sync"
        :label="t('room.player.flip')"
        severity="secondary"
        @click="back = !back"
      />
      <div v-if="act && shown" class="actions">
        <Message v-if="waiting" severity="warn" :closable="false">{{ t('room.request.waiting') }}</Message>
        <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
        <Button
          v-if="place !== 'equipped'"
          icon="pi pi-check-square"
          :label="t('room.request.doEquip')"
          :disabled="busy || waiting"
          @click="move(adventurer.name, 'equipped')"
        />
        <Button
          v-if="place !== 'pack'"
          icon="pi pi-briefcase"
          :label="t('room.request.doPack')"
          severity="secondary"
          :disabled="busy || waiting"
          @click="move(adventurer.name, 'pack')"
        />
        <Button
          v-if="place !== 'stash'"
          icon="pi pi-inbox"
          :label="t('room.request.doStash')"
          severity="secondary"
          :disabled="busy || waiting"
          @click="move('stash')"
        />
        <Button
          v-if="card?.imageBack && place === 'equipped'"
          icon="pi pi-refresh"
          :label="shown.flipped ? t('room.request.doUnflip') : t('room.request.doFlip')"
          severity="secondary"
          :disabled="busy || waiting"
          @click="ask({ type: 'flipItem', item: shown.id, flipped: !shown.flipped })"
        />
        <Button
          v-if="card?.type === 'consumable' && place === 'equipped'"
          icon="pi pi-bolt"
          :label="t('room.request.doUse')"
          severity="secondary"
          :disabled="busy || waiting"
          @click="ask({ type: 'useItem', item: shown.id, discard: true })"
        />
        <template v-if="offers.length">
          <p class="give">{{ t('room.request.upgradeTitle') }}</p>
          <Button
            v-for="offer in offers"
            :key="offer.upgrade"
            icon="pi pi-wrench"
            :label="t('room.request.doUpgrade', { upgrade: upgradeName(offer.upgrade), price: offer.price })"
            severity="secondary"
            outlined
            :disabled="busy || waiting || offer.price > gold"
            @click="ask({ type: 'attachUpgrade', item: shown.id, upgrade: offer.upgrade, price: offer.price })"
          />
        </template>
        <template v-if="others?.length">
          <p class="give">{{ t('room.request.giveTo') }}</p>
          <div class="others">
            <Button
              v-for="name in others"
              :key="name"
              :label="name"
              severity="secondary"
              outlined
              size="small"
              :disabled="busy || waiting"
              @click="move(name, 'pack')"
            />
          </div>
        </template>
      </div>
    </Dialog>
  </div>
</template>

<style scoped>
.equipment {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.gold {
  margin: 0;
  font-weight: 600;
}
.stash {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
}
h3 {
  font-size: 1rem;
  margin: 0.5rem 0;
}
.actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.75rem;
}
.actions :deep(.p-button) {
  min-height: 3rem;
}
.give {
  margin: 0.25rem 0 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.others {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}
.card-viewer img {
  width: 100%;
  border-radius: 0.5rem;
  margin-bottom: 0.75rem;
}
</style>
