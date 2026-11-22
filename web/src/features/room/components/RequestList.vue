<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import type { PlayerRequest } from '../api/types'
import { describeRequest } from '../lib/room'

/**
 * Requests (room-spec §6.2): for the GM, the queue to accept or refuse; for a phone, its own requests, the
 * pending ones to cancel and the last answers with their reason.
 */
const props = defineProps<{
  requests: PlayerRequest[]
  gm?: boolean
  busy?: boolean
  itemName: (id: string) => string
  disciplineName?: (id: string) => string
}>()
const emit = defineEmits<{ accept: [id: string]; acceptFrom: [seat: string]; refuse: [id: string]; cancel: [id: string] }>()
const { t, te } = useI18n()

/** The GM's queue by player (play-spec M14): the pending ones together, then the answers. */
const groups = computed(() => {
  if (!props.gm) return [{ seat: '', by: '', waiting: 0, list: props.requests }]
  const bySeat = new Map<string, PlayerRequest[]>()
  for (const request of props.requests.filter((r) => r.status === 'pending'))
    bySeat.set(request.seatId, [...(bySeat.get(request.seatId) ?? []), request])
  return [
    ...[...bySeat].map(([seat, list]) => ({ seat, by: list[0]?.by ?? '', waiting: list.length, list })),
    { seat: '', by: '', waiting: 0, list: props.requests.filter((r) => r.status !== 'pending') },
  ]
})

function text(request: PlayerRequest) {
  const { key, args } = describeRequest(request.command, props.itemName, props.disciplineName)
  return t(key, args)
}

/** An error code of the API ("Party.NotEnoughXp") in words, or the GM's own words. */
function reason(request: PlayerRequest) {
  if (!request.reason) return ''
  const key = `room.reason.${request.reason.replace('.', '_')}`
  return te(key) ? t(key) : request.reason
}

const severity: Record<PlayerRequest['status'], string> = {
  pending: 'warn',
  accepted: 'success',
  applied: 'success',
  refused: 'danger',
  failed: 'danger',
  cancelled: 'secondary',
}
</script>

<template>
  <ul class="requests">
    <template v-for="group in groups" :key="group.seat">
    <li v-if="group.waiting > 1" class="line">
      <strong class="what">{{ t('room.request.from', { name: group.by, n: group.waiting }) }}</strong>
      <Button
        icon="pi pi-check-square"
        :label="t('room.request.acceptFrom')"
        size="small"
        :disabled="busy"
        @click="emit('acceptFrom', group.seat)"
      />
    </li>
    <li v-for="request in group.list" :key="request.id" :class="request.status">
      <div class="line">
        <strong v-if="gm">{{ request.by }}</strong>
        <span class="what">{{ text(request) }}</span>
        <Tag :severity="severity[request.status]" :value="t(`room.request.status.${request.status}`)" />
      </div>
      <p v-if="reason(request)" class="reason">{{ reason(request) }}</p>
      <div v-if="request.status === 'pending'" class="actions">
        <template v-if="gm">
          <Button
            icon="pi pi-check"
            :label="t('room.request.accept')"
            size="small"
            :disabled="busy"
            @click="emit('accept', request.id)"
          />
          <Button
            icon="pi pi-times"
            :label="t('room.request.refuse')"
            size="small"
            severity="secondary"
            :disabled="busy"
            @click="emit('refuse', request.id)"
          />
        </template>
        <Button
          v-else
          icon="pi pi-undo"
          :label="t('room.request.cancel')"
          size="small"
          severity="secondary"
          text
          :disabled="busy"
          @click="emit('cancel', request.id)"
        />
      </div>
    </li>
    </template>
  </ul>
</template>

<style scoped>
.requests {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
li {
  padding: 0.5rem 0.6rem;
  border-radius: 0.5rem;
  border: 1px solid var(--p-content-border-color);
  background: var(--p-content-background);
}
li.pending {
  border-style: dashed;
  border-color: var(--seat, var(--p-primary-color));
}
.line {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.what {
  flex: 1;
}
.reason {
  margin: 0.3rem 0 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.actions {
  display: flex;
  gap: 0.4rem;
  margin-top: 0.4rem;
}
</style>
