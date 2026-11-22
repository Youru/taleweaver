<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import { usePreferences } from '@/shared/lib/preferences'
import { addAchievement, withXp, xpOf, type DraftAchievement } from '../lib/achievements'
import TextField from './TextField.vue'

/** The Achievements of an encounter (rulebook p. 17): condition, hidden text, the XP it gives. */
const props = defineProps<{ modelValue: DraftAchievement[] }>()
const emit = defineEmits<{ 'update:modelValue': [value: DraftAchievement[]] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

function change(index: number, next: DraftAchievement) {
  emit(
    'update:modelValue',
    props.modelValue.map((item, i) => (i === index ? next : item)),
  )
}
</script>

<template>
  <h3>{{ t('achievements.title') }}</h3>
  <ol class="list">
    <li v-for="(item, index) in modelValue" :key="item.id">
      <TextField
        :model-value="item.name"
        :label="t('achievements.name')"
        @update:model-value="(v) => change(index, { ...item, name: v ?? {} })"
      />
      <TextField
        :model-value="item.condition"
        :label="t('achievements.condition')"
        long
        :rows="2"
        @update:model-value="(v) => change(index, { ...item, condition: v })"
      />
      <TextField
        :model-value="item.hiddenText"
        :label="t('achievements.hiddenText')"
        long
        :rows="2"
        @update:model-value="(v) => change(index, { ...item, hiddenText: v })"
      />
      <label class="xp">
        {{ t('achievements.xp') }}
        <InputNumber
          :model-value="xpOf(item)"
          :min="0"
          :max="20"
          show-buttons
          @update:model-value="(v) => change(index, withXp(item, v ?? 0))"
        />
      </label>
      <Button
        :label="t('achievements.remove')"
        icon="pi pi-times"
        size="small"
        severity="secondary"
        text
        @click="
          emit(
            'update:modelValue',
            modelValue.filter((_, i) => i !== index),
          )
        "
      />
    </li>
  </ol>
  <Button
    :label="t('achievements.add')"
    icon="pi pi-plus"
    size="small"
    text
    @click="emit('update:modelValue', addAchievement(modelValue, language))"
  />
</template>

<style scoped>
.list {
  list-style: none;
  margin: 0;
  padding: 0;
}
li {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding: 0.5rem 0;
}
.xp {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
</style>
