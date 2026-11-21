<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import { useShowToPlayers } from '@/shared/lib/showToPlayers'

/** "Show the players" (room-spec §7.5): only while the game's room is open; a tick once the phones have it. */
const props = defineProps<{ image?: string | null; caption?: string }>()
const { t } = useI18n()
const show = useShowToPlayers()
const sent = ref(false)

async function send() {
  if (!show?.value || !props.image) return
  await show.value(props.image, props.caption)
  sent.value = true
  setTimeout(() => (sent.value = false), 1500)
}
</script>

<template>
  <Button
    v-if="show && image"
    :icon="sent ? 'pi pi-check' : 'pi pi-mobile'"
    text
    rounded
    size="small"
    :aria-label="t('room.gm.show')"
    :title="t('room.gm.show')"
    @click.stop="send"
  />
</template>
