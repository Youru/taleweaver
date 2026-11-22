<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Select from 'primevue/select'
import { useDisciplines } from '@/features/catalog'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { AdventurerState, PartyCommand } from '../api/types'

/** A newcomer made ready: their XP spent, their items, their player. The Active Group is asked after. */
const props = defineProps<{ member: AdventurerState; gold: number; toHandOut: number; busy: boolean }>()
const emit = defineEmits<{ party: [command: PartyCommand]; inventory: []; room: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const disciplines = useDisciplines()

const left = computed(() => props.member.xp - (props.member.disciplines ?? []).reduce((sum, d) => sum + d.xp, 0))
const options = computed(() =>
  (disciplines.data.value ?? [])
    .filter((d) => !d.story && !props.member.disciplines?.some((known) => known.id === d.id))
    .map((d) => ({
      value: d.id,
      cost: d.cost ?? 0,
      label: `${pick(d.name, language.value) || d.id} (${d.tree} ${d.level ?? ''})`,
    }))
    .sort((a, b) => a.label.localeCompare(b.label)),
)
const chosen = ref<string>()

function learn() {
  if (!chosen.value) return
  // The engine counts the catalogue's cost, and refuses a level not reached
  const xp = options.value.find((o) => o.value === chosen.value)?.cost ?? 0
  emit('party', { type: 'learn', adventurer: props.member.name, discipline: chosen.value, xp })
  chosen.value = undefined
}
</script>

<template>
  <section class="arrival" :aria-label="t('arrival.title', { name: member.name })">
    <h3>{{ t('arrival.title', { name: member.name }) }}</h3>
    <ol>
      <li>
        <span>{{ t('arrival.disciplines', { left, known: member.disciplines?.length ?? 0 }) }}</span>
        <template v-if="left > 0">
          <Select
            v-model="chosen"
            :options="options"
            option-label="label"
            option-value="value"
            filter
            :placeholder="t('arrival.discipline')"
            :aria-label="t('arrival.discipline')"
          />
          <Button :label="t('arrival.learn')" size="small" :disabled="busy || !chosen" @click="learn" />
        </template>
      </li>
      <li>
        <span>{{
          t('arrival.items', {
            equipped: member.equipped?.length ?? 0,
            pack: member.pack?.length ?? 0,
            toHandOut,
            gold,
          })
        }}</span>
        <Button :label="t('inventory.open')" icon="pi pi-briefcase" size="small" severity="secondary" @click="emit('inventory')" />
      </li>
      <li>
        <span>{{ t('arrival.seat') }}</span>
        <Button :label="t('room.gm.button')" icon="pi pi-mobile" size="small" severity="secondary" @click="emit('room')" />
      </li>
      <li>{{ t('arrival.group') }}</li>
    </ol>
  </section>
</template>

<style scoped>
.arrival {
  padding: 0.6rem 1rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
h3 {
  margin: 0;
}
ol {
  margin: 0.5rem 0 0;
  padding-left: 1.2rem;
}
li {
  margin-bottom: 0.4rem;
}
li > span {
  margin-right: 0.5rem;
}
li :deep(.p-select) {
  max-width: 100%;
  margin-right: 0.4rem;
}
</style>
