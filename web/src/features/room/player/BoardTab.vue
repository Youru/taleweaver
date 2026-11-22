<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { BoardCanvas, boardLayout, useBoardPieces } from '@/features/table'
import type { PlayerEncounter } from '../api/types'
import { asBoard } from '../lib/board'

/** The table, to look at (room-spec §5.1, Table): where am I, who is next to me. The physical board is the game. */
const props = defineProps<{ encounter: PlayerEncounter }>()
const { t } = useI18n()
const pieces = useBoardPieces()
const board = computed(() => asBoard(props.encounter))
const layout = computed(() =>
  board.value ? boardLayout(board.value.node, board.value.encounter, pieces.data.value) : undefined,
)
</script>

<template>
  <!-- Panning the board is not a swipe to another adventurer -->
  <div class="board" @touchstart.stop @touchend.stop>
    <BoardCanvas v-if="layout" :layout="layout" :selected="null" :label="t('room.player.boardTab')" readonly />
    <p v-else>{{ t('room.player.noBoard') }}</p>
  </div>
</template>

<style scoped>
.board {
  height: 62dvh;
  min-height: 18rem;
}
p {
  padding: 1rem;
  color: var(--p-text-muted-color);
}
</style>
