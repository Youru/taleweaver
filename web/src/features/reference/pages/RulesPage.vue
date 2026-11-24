<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import RulesBrowser from '../components/RulesBrowser.vue'

/**
 * The rules out of any game (/rules): the question and the rule open are copied into the address. The page holds
 * them itself: the address follows, it never leads while one types (a field fed back by the router lags behind).
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const typed = ref(String(route.query.q ?? ''))
const section = ref(String(route.query.s ?? ''))
watch([typed, section], ([q, s]) => {
  const query = Object.fromEntries(Object.entries({ q, s }).filter(([, kept]) => kept))
  void router.replace({ query })
})
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
