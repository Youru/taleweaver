<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { gamesApi } from '../api/gamesApi'
import { useRestartGame } from '../api/queries'

/** An Ending (rulebook p. 13): try again, start over, or keep the Adventure Sheet for the next Act. */
const props = defineProps<{ id: string; fought: boolean }>()
const { t } = useI18n()
const router = useRouter()
const restart = useRestartGame(() => props.id)

function again(from: 'beginning' | 'lastEncounter') {
  restart.mutate(from, { onSuccess: (game) => void router.push({ name: 'play', params: { id: game.id } }) })
}
</script>

<template>
  <section class="ending" :aria-label="t('ending.title')">
    <p>{{ t('ending.intro') }}</p>
    <div class="buttons">
      <Button
        v-if="fought"
        icon="pi pi-replay"
        :label="t('ending.lastEncounter')"
        :loading="restart.isPending.value"
        @click="again('lastEncounter')"
      />
      <Button
        icon="pi pi-refresh"
        :label="t('ending.beginning')"
        severity="secondary"
        :disabled="restart.isPending.value"
        @click="again('beginning')"
      />
      <Button
        as="a"
        :href="gamesApi.actExportUrl(id)"
        download
        icon="pi pi-download"
        :label="t('ending.export')"
        severity="secondary"
        class="export"
      />
    </div>
    <Message v-if="restart.error.value" severity="error">{{ restart.error.value.message }}</Message>
  </section>
</template>

<style scoped>
/* A link drawn as a button */
.export {
  text-decoration: none;
}
p {
  margin: 0 0 0.5rem;
}
.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
