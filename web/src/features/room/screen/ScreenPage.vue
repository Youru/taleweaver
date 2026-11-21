<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { storeScreenToken } from '@/shared/lib/screen'

/** The QR code of the second screen lands here: the token is kept, then the table of the game shows. */
const props = defineProps<{ gameId: string; token: string }>()
const router = useRouter()

onMounted(() => {
  storeScreenToken(props.token)
  void router.replace({ name: 'table', params: { id: props.gameId } })
})
</script>

<template>
  <p class="sr-only">…</p>
</template>
