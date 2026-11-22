<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { useQueryClient } from '@tanstack/vue-query'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { InitiativeTrack, PauseScreen, type EncounterState } from '@/features/games'
import { BoardCanvas, boardLayout, useBoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { storedScreen } from '@/shared/lib/screen'
import { screenApi } from '../api/roomApi'
import { useRoomHub } from '../api/roomHub'
import type { TableScreenView } from '../api/types'
import { asBoard } from '../lib/board'

/**
 * /table: the screen in the middle of the players (room-spec §7.6). It reads the table's view and nothing else,
 * and never acts. The hub only signals; a slow refresh catches up on whatever was missed.
 */
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const queries = useQueryClient()
const pieces = useBoardPieces()

const link = storedScreen()
const token = link?.role === 'table' ? link.token : undefined
const view = ref<TableScreenView>()
const cut = ref<'closed' | 'unlinked' | undefined>(token ? undefined : 'unlinked')
const offline = ref(false)
const shown = ref<{ image: string; caption?: string | null } | null>(null)

let asked = 0
async function load() {
  if (!token) return
  const turn = ++asked
  try {
    const next = await screenApi.table(token)
    if (turn !== asked) return
    if (next.cards !== view.value?.cards) void queries.invalidateQueries({ queryKey: ['catalog'] })
    view.value = next
    cut.value = undefined
    offline.value = false
  } catch (e) {
    if (turn !== asked) return
    const status = e instanceof ApiError ? e.status : 0
    offline.value = status !== 410 && status !== 403 && status !== 404
    if (offline.value) return
    cut.value = status === 410 ? 'closed' : 'unlinked'
    view.value = undefined
    shown.value = null
  }
}

if (token) {
  useRoomHub(
    `table_token=${encodeURIComponent(token)}`,
    {
      TableChanged: () => void load(),
      ScreenRevoked: () => void load(),
      Shown: (picture: { image: string; caption?: string | null }) => (shown.value = picture),
    },
    () => void load(),
  )
}
void load()
const timer = window.setInterval(() => void load(), 10_000)
onBeforeUnmount(() => window.clearInterval(timer))

const encounter = computed(() => view.value?.encounter ?? undefined)
/** The track reads the GM's encounter: the table's carries the fields it needs. */
const track = computed(() => encounter.value as unknown as EncounterState)
const layout = computed(() => {
  const board = encounter.value && asBoard(encounter.value)
  return board ? boardLayout(board.node, board.encounter, pieces.data.value) : undefined
})
const party = computed(() => view.value?.party.filter((member) => member.active) ?? [])
</script>

<template>
  <main class="screen">
    <PauseScreen v-if="view?.paused" />
    <div v-if="cut" class="center">
      <i class="pi pi-moon" aria-hidden="true" />
      <p role="status">{{ t(`screens.${cut}`) }}</p>
    </div>
    <template v-else-if="view">
      <header>
        <h1>{{ pick(view.scenarioTitle, language) }}</h1>
        <p v-if="encounter">
          {{ t('room.player.round', { round: encounter.round }) }} ·
          {{ t('room.player.urgency', { urgency: encounter.urgency }) }}
        </p>
        <Button
          :label="language === 'fr' ? 'EN' : 'FR'"
          :aria-label="t('app.language')"
          severity="secondary"
          text
          @click="language = language === 'fr' ? 'en' : 'fr'"
        />
      </header>
      <Message v-if="offline" severity="warn" :closable="false">{{ t('screens.offline') }}</Message>
      <InitiativeTrack v-if="encounter && encounter.round > 0" :encounter="track" large />
      <BoardCanvas
        v-if="layout"
        class="board"
        :layout="layout"
        :selected="null"
        :label="t('room.player.boardTab')"
        readonly
      />
      <p v-else class="center">{{ t(encounter ? 'room.player.noBoard' : 'screens.noEncounter') }}</p>
      <ul v-if="party.length" class="party">
        <li v-for="member in party" :key="member.name" :class="{ down: member.defeated }">
          <strong>{{ member.name }}</strong>
          <span
            ><i class="pi pi-heart" aria-hidden="true" /> {{ Math.max(0, member.health - member.damage) }}/{{
              member.health
            }}</span
          >
        </li>
      </ul>
    </template>
    <div v-else class="center">
      <p v-if="offline" role="status">{{ t('screens.offline') }}</p>
      <ProgressSpinner v-else :aria-label="t('common.loading')" />
    </div>
    <button v-if="shown" type="button" class="shown" @click="shown = null">
      <img :src="assetUrl(shown.image, 1280)" :alt="shown.caption ?? ''" />
      <strong v-if="shown.caption">{{ shown.caption }}</strong>
      <small>{{ t('room.player.tapToClose') }}</small>
    </button>
  </main>
</template>

<style scoped>
.screen {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  box-sizing: border-box;
  height: 100dvh;
  padding: 0.75rem;
  overflow: hidden;
}
header {
  display: flex;
  align-items: center;
  gap: 1rem;
}
header h1 {
  flex: 1;
  margin: 0;
  font-size: 1.4rem;
}
header p {
  margin: 0;
  font-size: 1.2rem;
}
.board {
  flex: 1;
  min-height: 0;
}
.center {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}
.party {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.25rem 1.5rem;
  margin: 0;
  padding: 0;
  list-style: none;
  font-size: 1.2rem;
}
.party li {
  display: flex;
  gap: 0.5rem;
}
.party .down {
  opacity: 0.5;
}
.shown {
  position: fixed;
  inset: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  border: 0;
  background: rgb(10 10 10 / 90%);
  color: #fff;
  font: inherit;
}
.shown img {
  max-width: 100%;
  max-height: 80dvh;
}
</style>
