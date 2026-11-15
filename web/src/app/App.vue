<script setup lang="ts">
import { watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import ConfirmDialog from 'primevue/confirmdialog'
import SelectButton from 'primevue/selectbutton'
import { usePreferences } from '@/shared/lib/preferences'

const { t, locale } = useI18n()
const { language } = storeToRefs(usePreferences())
watch(
  language,
  (value) => {
    locale.value = value
    document.documentElement.lang = value
  },
  { immediate: true },
)
const languages = [
  { label: 'FR', value: 'fr' },
  { label: 'EN', value: 'en' },
]
</script>

<template>
  <header class="top">
    <RouterLink to="/" class="brand">{{ t('app.title') }}</RouterLink>
    <nav class="nav">
      <RouterLink :to="{ name: 'workshop' }">{{ t('app.workshop') }}</RouterLink>
    </nav>
    <SelectButton
      v-model="language"
      :options="languages"
      option-label="label"
      option-value="value"
      :allow-empty="false"
      :aria-label="t('app.language')"
    />
  </header>
  <RouterView />
  <ConfirmDialog />
</template>

<style scoped>
.top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
}
.nav {
  flex: 1;
  margin-left: 1.5rem;
}
.nav a {
  color: var(--p-text-muted-color);
  text-decoration: none;
  font-weight: 600;
}
.nav a.router-link-active {
  color: var(--p-primary-color);
}
.brand {
  font-family: Georgia, serif;
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--p-primary-color);
  text-decoration: none;
}
</style>
