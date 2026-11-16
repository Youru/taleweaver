<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useConfirm } from 'primevue/useconfirm'
import { ApiError } from '@/shared/api/http'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useDeleteDraft, useSaveDraft, useWorkshopList } from '../api/queries'
import type { DraftScenario, WorkshopSummary } from '../api/types'
import { nameOf, workshopApi } from '../api/workshopApi'
import { newScene, slug, text } from '../lib/graph'

/**
 * The workshop: the scenarios made here (open, delete), the official ones (read only: duplicate one
 * to start from it), and a new scenario from scratch.
 */
const { t } = useI18n()
const router = useRouter()
const confirm = useConfirm()
const { language } = storeToRefs(usePreferences())
const list = useWorkshopList()
const save = useSaveDraft()
const remove = useDeleteDraft()

const mine = computed(() => (list.data.value ?? []).filter((s) => s.editable))
const official = computed(() => (list.data.value ?? []).filter((s) => !s.editable))

/** A name not taken yet: "my-bounty", "my-bounty-2"… */
function freeName(title: string): string {
  const taken = new Set((list.data.value ?? []).map((s) => s.id))
  const base = slug(title)
  let name = base
  for (let n = 2; taken.has(`homebrew:scenario/${name}`); n++) name = `${base}-${n}`
  return name
}

function printOut(id: string) {
  void router.push({ name: 'workshop-print', query: { scenario: id } })
}

function open(name: string) {
  void router.push({ name: 'workshop-editor', params: { name } })
}

/** Saves the new scenario, then opens it (awaited: a callback given to mutate() can be skipped). */
async function create(scenario: DraftScenario) {
  try {
    await save.mutateAsync(scenario)
  } catch {
    return // shown by the error message
  }
  open(nameOf(scenario.id))
}

// New scenario
const naming = ref(false)
const title = ref('')
function createNew() {
  const value = title.value.trim()
  if (!value) return
  const name = freeName(value)
  void create({
    id: `homebrew:scenario/${name}`,
    title: text(language.value, value),
    kind: 'bounty',
    packs: ['um1'],
    start: 'intro',
    nodes: [newScene('intro', language.value, t('workshop.newIntro'))],
  })
  naming.value = false
  title.value = ''
}

/** Starts from an official scenario: a copy in the workshop, the original stays as it is. */
async function duplicate(summary: WorkshopSummary) {
  const original = await workshopApi.get(summary.id)
  const copyTitle = t('workshop.copyOf', { title: pick(original.title, language.value) })
  void create({ ...original, id: `homebrew:scenario/${freeName(copyTitle)}`, title: text(language.value, copyTitle) })
}

function askRemove(summary: WorkshopSummary) {
  confirm.require({
    message: t('workshop.deleteConfirm', { title: pick(summary.title, language.value) }),
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', text: true },
    accept: () => remove.mutate(summary.id),
  })
}

const error = computed(() => {
  const e = save.error.value ?? remove.error.value
  return e instanceof ApiError ? e.message : e ? String(e) : null
})
</script>

<template>
  <main class="workshop">
    <header class="head">
      <h1>{{ t('workshop.title') }}</h1>
      <Button :label="t('workshop.new')" icon="pi pi-plus" @click="naming = true" />
    </header>
    <p class="intro">{{ t('workshop.intro') }}</p>
    <Message v-if="error" severity="error" closable>{{ error }}</Message>
    <ProgressSpinner v-if="list.isPending.value" :aria-label="t('common.loading')" />
    <template v-else>
      <section :aria-labelledby="'mine-title'">
        <h2 id="mine-title">{{ t('workshop.mine') }}</h2>
        <p v-if="!mine.length" class="empty">{{ t('workshop.noneYet') }}</p>
        <div v-else class="cards">
          <Card v-for="scenario in mine" :key="scenario.id">
            <template #title>{{ pick(scenario.title, language) }}</template>
            <template #subtitle>{{ nameOf(scenario.id) }}</template>
            <template #footer>
              <div class="actions">
                <Button :label="t('workshop.open')" icon="pi pi-pencil" @click="open(nameOf(scenario.id))" />
                <span class="spacer" />
                <Button
                  icon="pi pi-print"
                  severity="secondary"
                  text
                  :aria-label="t('workshop.print.printScenario', { title: pick(scenario.title, language) })"
                  @click="printOut(scenario.id)"
                />
                <Button
                  icon="pi pi-trash"
                  severity="danger"
                  text
                  :aria-label="t('workshop.deleteScenario', { title: pick(scenario.title, language) })"
                  @click="askRemove(scenario)"
                />
              </div>
            </template>
          </Card>
        </div>
      </section>
      <section :aria-labelledby="'official-title'">
        <h2 id="official-title">{{ t('workshop.official') }}</h2>
        <div class="cards">
          <Card v-for="scenario in official" :key="scenario.id">
            <template #title>{{ pick(scenario.title, language) }}</template>
            <template #subtitle>{{ scenario.kind }} · {{ t('workshop.readOnly') }}</template>
            <template #footer>
              <div class="actions">
                <Button
                  :label="t('workshop.duplicate')"
                  :aria-label="t('workshop.duplicateScenario', { title: pick(scenario.title, language) })"
                  icon="pi pi-copy"
                  severity="secondary"
                  :loading="save.isPending.value"
                  @click="duplicate(scenario)"
                />
                <Button
                  icon="pi pi-print"
                  severity="secondary"
                  text
                  :aria-label="t('workshop.print.printScenario', { title: pick(scenario.title, language) })"
                  @click="printOut(scenario.id)"
                />
              </div>
            </template>
          </Card>
        </div>
      </section>
    </template>

    <Dialog v-model:visible="naming" modal :header="t('workshop.new')" :style="{ width: '26rem' }">
      <form class="form" @submit.prevent="createNew">
        <label for="new-title">{{ t('workshop.scenarioTitle') }}</label>
        <InputText id="new-title" v-model="title" autofocus maxlength="80" />
        <Button type="submit" :label="t('workshop.create')" :disabled="!title.trim()" :loading="save.isPending.value" />
      </form>
    </Dialog>
  </main>
</template>

<style scoped>
.workshop {
  padding: 0 1rem 1rem;
  display: grid;
  gap: 1rem;
  max-width: 1100px;
  margin: 0 auto;
}
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
h1 {
  margin: 0;
  font-size: 1.5rem;
}
.intro,
.empty {
  margin: 0;
  color: var(--p-text-muted-color);
}
.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
  gap: 1rem;
}
.actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.spacer {
  flex: 1;
}
.form {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
</style>
