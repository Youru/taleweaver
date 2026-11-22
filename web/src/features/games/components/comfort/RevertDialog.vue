<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import type { LogEntry } from '../../api/types'
import { actionOfLine } from '../../lib/comfort'
import GameJournal from '../GameJournal.vue'

/** Back to before a line (play-spec M2): what goes, who had asked for it, then the GM confirms. */
const line = defineModel<number | null>('line', { required: true })
const props = defineProps<{ log: LogEntry[]; actions: number[]; busy: boolean }>()
const emit = defineEmits<{ confirm: [actions: number, outOf: number] }>()
const { t } = useI18n()

const action = computed(() => (line.value === null ? undefined : actionOfLine(props.actions, line.value)))
const gone = computed(() => (action.value === undefined ? [] : props.log.slice(props.actions[action.value])))

function confirm() {
  if (action.value !== undefined) emit('confirm', action.value, props.actions.length)
  line.value = null
}
</script>

<template>
  <Dialog
    :visible="line !== null"
    modal
    :header="t('comfort.revert.title')"
    :style="{ width: 'min(38rem, 95vw)' }"
    @update:visible="(shown: boolean) => !shown && (line = null)"
  >
    <Message v-if="action === undefined" severity="warn">{{ t('comfort.revert.blocked') }}</Message>
    <template v-else>
      <p>{{ t('comfort.revert.intro', { n: actions.length - action }) }}</p>
      <GameJournal :log="gone" class="gone" />
      <Button
        icon="pi pi-undo"
        :label="t('comfort.revert.confirm')"
        severity="danger"
        :disabled="busy"
        @click="confirm"
      />
    </template>
  </Dialog>
</template>

<style scoped>
.gone {
  max-height: 45vh;
  margin-bottom: 0.8rem;
}
</style>
