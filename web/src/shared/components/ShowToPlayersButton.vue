<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Menu from 'primevue/menu'
import { useShowToPlayers } from '@/shared/lib/showToPlayers'

/**
 * "Show the players" (room-spec §7.5): only while the game's room is open. With several players it asks to whom
 * (all, or one: a secret for that player); a tick once the phones have it.
 */
// Button and menu: the caller's class goes on the button
defineOptions({ inheritAttrs: false })
const props = defineProps<{ image?: string | null; caption?: string }>()
const { t } = useI18n()
const sharing = useShowToPlayers()
const sent = ref(false)
const menu = ref<{ toggle: (event: Event) => void }>()
const choices = computed(() => [
  { label: t('room.gm.showAll'), icon: 'pi pi-users', command: () => void send() },
  ...(sharing?.value?.players ?? []).map((player) => ({
    label: player.name,
    icon: 'pi pi-user',
    command: () => void send(player.id),
  })),
])

async function send(player?: string) {
  if (!sharing?.value || !props.image) return
  await sharing.value.show(props.image, props.caption, player)
  sent.value = true
  setTimeout(() => (sent.value = false), 1500)
}
function press(event: Event) {
  if ((sharing?.value?.players.length ?? 0) > 1) menu.value?.toggle(event)
  else void send()
}
</script>

<template>
  <template v-if="sharing && image">
    <Button
      v-bind="$attrs"
      :icon="sent ? 'pi pi-check' : 'pi pi-mobile'"
      text
      rounded
      size="small"
      :aria-label="t('room.gm.show')"
      :title="t('room.gm.show')"
      @click.stop="press"
    />
    <Menu ref="menu" :model="choices" popup />
  </template>
</template>
