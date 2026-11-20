<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useConfirm } from 'primevue/useconfirm'
import { usePreferences } from '@/shared/lib/preferences'
import type { LocalizedText } from '@/shared/lib/localized'
import type { DraftChapter, DraftScenario, ScenarioIssue } from '../api/types'
import {
  addChapter,
  chapterCaption,
  chapterSizes,
  chaptersOf,
  moveChapter,
  removeChapter,
  renameChapter,
} from '../lib/chapters'
import { captionOf, text } from '../lib/graph'
import TextField from './TextField.vue'

/**
 * The scenario itself, when no node is selected: its title, its chapters, the steps of the chapter
 * shown, what is left to fix.
 */
const props = defineProps<{ scenario: DraftScenario; issues: ScenarioIssue[]; chapter?: string | null }>()
const emit = defineEmits<{ change: [scenario: DraftScenario]; select: [id: string]; show: [chapter: string] }>()

const { t } = useI18n()
const confirm = useConfirm()
const { language } = storeToRefs(usePreferences())
const title = computed({
  get: () => props.scenario.title,
  set: (value: LocalizedText | undefined) => emit('change', { ...props.scenario, title: value ?? {} }),
})

const chapters = computed(() => chaptersOf(props.scenario))
const sizes = computed(() => chapterSizes(props.scenario))
const steps = computed(() =>
  props.chapter ? props.scenario.nodes.filter((n) => n.chapter === props.chapter) : props.scenario.nodes,
)

function add() {
  const title = text(language.value, t('workshop.chapters.newTitle', { n: chapters.value.length + 1 }))
  const next = addChapter(props.scenario, title)
  emit('change', next)
  const added = chaptersOf(next).at(-1)
  if (added) emit('show', added.id)
}

function rename(chapter: DraftChapter, value: LocalizedText | undefined) {
  emit('change', renameChapter(props.scenario, chapter.id, value ?? {}))
}

function remove(chapter: DraftChapter) {
  confirm.require({
    message:
      chapters.value.length > 1
        ? t('workshop.chapters.removeConfirm', { title: chapterCaption(chapter, language.value) })
        : t('workshop.chapters.removeLast'),
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', text: true },
    accept: () => emit('change', removeChapter(props.scenario, chapter.id)),
  })
}
</script>

<template>
  <section class="sheet" :aria-label="t('workshop.scenarioSheet')">
    <TextField v-model="title" :label="t('workshop.scenarioTitle')" />
    <p class="hint">{{ t('workshop.howTo') }}</p>
    <Message v-for="(issue, i) in issues" :key="i" severity="error" size="small">
      {{ issue.where }} : {{ issue.message }}
    </Message>

    <h3>{{ t('workshop.chapters.title') }}</h3>
    <p class="hint">{{ t('workshop.chapters.hint') }}</p>
    <ol v-if="chapters.length" class="chapters">
      <li v-for="(c, i) in chapters" :key="c.id" :class="{ shown: c.id === chapter }">
        <TextField
          :model-value="c.title"
          :label="t('workshop.chapters.titleOf', { id: c.id, count: sizes.get(c.id) ?? 0 }, sizes.get(c.id) ?? 0)"
          @update:model-value="(value) => rename(c, value)"
        />
        <div class="tools">
          <Button
            icon="pi pi-eye"
            size="small"
            text
            :disabled="c.id === chapter"
            :aria-label="t('workshop.chapters.open')"
            :title="t('workshop.chapters.open')"
            @click="emit('show', c.id)"
          />
          <Button
            icon="pi pi-arrow-up"
            size="small"
            text
            severity="secondary"
            :disabled="i === 0"
            :aria-label="t('workshop.chapters.up')"
            :title="t('workshop.chapters.up')"
            @click="emit('change', moveChapter(scenario, c.id, -1))"
          />
          <Button
            icon="pi pi-arrow-down"
            size="small"
            text
            severity="secondary"
            :disabled="i === chapters.length - 1"
            :aria-label="t('workshop.chapters.down')"
            :title="t('workshop.chapters.down')"
            @click="emit('change', moveChapter(scenario, c.id, 1))"
          />
          <Button
            icon="pi pi-trash"
            size="small"
            text
            severity="danger"
            :aria-label="t('workshop.chapters.remove')"
            :title="t('workshop.chapters.remove')"
            @click="remove(c)"
          />
        </div>
      </li>
    </ol>
    <Button
      :label="chapters.length ? t('workshop.chapters.add') : t('workshop.chapters.split')"
      icon="pi pi-folder-plus"
      size="small"
      severity="secondary"
      outlined
      class="add"
      @click="add"
    />

    <h3>{{ t('workshop.steps') }}</h3>
    <ul class="steps">
      <li v-for="node in steps" :key="node.id">
        <Button
          :label="captionOf(node, language)"
          :icon="node.type === 'encounter' ? 'pi pi-bolt' : 'pi pi-book'"
          size="small"
          text
          @click="emit('select', node.id)"
        />
        <span v-if="node.id === scenario.start" class="start">{{ t('workshop.isStart') }}</span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.sheet {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.hint {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
h3 {
  margin: 0.5rem 0 0;
  font-size: 1rem;
}
.chapters {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin: 0;
  padding: 0;
  list-style: none;
}
.chapters li {
  display: flex;
  align-items: flex-end;
  gap: 0.25rem;
  padding: 0.35rem 0.5rem;
  border-radius: var(--p-content-border-radius);
}
.chapters li.shown {
  background: color-mix(in srgb, var(--p-primary-color) 10%, transparent);
}
.chapters li > :first-child {
  flex: 1;
  min-width: 0;
}
.tools {
  display: flex;
}
.add {
  align-self: flex-start;
}
.steps {
  list-style: none;
  margin: 0;
  padding: 0;
}
.steps li {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}
.start {
  font-size: 0.75rem;
  color: var(--p-primary-color);
  font-weight: 600;
}
</style>
