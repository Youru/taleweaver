<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'

/** The neutral screen of a pause (play-spec M16): nothing of the game shows, dialogs included. */
defineProps<{ resumable?: boolean; busy?: boolean }>()
const emit = defineEmits<{ resume: [] }>()
const { t } = useI18n()
</script>

<template>
  <Dialog
    :visible="true"
    modal
    :closable="false"
    :close-on-escape="false"
    :show-header="false"
    :style="{ width: '100vw', height: '100dvh', maxHeight: '100dvh', margin: 0, borderRadius: 0 }"
    :content-style="{ flex: 1 }"
  >
    <div class="pause" role="status">
      <i class="pi pi-pause-circle" aria-hidden="true" />
      <p>{{ t('comfort.pause.paused') }}</p>
      <Button
        v-if="resumable"
        autofocus
        icon="pi pi-play"
        :label="t('comfort.pause.resume')"
        :disabled="busy"
        @click="emit('resume')"
      />
    </div>
  </Dialog>
</template>

<style scoped>
.pause {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  font-size: 1.6rem;
}
.pi-pause-circle {
  font-size: 4rem;
}
</style>
