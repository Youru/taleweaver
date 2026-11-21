<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import type { CatalogItem } from '@/features/catalog'
import type { Cell, EncounterState, ItemCommand, ItemSlot, OwnedItem } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import { BoardCanvas, boardLayout, useBoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { discardedOnUse, salePrice, suggestedToken, type Place } from '../lib/inventory'

/**
 * A card the party holds: read it (both sides), hand it to someone else, flip it, use it (a Consumable is
 * discarded, a Plank put on the board), sell or discard it. A card still to draw asks for its card first.
 */
const props = defineProps<{
  owned: OwnedItem
  card: CatalogItem | undefined
  place: Place
  owners: { value: string; label: string }[]
  /** The encounter in progress, to place the token an item puts on the board. */
  encounter?: EncounterState
  node?: ScenarioNode
  busy: boolean
}>()
const emit = defineEmits<{ command: [command: ItemCommand]; draw: []; close: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const pieces = useBoardPieces()

const name = computed(() => pick(props.card?.name, language.value) || props.owned.item || '')
const side = computed(() =>
  props.owned.flipped && props.card?.backSide
    ? {
        image: props.card.imageBack ?? props.card.image,
        text: props.card.backSide.text,
        tags: props.card.backSide.tags,
      }
    : {
        image: props.owned.flipped ? (props.card?.imageBack ?? props.card?.image) : props.card?.image,
        text: props.card?.text,
        tags: props.card?.tags ?? [],
      },
)

// Hand it to someone else
const target = ref(props.place.owner)
const slot = ref<ItemSlot>('slot' in props.place ? props.place.slot : 'equipped')
watch(
  () => [props.owned.id, props.place] as const,
  () => {
    target.value = props.place.owner
    slot.value = 'slot' in props.place ? props.place.slot : 'equipped'
    using.value = false
    selling.value = false
  },
)
const slots = computed(() => [
  { value: 'equipped', label: t('inventory.equipped') },
  { value: 'pack', label: t('inventory.pack') },
])
const toAdventurer = computed(() => target.value !== 'stash' && target.value !== 'unassigned')
const unchanged = computed(
  () =>
    target.value === props.place.owner &&
    (!toAdventurer.value || ('slot' in props.place && props.place.slot === slot.value)),
)
function move() {
  emit('command', {
    type: 'move',
    id: props.owned.id,
    owner: target.value,
    slot: toAdventurer.value ? slot.value : undefined,
  })
}
/** One click for the usual move of an adventurer's card: equip what is in the Pack, store what is equipped. */
const swapSlot = computed<ItemSlot | null>(() =>
  'slot' in props.place ? (props.place.slot === 'pack' ? 'equipped' : 'pack') : null,
)

// Use it
const using = ref(false)
const token = computed(() => suggestedToken(props.card, pieces.data.value?.tokens))
const placeToken = ref(true)
const discard = ref(false)
const at = ref<Cell | null>(null)
function startUse() {
  discard.value = discardedOnUse(props.card)
  placeToken.value = !!token.value && !!props.encounter
  at.value = null
  using.value = true
}
const holder = computed(() =>
  props.encounter?.adventurers.find((a) => a.name.toLowerCase() === props.place.owner.toLowerCase()),
)
const tokenSize = computed(() => {
  const [w = 1, h = 1] = (pieces.data.value?.tokens[token.value ?? '']?.size ?? '1x1').split('x').map(Number)
  return { w, h }
})
/** The board with the token where it would go, so the GM sees it before confirming. */
const layout = computed(() => {
  if (!props.encounter) return undefined
  const preview =
    at.value && token.value
      ? [
          {
            id: 'preview',
            key: 'preview',
            type: token.value,
            area: { ...at.value, ...tokenSize.value },
            removed: false,
          },
        ]
      : []
  return boardLayout(
    props.node,
    { ...props.encounter, tokens: [...props.encounter.tokens, ...preview] },
    pieces.data.value,
  )
})
const focus = computed(() => {
  const position = at.value ?? holder.value?.position
  return position ? { x: position.x - 3, y: position.y - 2, w: 7, h: 5 } : undefined
})
const marks = computed(() => (holder.value ? { attacker: `adventurer:${holder.value.name}` } : undefined))
const needsSquare = computed(() => placeToken.value && !!token.value && !at.value)
function use() {
  emit('command', {
    type: 'use',
    id: props.owned.id,
    discard: discard.value,
    ...(placeToken.value && token.value && at.value ? { token: token.value, at: at.value } : {}),
  })
  using.value = false
}

// Sell or discard it
const selling = ref(false)
const gold = ref<number | null>(null)
function startSell() {
  gold.value = salePrice(props.card) ?? 0
  selling.value = true
}
function sell() {
  emit('command', { type: 'discard', id: props.owned.id, gold: gold.value ?? 0 })
}
</script>

<template>
  <section class="detail" :aria-label="name">
    <header>
      <h3>{{ name }}</h3>
      <Button icon="pi pi-times" text rounded size="small" :aria-label="t('common.close')" @click="emit('close')" />
    </header>

    <div v-if="!owned.item" class="draw">
      <p>{{ t('inventory.drawExplain') }}</p>
      <Button icon="pi pi-search" :label="t('inventory.drawCard')" @click="emit('draw')" />
    </div>

    <template v-else>
      <div class="card">
        <img v-if="side.image" :src="assetUrl(side.image)" :alt="name" />
        <div class="text">
          <p v-if="card" class="meta">
            {{ t(`inventory.type.${card.type}`, card.type) }}
            <template v-if="card.tier"> · {{ t(`inventory.tier.${card.tier}`, card.tier) }}</template>
            <template v-if="side.tags.length"> · {{ side.tags.join(' • ') }}</template>
          </p>
          <p>{{ pick(side.text, language) }}</p>
        </div>
      </div>

      <div class="actions">
        <Button
          v-if="swapSlot"
          :icon="swapSlot === 'equipped' ? 'pi pi-shield' : 'pi pi-briefcase'"
          :label="swapSlot === 'equipped' ? t('inventory.equip') : t('inventory.store')"
          size="small"
          :disabled="busy"
          @click="emit('command', { type: 'move', id: owned.id, owner: place.owner, slot: swapSlot })"
        />
        <Button
          icon="pi pi-sync"
          :label="owned.flipped ? t('inventory.unflip') : t('inventory.flip')"
          size="small"
          severity="secondary"
          :disabled="busy"
          @click="emit('command', { type: 'flip', id: owned.id, flipped: !owned.flipped })"
        />
        <Button
          icon="pi pi-bolt"
          :label="t('inventory.use')"
          size="small"
          severity="secondary"
          :disabled="busy"
          @click="startUse"
        />
        <Button
          icon="pi pi-dollar"
          :label="t('inventory.sell')"
          size="small"
          severity="secondary"
          :disabled="busy"
          @click="startSell"
        />
        <Button
          icon="pi pi-trash"
          :label="t('inventory.discard')"
          size="small"
          severity="danger"
          text
          :disabled="busy"
          @click="emit('command', { type: 'discard', id: owned.id })"
        />
      </div>

      <div v-if="using" class="use">
        <h4>{{ t('inventory.useTitle', { name }) }}</h4>
        <div v-if="token && encounter" class="check">
          <Checkbox v-model="placeToken" binary input-id="inventory-place-token" />
          <label for="inventory-place-token">
            {{ t('inventory.placeToken', { token: pick(pieces.data.value?.tokens[token]?.name, language) }) }}
          </label>
        </div>
        <template v-if="placeToken && token && layout">
          <BoardCanvas
            class="minimap"
            :layout="layout"
            :selected="null"
            :focus="focus"
            :marks="marks"
            readonly
            :label="t('inventory.boardLabel')"
            @tap="at = $event"
          />
          <p class="hint">{{ at ? t('inventory.squareChosen', at) : t('inventory.tapSquare') }}</p>
        </template>
        <p v-else-if="token && !encounter" class="hint">{{ t('inventory.noEncounter') }}</p>
        <div class="check">
          <Checkbox v-model="discard" binary input-id="inventory-discard-used" />
          <label for="inventory-discard-used">{{ t('inventory.discardAfter') }}</label>
        </div>
        <div class="row">
          <Button
            icon="pi pi-check"
            :label="t('inventory.confirmUse')"
            size="small"
            :disabled="busy || needsSquare"
            @click="use"
          />
          <Button :label="t('common.cancel')" size="small" text @click="using = false" />
        </div>
      </div>

      <div v-if="selling" class="use">
        <h4>{{ t('inventory.sellTitle', { name }) }}</h4>
        <div class="row">
          <InputNumber
            v-model="gold"
            :min="0"
            :max="100000"
            size="small"
            suffix=" or"
            :aria-label="t('inventory.salePrice')"
          />
          <Button icon="pi pi-check" :label="t('inventory.confirmSell')" size="small" :disabled="busy" @click="sell" />
          <Button :label="t('common.cancel')" size="small" text @click="selling = false" />
        </div>
        <p class="hint">{{ t('inventory.saleHint') }}</p>
      </div>
    </template>

    <div class="give">
      <h4>{{ t('inventory.giveTo') }}</h4>
      <div class="row">
        <Select
          v-model="target"
          :options="owners"
          option-label="label"
          option-value="value"
          size="small"
          :aria-label="t('inventory.giveTo')"
        />
        <Select
          v-if="toAdventurer"
          v-model="slot"
          :options="slots"
          option-label="label"
          option-value="value"
          size="small"
          :aria-label="t('inventory.slot')"
        />
        <Button
          icon="pi pi-arrow-right"
          :label="t('inventory.move')"
          size="small"
          :disabled="busy || unchanged"
          @click="move"
        />
      </div>
    </div>
  </section>
</template>

<style scoped>
.detail {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  min-height: 0;
}
header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
h3,
h4 {
  margin: 0;
}
h3 {
  font-size: 1.05rem;
}
h4 {
  font-size: 0.9rem;
}
.card {
  display: flex;
  gap: 0.75rem;
}
.card img {
  width: 11rem;
  max-width: 45%;
  align-self: flex-start;
  border-radius: 0.4rem;
}
.text {
  flex: 1;
  min-width: 0;
  font-size: 0.85rem;
  white-space: pre-line;
  max-height: 18rem;
  overflow-y: auto;
}
.text p {
  margin: 0 0 0.4rem;
}
.meta,
.hint {
  color: var(--p-text-muted-color);
  font-size: 0.8rem;
}
.hint {
  margin: 0;
}
.actions,
.row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.use,
.give,
.draw {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  padding: 0.6rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0.5rem;
}
.draw p {
  margin: 0;
}
.check {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}
.minimap {
  height: 14rem;
  flex: none;
}
</style>
