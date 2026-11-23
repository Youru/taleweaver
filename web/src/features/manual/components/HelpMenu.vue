<script setup lang="ts">
import { computed, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Menu from 'primevue/menu'
import type { MenuItem } from 'primevue/menuitem'
import { helpSection } from '../lib/help'

/**
 * Help, apart from the features: the manual, opened at the section of the page in use. From a game it opens in
 * another tab, so that the game stays where it is.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const menu = useTemplateRef<{ toggle: (event: Event) => void }>('menu')

function open(hash?: string) {
  const target = router.resolve({ name: 'manual', hash: hash ? `#${hash}` : undefined })
  if (route.path.startsWith('/games/')) window.open(target.href, '_blank', 'noopener')
  else void router.push(target)
}

const items = computed<MenuItem[]>(() => {
  const section = helpSection(route.name)
  return [
    ...(section ? [{ label: t('help.thisPage'), icon: 'pi pi-info-circle', command: () => open(section) }] : []),
    { label: t('help.manual'), icon: 'pi pi-book', command: () => open() },
  ]
})
</script>

<template>
  <Button
    icon="pi pi-question-circle"
    text
    rounded
    class="help-button"
    :aria-label="t('help.open')"
    :title="t('help.open')"
    aria-haspopup="true"
    aria-controls="help-menu"
    @click="menu?.toggle($event)"
  />
  <Menu id="help-menu" ref="menu" :model="items" popup />
</template>

<style scoped>
.help-button {
  color: var(--mid-cream);
}
</style>
