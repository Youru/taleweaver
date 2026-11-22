<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import RulesBrowser from '../components/RulesBrowser.vue'

/** The rules out of any game (/rules): the question and the rule open are in the address. */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()

// The two can change in the same tick: both leave together
let waiting: Record<string, string> | undefined
function inAddress(name: string) {
  return computed({
    get: () => String(waiting?.[name] ?? route.query[name] ?? ''),
    set(value: string) {
      waiting = { ...(waiting ?? (route.query as Record<string, string>)), [name]: value }
      const query = Object.fromEntries(Object.entries(waiting).filter(([, kept]) => kept))
      void router.replace({ query }).then(() => (waiting = undefined))
    },
  })
}
const typed = inAddress('q')
const section = inAddress('s')
</script>

<template>
  <main class="page">
    <h1>{{ t('reference.title') }}</h1>
    <RulesBrowser v-model:typed="typed" v-model:section="section" class="browser" />
  </main>
</template>

<style scoped>
.page {
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  height: calc(100dvh - 5.5rem);
  max-width: 80rem;
  margin: 0 auto;
  padding: 0 1rem 1rem;
}
h1 {
  margin: 0 0 0.5rem;
  font-size: 1.5rem;
}
.browser {
  flex: 1;
}
@media (max-width: 30rem) {
  .page {
    height: calc(100dvh - 7.5rem);
  }
}
</style>
