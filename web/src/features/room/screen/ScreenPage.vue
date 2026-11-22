<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useQueryClient } from '@tanstack/vue-query'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import { ApiError } from '@/shared/api/http'
import { storedScreen, storeScreen, type ScreenLink } from '@/shared/lib/screen'
import { screenApi } from '../api/roomApi'

/** A QR code lands here: exchanged once for this device's link, then the GM's view or the table. */
const props = defineProps<{ token: string }>()
const { t } = useI18n()
const router = useRouter()
const queries = useQueryClient()
const failed = ref<string>()
const linked = ref<ScreenLink>()

function enter(link: ScreenLink) {
  void router.replace(link.role === 'gm' ? { name: 'play', params: { id: link.gameId } } : { name: 'table-screen' })
}

async function claim() {
  failed.value = undefined
  try {
    const link = await screenApi.claim(props.token)
    storeScreen(link)
    // What was read in the former role is not for this one
    queries.clear()
    enter(link)
  } catch (e) {
    const gone = e instanceof ApiError && e.code === 'Rooms.ScreenLinkUsed'
    failed.value = gone ? t('screens.used') : t('screens.offline')
    linked.value = storedScreen()
  }
}

onMounted(claim)
</script>

<template>
  <main class="center">
    <template v-if="failed">
      <p role="alert">{{ failed }}</p>
      <div class="buttons">
        <Button :label="t('screens.retry')" icon="pi pi-refresh" severity="secondary" @click="claim" />
        <Button v-if="linked" :label="t('screens.resume')" @click="enter(linked)" />
      </div>
    </template>
    <ProgressSpinner v-else :aria-label="t('common.loading')" />
  </main>
</template>

<style scoped>
.center {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100dvh;
  padding: 1rem;
  text-align: center;
}
.buttons {
  display: flex;
  gap: 0.5rem;
}
</style>
