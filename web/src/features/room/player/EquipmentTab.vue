<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import { useItems } from '@/features/catalog'
import type { AdventurerState, OwnedItem } from '@/features/games'
import { ItemCard, PlayerMat } from '@/features/inventory'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'

/** The player mat of the adventurer, read only: tap a card to see it in full, front or back. */
const props = defineProps<{ adventurer: AdventurerState; gold: number; stash: OwnedItem[] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const catalog = computed(() => new Map((items.data.value ?? []).map((i) => [i.id, i])))

const shown = ref<OwnedItem>()
const back = ref(false)
const card = computed(() => (shown.value?.item ? catalog.value.get(shown.value.item) : undefined))
const image = computed(() => assetUrl(back.value ? (card.value?.imageBack ?? card.value?.image) : card.value?.image))

function show(owned: OwnedItem) {
  shown.value = owned
  back.value = owned.flipped ?? false
}
</script>

<template>
  <div class="equipment">
    <p class="gold"><i class="pi pi-wallet" aria-hidden="true" /> {{ t('play.gold', { gold }) }}</p>
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
      <Button
        v-if="card?.imageBack"
        icon="pi pi-sync"
        :label="t('room.player.flip')"
        severity="secondary"
        @click="back = !back"
      />
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
.card-viewer img {
  width: 100%;
  border-radius: 0.5rem;
  margin-bottom: 0.75rem;
}
</style>
