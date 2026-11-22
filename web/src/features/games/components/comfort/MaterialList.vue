<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import { materialKinds, type Material } from '../../lib/comfort'
import { shortId } from '../../lib/ids'

/** What to take out of the box (play-spec M8), ticked as it goes. The ticks stay in this browser. */
const props = defineProps<{ material: Material; storageKey: string }>()
const { t } = useI18n()
const ticked = ref<string[]>([])

watch(
  () => props.storageKey,
  (key) => {
    try {
      ticked.value = JSON.parse(localStorage.getItem(key) ?? '[]') as string[]
    } catch {
      ticked.value = []
    }
  },
  { immediate: true },
)
watch(ticked, (value) => {
  try {
    localStorage.setItem(props.storageKey, JSON.stringify(value))
  } catch {
    // a convenience only
  }
})

const lists = computed(() => materialKinds.filter((kind) => props.material[kind].length))
const lines = computed(() => lists.value.flatMap((kind) => props.material[kind].map((line) => `${kind}|${line.id}`)))
const left = computed(() => lines.value.filter((line) => !ticked.value.includes(line)).length)
</script>

<template>
  <div>
    <p class="left" role="status">
      {{ left ? t('comfort.material.left', { n: left }) : t('comfort.material.done') }}
      <Button :label="t('comfort.material.clear')" size="small" text @click="ticked = []" />
    </p>
    <section v-for="kind in lists" :key="kind">
      <h4>{{ t(`comfort.material.${kind}`) }}</h4>
      <label v-for="line in material[kind]" :key="line.id">
        <Checkbox v-model="ticked" :value="`${kind}|${line.id}`" />
        <span>{{ kind === 'bases' ? t('comfort.material.base', { n: line.id }) : shortId(line.id) }}</span>
        <strong v-if="line.count > 1">×{{ line.count }}</strong>
      </label>
    </section>
  </div>
</template>

<style scoped>
.left {
  margin: 0;
}
section {
  margin-bottom: 0.5rem;
}
h4 {
  margin: 0.4rem 0 0.2rem;
}
label {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 13rem;
  padding: 0.2rem 0.8rem 0.2rem 0;
}
</style>
