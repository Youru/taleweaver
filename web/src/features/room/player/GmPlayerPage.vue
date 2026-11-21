<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useGameCommand } from '@/features/games'
import { roomGmApi } from '../api/roomApi'
import { useRoomHub } from '../api/roomHub'
import type { PlayerCommand, PlayerView } from '../api/types'
import { asGameCommand } from '../lib/room'
import PlayerLayout from './PlayerLayout.vue'

/**
 * /play/gm/:gameId/:adventurer: the GM plays an adventurer as a player, in a tab of their own (room-spec §7). No
 * seat: the GM's identity; the view is reloaded each time the game changes.
 */
const props = defineProps<{ gameId: string; adventurer: string }>()
const { t } = useI18n()
const view = ref<PlayerView>()
const error = ref<string>()

async function load() {
  try {
    view.value = await roomGmApi.impersonate(props.gameId, props.adventurer)
  } catch (e) {
    error.value = (e as Error).message
  }
}

// The GM's actions apply at once, as from the game page (room-spec §7.4)
const command = useGameCommand(() => props.gameId)
async function act(action: PlayerCommand) {
  await command.mutateAsync(asGameCommand(action))
  await load()
}

const { status } = useRoomHub(
  `game=${encodeURIComponent(props.gameId)}`,
  { GameChanged: () => void load() },
  () => void load(),
)
onMounted(load)
</script>

<template>
  <Message v-if="error" severity="error">{{ error }}</Message>
  <PlayerLayout v-else-if="view" :view="view" :followed="adventurer" :status="status" :act="act" />
  <div v-else class="center"><ProgressSpinner :aria-label="t('common.loading')" /></div>
</template>

<style scoped>
.center {
  min-height: 100dvh;
  display: grid;
  place-items: center;
}
</style>
