<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import Dialog from 'primevue/dialog'
import { rulesOpen } from '../lib/panel'
import RulesBrowser from './RulesBrowser.vue'

/** The rules over any screen, without leaving it (Ctrl+K). The last question is still there when it opens again. */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const typed = ref('')
const section = ref('')
const browser = useTemplateRef<{ focus: () => void }>('browser')

function shortcut(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'k' || route.meta.bare) return
  event.preventDefault()
  if (route.name === 'rules') document.querySelector<HTMLInputElement>('input[type="search"]')?.select()
  else if (rulesOpen.value) browser.value?.focus()
  else rulesOpen.value = true
}
onMounted(() => window.addEventListener('keydown', shortcut))
onBeforeUnmount(() => window.removeEventListener('keydown', shortcut))
</script>

<template>
  <Dialog
    v-model:visible="rulesOpen"
    modal
    dismissable-mask
    :header="t('reference.title')"
    :style="{ width: 'min(76rem, 97vw)', height: '90dvh' }"
    :content-style="{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }"
    @show="browser?.focus()"
  >
    <RulesBrowser ref="browser" v-model:typed="typed" v-model:section="section" class="browser" />
    <a
      class="apart"
      target="_blank"
      rel="noopener"
      :href="
        router.resolve({ name: 'rules', query: { ...(typed && { q: typed }), ...(section && { s: section }) } }).href
      "
    >
      {{ t('reference.apart') }}
    </a>
  </Dialog>
</template>

<style scoped>
.browser {
  flex: 1;
}
.apart {
  align-self: flex-end;
  padding-top: 0.4rem;
  font-size: 0.8rem;
  color: var(--mid-crimson);
}
</style>
