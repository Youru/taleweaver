<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import { useDisciplines, useItems } from '@/features/catalog'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { ChoiceChange, PartyPreparation } from '../../api/preparation'
import type { AdventurerState } from '../../api/types'
import { disciplineBlock, disciplineCost } from '../../lib/storyRound'
import { counted, kitsOf } from '../../lib/preparation'

/** One adventurer before the departure: their kit, what they buy, what they learn. The same for the GM and a phone. */
const props = defineProps<{
  preparation: PartyPreparation
  adventurer: AdventurerState
  busy?: boolean
}>()
const emit = defineEmits<{ change: [change: ChoiceChange] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const disciplines = useDisciplines()
const itemById = computed(() => new Map((items.data.value ?? []).map((i) => [i.id, i])))
const disciplineById = computed(() => new Map((disciplines.data.value ?? []).map((d) => [d.id, d])))
const itemName = (id: string) => pick(itemById.value.get(id)?.name, language.value) || id
const disciplineName = (id: string) => pick(disciplineById.value.get(id)?.name, language.value) || id

const choice = computed(() => props.preparation.choices.find((c) => c.adventurer === props.adventurer.name))
const bought = computed(() => choice.value?.items ?? [])
const learnt = computed(() => choice.value?.disciplines ?? [])

const kits = computed(() =>
  kitsOf(props.preparation, props.adventurer.name).map((kit) => ({ ...kit, label: pick(kit.name, language.value) })),
)
const kit = computed(() => kits.value.find((k) => k.id === choice.value?.kit))

const shop = computed(() =>
  props.preparation.items
    .map(({ item, price }) => ({
      value: item,
      label: t('preparation.priced', { name: itemName(item), price }),
      disabled: !bought.value.includes(item) && price > props.preparation.goldLeft,
    }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)

const xpLeft = computed(
  () => props.adventurer.xp - (props.adventurer.disciplines ?? []).reduce((sum, d) => sum + d.xp, 0),
)
/** What may be picked now; what was picked stays, to take it back. */
const training = computed(() => {
  const catalog = disciplines.data.value ?? []
  return props.preparation.disciplines
    .flatMap((id) => {
      const card = disciplineById.value.get(id)
      if (!card) return []
      const picked = learnt.value.includes(id)
      const cost = picked
        ? (props.adventurer.disciplines?.find((d) => d.id === id)?.xp ?? 0)
        : (disciplineCost(card, props.adventurer, catalog) ?? 0)
      if (!picked && (cost > xpLeft.value || disciplineBlock(card, props.adventurer, catalog) !== null)) return []
      return [
        {
          value: id,
          level: card.level ?? 0,
          label: t('preparation.trained', { name: disciplineName(id), level: card.level ?? 0, xp: cost }),
        },
      ]
    })
    .sort((a, b) => a.level - b.level || a.label.localeCompare(b.label))
})

/** Every card in hand at the departure, as pictures. */
const held = computed(() => [
  ...counted(
    [...(props.adventurer.equipped ?? []), ...(props.adventurer.pack ?? [])].flatMap((i) => (i.item ? [i.item] : [])),
  ).map(({ id, count }) => ({ id, count, name: itemName(id), image: itemById.value.get(id)?.image })),
  ...(props.adventurer.disciplines ?? []).map(({ id }) => ({
    id,
    count: 1,
    name: disciplineName(id),
    image: disciplineById.value.get(id)?.image,
  })),
])
</script>

<template>
  <div class="choice">
    <label v-if="kits.length > 1" class="field">
      <span>{{ t('preparation.kit') }}</span>
      <Select
        :model-value="choice?.kit ?? null"
        :options="kits"
        option-label="label"
        option-value="id"
        :placeholder="t('preparation.chooseKit')"
        :show-clear="preparation.itemsOpen"
        :disabled="busy"
        :aria-label="t('preparation.kitOf', { name: adventurer.name })"
        @update:model-value="(value: string | null) => emit('change', { kit: value ?? '' })"
      />
    </label>
    <p v-else-if="kit" class="kit">{{ kit.label }}</p>

    <label v-if="preparation.itemsOpen" class="field">
      <span>{{ t('preparation.buy', { gold: preparation.goldLeft }) }}</span>
      <MultiSelect
        :model-value="bought"
        :options="shop"
        option-label="label"
        option-value="value"
        option-disabled="disabled"
        :selection-limit="preparation.maxItems ?? undefined"
        :placeholder="t('preparation.nothing')"
        :show-toggle-all="false"
        filter
        :disabled="busy"
        :aria-label="t('preparation.buyFor', { name: adventurer.name })"
        @update:model-value="(value: string[]) => emit('change', { items: value })"
      />
    </label>

    <label v-if="preparation.disciplines.length" class="field">
      <span>{{ t('preparation.train', { xp: xpLeft }) }}</span>
      <MultiSelect
        :model-value="learnt"
        :options="training"
        option-label="label"
        option-value="value"
        :selection-limit="preparation.maxDisciplines ?? undefined"
        :placeholder="t('preparation.nothing')"
        :empty-message="t('preparation.nothingToLearn')"
        :show-toggle-all="false"
        filter
        :disabled="busy"
        :aria-label="t('preparation.trainFor', { name: adventurer.name })"
        @update:model-value="(value: string[]) => emit('change', { disciplines: value })"
      />
    </label>

    <ul v-if="held.length" class="held">
      <li v-for="card in held" :key="card.id" :title="card.name">
        <img v-if="card.image" :src="assetUrl(card.image, 320)" :alt="card.name" loading="lazy" />
        <span v-else>{{ card.name }}</span>
        <b v-if="card.count > 1">×{{ card.count }}</b>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.choice {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  min-width: 0;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  min-width: 0;
}
.field > span,
.kit {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.field :deep(.p-select),
.field :deep(.p-multiselect) {
  width: 100%;
  min-width: 0;
}
.held {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(4.2rem, 1fr));
  gap: 0.35rem;
  list-style: none;
  margin: 0;
  padding: 0;
}
.held li {
  position: relative;
  font-size: 0.75rem;
}
.held img {
  width: 100%;
  border-radius: 0.3rem;
  display: block;
}
.held b {
  position: absolute;
  right: 0.2rem;
  bottom: 0.2rem;
  padding: 0 0.3rem;
  border-radius: 999px;
  background: rgb(0 0 0 / 70%);
  color: #fff;
}
</style>
