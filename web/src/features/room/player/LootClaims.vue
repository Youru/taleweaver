<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useItems } from '@/features/catalog'
import type { OwnedItem } from '@/features/games'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerCommand, PlayerRequest } from '../api/types'

/** "I want it" on what was gained and waits to be handed out (play-spec J15): the GM decides. */
const props = defineProps<{
  loot: OwnedItem[]
  adventurer: string
  requests: PlayerRequest[]
  act: (command: PlayerCommand) => Promise<void>
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const failure = ref<string>()

const nameOf = (owned: OwnedItem) =>
  pick(items.data.value?.find((i) => i.id === owned.item)?.name, language.value) ||
  [owned.tiers?.join('/'), owned.type].filter(Boolean).join(' ') ||
  t('room.player.cardToDraw')
const asked = (id: string) =>
  props.requests.some(
    (r) =>
      r.status === 'pending' &&
      r.command.type === 'claim' &&
      r.command.item === id &&
      r.command.adventurer === props.adventurer,
  )
async function claim(id: string) {
  failure.value = undefined
  try {
    await props.act({ type: 'claim', adventurer: props.adventurer, item: id })
  } catch (e) {
    failure.value = (e as Error).message
  }
}
</script>

<template>
  <section v-if="loot.length" class="loot">
    <h2>{{ t('room.player.loot') }}</h2>
    <div v-for="owned in loot" :key="owned.id" class="line">
      <span>{{ nameOf(owned) }}</span>
      <Button
        :label="asked(owned.id) ? t('room.player.claimed') : t('room.player.claim')"
        size="small"
        :disabled="asked(owned.id)"
        @click="claim(owned.id)"
      />
    </div>
    <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
  </section>
</template>

<style scoped>
.line {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 0.3rem 0;
}
</style>
