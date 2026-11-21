<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import type { CatalogItem } from '@/features/catalog'
import type { OwnedItem } from '@/features/games'
import { assetUrl, useThumbnailWidth } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'

/** One card the party holds, as a small picture: the side shown, its name; a card still to draw is a dashed box. */
const props = defineProps<{ owned: OwnedItem; card: CatalogItem | undefined; selected: boolean }>()
/** A phone asks for a small copy (room-spec §10.3). */
const thumbnail = useThumbnailWidth()
defineEmits<{ select: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const image = computed(() =>
  assetUrl(props.owned.flipped ? (props.card?.imageBack ?? props.card?.image) : props.card?.image, thumbnail),
)
const name = computed(() =>
  props.owned.item
    ? pick(props.card?.name, language.value) || (props.owned.item.split('/').pop() ?? props.owned.item)
    : t('inventory.toDraw', {
        tier: (props.owned.tiers ?? []).map((tier) => t(`inventory.tier.${tier}`, tier)).join(' / ') || '?',
        type: props.owned.type ? t(`inventory.type.${props.owned.type}`, props.owned.type) : t('inventory.anyType'),
      }),
)
</script>

<template>
  <button
    type="button"
    class="item-card"
    :class="{ selected, draw: !owned.item }"
    :aria-pressed="selected"
    :title="name"
    @click="$emit('select')"
  >
    <img v-if="image" :src="image" :alt="name" loading="lazy" />
    <i v-else :class="owned.item ? 'pi pi-box' : 'pi pi-question-circle'" aria-hidden="true" />
    <span class="name">{{ name }}</span>
    <span v-if="owned.flipped" class="flag">{{ t('inventory.flipped') }}</span>
  </button>
</template>

<style scoped>
.item-card {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  width: 7.2rem;
  padding: 0.3rem;
  border: 2px solid transparent;
  border-radius: 0.5rem;
  background: var(--p-content-hover-background);
  color: inherit;
  cursor: pointer;
  font: inherit;
}
.item-card:hover {
  border-color: var(--p-content-border-color);
}
.item-card.selected {
  border-color: var(--p-primary-color);
}
.item-card.draw {
  border: 2px dashed var(--p-orange-400);
  min-height: 6rem;
  justify-content: center;
}
img {
  width: 100%;
  aspect-ratio: 5 / 7;
  object-fit: cover;
  border-radius: 0.3rem;
}
.pi {
  font-size: 2rem;
  color: var(--p-text-muted-color);
  padding: 1rem 0;
}
.name {
  font-size: 0.78rem;
  line-height: 1.15;
  text-align: center;
}
.flag {
  position: absolute;
  top: 0.4rem;
  right: 0.4rem;
  padding: 0 0.3rem;
  border-radius: 0.3rem;
  font-size: 0.7rem;
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
}
</style>
