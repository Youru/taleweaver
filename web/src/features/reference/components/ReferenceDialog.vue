<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useReference } from '../api/queries'
import { search } from '../lib/reference'

/** The rules in play (roadmap §8.6): effects, terrain, tokens and tags, found by typing. */
const open = defineModel<boolean>('open', { required: true })
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const reference = useReference()
const typed = ref('')
const found = computed(() => (reference.data.value ? search(reference.data.value, typed.value) : []))
</script>

<template>
  <Dialog
    v-model:visible="open"
    modal
    dismissable-mask
    :header="t('reference.title')"
    :style="{ width: 'min(44rem, 95vw)', height: '80vh' }"
  >
    <InputText
      v-model="typed"
      :placeholder="t('reference.search')"
      :aria-label="t('reference.search')"
      autofocus
      fluid
    />
    <Message v-if="reference.isError.value" severity="error">{{ reference.error.value?.message }}</Message>
    <p v-else-if="reference.data.value && !found.length">{{ t('reference.none') }}</p>
    <section v-for="{ table, entries } in found" :key="table">
      <h3>{{ t(`reference.tables.${table}`) }}</h3>
      <dl>
        <template v-for="entry in entries" :key="entry.id">
          <dt>{{ pick(entry.name, language) }}</dt>
          <dd>{{ pick(entry.text, language) }}</dd>
        </template>
      </dl>
    </section>
  </Dialog>
</template>

<style scoped>
h3 {
  margin: 1rem 0 0.3rem;
  text-transform: uppercase;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
dt {
  font-weight: 600;
}
dd {
  margin: 0 0 0.5rem;
}
</style>
