<script setup lang="ts">
import { watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import SelectButton from 'primevue/selectbutton'
import { HelpMenu } from '@/features/manual'
import { openRules, ReferenceDialog } from '@/features/reference'
import ActionConfirm from '@/shared/components/ActionConfirm.vue'
import { isHostDevice } from '@/shared/lib/screen'
import { usePreferences } from '@/shared/lib/preferences'

const { t, locale } = useI18n()
const { language } = storeToRefs(usePreferences())
const route = useRoute()
const host = isHostDevice()
watch(
  language,
  (value) => {
    locale.value = value
    document.documentElement.lang = value
  },
  { immediate: true },
)
/** From a game the rules open over it; elsewhere they are a page. */
function toRules(event: MouseEvent) {
  if (!route.path.startsWith('/games/') || event.ctrlKey || event.metaKey || event.shiftKey) return
  event.preventDefault()
  openRules()
}
const languages = [
  { label: 'FR', value: 'fr' },
  { label: 'EN', value: 'en' },
]
</script>

<template>
  <header v-if="!route.meta.bare" class="top" :class="{ game: route.meta.game }">
    <RouterLink to="/" class="brand">{{ t('app.title') }}</RouterLink>
    <!-- A game fills the banner itself (GameBar) -->
    <div v-show="route.meta.game" id="app-bar" class="slot" />
    <nav v-if="!route.meta.game" class="nav">
      <RouterLink v-if="host" :to="{ name: 'workshop' }">{{ t('app.workshop') }}</RouterLink>
      <a v-if="route.path.startsWith('/games/')" href="/rules" :title="t('reference.shortcut')" @click="toRules">
        {{ t('reference.open') }}
      </a>
      <RouterLink v-else :to="{ name: 'rules' }" :title="t('reference.shortcut')">{{ t('reference.open') }}</RouterLink>
    </nav>
    <!-- Help stays apart from the features: the manual, at the section of this page -->
    <HelpMenu v-if="!route.meta.game" />
    <SelectButton
      v-if="!route.meta.game"
      v-model="language"
      :options="languages"
      option-label="label"
      option-value="value"
      :allow-empty="false"
      :aria-label="t('app.language')"
    />
  </header>
  <RouterView />
  <ReferenceDialog />
  <ActionConfirm />
</template>

<style scoped>
/* The top bar as an encounter banner of the books */
.top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem 1.25rem;
  margin-bottom: 0.75rem;
  background: var(--mid-crimson) var(--mid-banner) left center / cover;
  border-bottom: 2px solid #3a0d0d;
  box-shadow: 0 2px 8px rgb(0 0 0 / 0.35);
}
.top.game {
  box-sizing: border-box;
  min-height: 3.25rem;
  padding: 0.2rem 0.75rem;
  margin-bottom: 0.5rem;
}
.game .brand {
  font-size: 1.3rem;
}
.slot {
  flex: 1;
  min-width: 0;
  margin-left: 0.75rem;
}
.nav {
  flex: 1;
  margin-left: 1.5rem;
}
.nav a + a {
  margin-left: 1.25rem;
}
.nav a {
  color: rgb(243 233 216 / 0.75);
  text-decoration: none;
  font-family: var(--mid-title-font);
  font-variant: small-caps;
  font-weight: 600;
  letter-spacing: 0.06em;
}
.nav a.router-link-active,
.nav a:hover {
  color: var(--mid-cream);
}
.brand {
  font-family: var(--mid-title-font);
  font-variant: small-caps;
  font-size: 1.6rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  color: var(--mid-cream);
  text-decoration: none;
  text-shadow: 0 1px 2px rgb(0 0 0 / 0.5);
}
@media (max-width: 40rem) {
  .top.game {
    flex-wrap: wrap;
  }
  .game .slot {
    flex-basis: 100%;
    margin-left: 0;
  }
}
@media (max-width: 30rem) {
  .top {
    flex-wrap: wrap;
    row-gap: 0.4rem;
  }
  .nav {
    order: 1;
    flex-basis: 100%;
    margin-left: 0;
  }
}
</style>
