<script setup lang="ts">
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import { useItems } from '@/features/catalog'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { CampaignState, ItemCommand, OwnedItem } from '../../api/types'
import { consumablesAtRisk } from '../../lib/comfort'

/** Before "Explore" (play-spec M18): what would be discarded, to equip, sell for 1 Gold, or let go. */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{ campaign: CampaignState; busy: boolean }>()
const emit = defineEmits<{ command: [command: ItemCommand]; confirm: [] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()

const typeOf = (id: string) => items.data.value?.find((i) => i.id === id)?.type
const lost = computed(() => consumablesAtRisk(props.campaign, typeOf))
const names = computed(() => props.campaign.party.map((a) => a.name))
const wearer = reactive<Record<string, string>>({})
const nameOf = (owned: OwnedItem) =>
  pick(items.data.value?.find((i) => i.id === owned.item)?.name, language.value) ||
  [owned.tiers?.join('/'), owned.type].filter(Boolean).join(' ')
const holderOf = (holder: string) =>
  holder === 'stash' || holder === 'unassigned' ? t(`log.itemOwner.${holder}`) : holder
</script>

<template>
  <Dialog v-model:visible="open" modal :header="t('comfort.consumables.title')" :style="{ width: 'min(36rem, 95vw)' }">
    <p>{{ lost.length ? t('comfort.consumables.intro', { n: lost.length }) : t('comfort.consumables.none') }}</p>
    <ul>
      <li v-for="{ owned, holder } in lost" :key="owned.id">
        <span class="name">
          {{ nameOf(owned) }} <small>{{ holderOf(holder) }}</small>
        </span>
        <Select
          :model-value="wearer[owned.id] ?? (names.includes(holder) ? holder : undefined)"
          :options="names"
          size="small"
          :placeholder="t('comfort.consumables.who')"
          :aria-label="t('comfort.consumables.who')"
          @update:model-value="(name: string) => (wearer[owned.id] = name)"
        />
        <Button
          :label="t('comfort.consumables.equip')"
          size="small"
          text
          :disabled="busy || !(wearer[owned.id] ?? names.includes(holder))"
          @click="emit('command', { type: 'move', id: owned.id, owner: wearer[owned.id] ?? holder, slot: 'equipped' })"
        />
        <Button
          :label="t('comfort.consumables.sell')"
          size="small"
          text
          :disabled="busy"
          @click="emit('command', { type: 'discard', id: owned.id, gold: 1 })"
        />
      </li>
    </ul>
    <Button
      :label="lost.length ? t('comfort.consumables.confirm') : t('comfort.consumables.explore')"
      :severity="lost.length ? 'danger' : undefined"
      :disabled="busy"
      @click="((open = false), emit('confirm'))"
    />
  </Dialog>
</template>

<style scoped>
ul {
  margin: 0 0 0.8rem;
  padding: 0;
  list-style: none;
}
li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
  padding: 0.3rem 0;
}
.name {
  flex: 1;
}
small {
  color: var(--p-text-muted-color);
}
</style>
