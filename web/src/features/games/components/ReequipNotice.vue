<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { packRoom } from '@/features/inventory'
import type { CampaignState, EncounterCommand } from '../api/types'

/** The free Re-Equip after a gain or a Restore (rulebook p. 70), and what to do when the Pack is full. */
const props = defineProps<{ campaign: CampaignState; busy: boolean }>()
const emit = defineEmits<{ command: [command: EncounterCommand]; inventory: [] }>()
const { t } = useI18n()

const offers = computed(() =>
  (props.campaign.freeReequip ?? []).flatMap((name) => {
    const member = props.campaign.party.find((a) => a.name === name)
    return member ? [{ name, full: (member.pack?.length ?? 0) > packRoom(member) }] : []
  }),
)
</script>

<template>
  <Message v-if="offers.length" :severity="offers.some((o) => o.full) ? 'warn' : 'info'" size="small" class="reequip">
    <span class="line">
      <span>{{ t('reequip.offer') }}</span>
      <span v-for="offer in offers" :key="offer.name" class="offer">
        <strong>{{ offer.name }}</strong>
        <em v-if="offer.full">{{ t('reequip.full') }}</em>
        <Button
          icon="pi pi-check"
          size="small"
          text
          :disabled="busy"
          :aria-label="t('reequip.done', { name: offer.name })"
          :title="t('reequip.done', { name: offer.name })"
          @click="emit('command', { type: 'reequipped', adventurer: offer.name })"
        />
      </span>
      <Button :label="t('inventory.open')" icon="pi pi-briefcase" size="small" text @click="emit('inventory')" />
    </span>
  </Message>
</template>

<style scoped>
.reequip {
  flex: none;
}
.line,
.offer {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.25rem 0.6rem;
}
</style>
