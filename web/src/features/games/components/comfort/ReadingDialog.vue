<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { LogEntry } from '../../api/types'
import { speeches } from '../../lib/comfort'

/** Reading aloud (play-spec M19): the page in large, each speech under its character. */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{ page: LogEntry[]; names: string[] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const size = ref(1.7)

// What is read aloud, not what is written for the GM
const aloud = new Set(['scene', 'storyRound', 'encounter', 'text', 'narrative', 'reward', 'encounterEnd', 'bountyStart'])
const paragraphs = computed(() =>
  props.page
    .filter((line) => aloud.has(line.code))
    .flatMap((line) => pick(line.text, language.value).split(/\n+/))
    .filter((paragraph) => paragraph.trim())
    .map((paragraph) => speeches(paragraph, props.names)),
)
/** The same colour each time for a character. */
const hueOf = (name: string) => [...name].reduce((sum, letter) => sum * 31 + letter.charCodeAt(0), 7) % 360
</script>

<template>
  <Dialog v-model:visible="open" modal maximizable :header="t('comfort.reading.title')" :style="{ width: '95vw' }">
    <div class="tools">
      <Button :label="t('comfort.reading.smaller')" text :disabled="size <= 1.1" @click="size -= 0.3" />
      <Button :label="t('comfort.reading.larger')" text :disabled="size >= 3.5" @click="size += 0.3" />
    </div>
    <p v-if="!paragraphs.length">{{ t('comfort.reading.empty') }}</p>
    <div class="page" :style="{ fontSize: `${size}rem` }">
      <p v-for="(parts, i) in paragraphs" :key="i">
        <template v-for="(part, j) in parts" :key="j">
          <span v-if="part.who" class="speech" :style="{ '--voice': `hsl(${hueOf(part.who)} 60% 45%)` }">
            <small>{{ part.who }}</small>
            {{ part.text }}
          </span>
          <strong v-else-if="part.speech">{{ part.text }}</strong>
          <template v-else>{{ part.text }}</template>
        </template>
      </p>
    </div>
  </Dialog>
</template>

<style scoped>
.page {
  max-width: 45em;
  margin: 0 auto;
  line-height: 1.55;
  font-family: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
}
.speech {
  display: block;
  margin: 0.3em 0;
  padding-left: 0.5em;
  border-left: 0.18em solid var(--voice);
  font-weight: 600;
}
small {
  display: block;
  color: var(--voice);
  font-size: 0.6em;
}
</style>
