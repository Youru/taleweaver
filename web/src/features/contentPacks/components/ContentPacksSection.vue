<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useConfirm } from 'primevue/useconfirm'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useContentPacks, useInstallContentPack, useRemoveContentPack } from '../api/contentPacks'

/**
 * The content packs (Features/ContentPacks): the app is given without the game's content; whoever has a pack
 * file (*.taleweaver.zip) adds it here, or drops it beside the executable.
 */
const { t } = useI18n()
const confirm = useConfirm()
const { language } = storeToRefs(usePreferences())
const packs = useContentPacks()
const install = useInstallContentPack()
const removing = useRemoveContentPack()
const input = ref<HTMLInputElement>()

function chosen(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (file) install.mutate(file)
  ;(event.target as HTMLInputElement).value = ''
}

function remove(id: string, name: string) {
  confirm.require({
    message: t('contentPacks.removeConfirm', { name }),
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', text: true },
    accept: () => removing.mutate(id),
  })
}
</script>

<template>
  <section aria-labelledby="content-packs-title" class="content-packs">
    <h2 id="content-packs-title">{{ t('contentPacks.title') }}</h2>
    <p class="help">{{ t('contentPacks.help') }}</p>
    <ul v-if="packs.data.value?.length">
      <li v-for="pack in packs.data.value" :key="pack.id">
        <div>
          <strong>{{ pick(pack.name, language) }}</strong>
          <small>{{
            t('contentPacks.counts', {
              scenarios: pack.scenarios,
              pictures: pack.pictures,
              version: pack.version ?? '',
            })
          }}</small>
        </div>
        <Button
          icon="pi pi-trash"
          size="small"
          severity="danger"
          text
          :aria-label="t('contentPacks.remove', { name: pick(pack.name, language) })"
          @click="remove(pack.id, pick(pack.name, language))"
        />
      </li>
    </ul>
    <input ref="input" type="file" accept=".zip,application/zip" hidden @change="chosen" />
    <Button
      :label="install.isPending.value ? t('contentPacks.installing') : t('contentPacks.add')"
      :loading="install.isPending.value"
      icon="pi pi-upload"
      severity="secondary"
      @click="input?.click()"
    />
    <Message v-if="install.isError.value" severity="error">
      {{ t('contentPacks.failed', { message: install.error.value?.message ?? '' }) }}
    </Message>
    <Message v-else-if="install.isSuccess.value" severity="success">
      {{ t('contentPacks.installed', { name: pick(install.data.value?.name, language) }) }}
    </Message>
  </section>
</template>

<style scoped>
.content-packs {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.5rem;
}
.help {
  margin: 0;
  color: var(--p-text-muted-color);
}
ul {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  width: 100%;
  margin: 0;
  padding: 0;
  list-style: none;
}
li {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}
li div {
  display: flex;
  flex-direction: column;
}
</style>
