<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import JsonField from './JsonField.vue'

/**
 * The figures an encounter describes (data-model §3.10, play-spec A12): numbers in the place of a card's, placed or
 * called by `ref: "figure:<id>"`; and the Espers its opponents may summon (A21). Field by field, the rare fields in
 * JSON beneath.
 */
type Figure = {
  id: string
  name?: Record<string, string>
  stats?: Record<string, unknown>
  side?: string
  attackable?: boolean
  loot?: boolean
  [field: string]: unknown
}
type Esper = { ref?: string; summoner?: string; [field: string]: unknown }

const figures = defineModel<Figure[] | undefined>('figures')
const espers = defineModel<Esper[] | undefined>('espers')
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const stats = ['health', 'defense', 'armor', 'movement'] as const
const sides = [
  { value: undefined, label: t('workshop.figuresEditor.opponents') },
  { value: 'players', label: t('workshop.figuresEditor.players') },
]

function setFigure(index: number, fields: Partial<Figure>) {
  figures.value = (figures.value ?? []).map((f, i) => {
    if (i !== index) return f
    const next: Figure = { ...f, ...fields }
    for (const [k, v] of Object.entries(fields)) if (v === undefined || v === '') delete next[k]
    return next
  })
}
function setStat(index: number, stat: string, value: number | null) {
  const current = { ...(figures.value?.[index]?.stats ?? {}) }
  if (value == null) delete current[stat]
  else current[stat] = value
  setFigure(index, { stats: Object.keys(current).length ? current : undefined })
}
function addFigure() {
  figures.value = [...(figures.value ?? []), { id: `figure-${(figures.value?.length ?? 0) + 1}`, stats: { health: 6 } }]
}
function removeFigure(index: number) {
  const next = (figures.value ?? []).filter((_, i) => i !== index)
  figures.value = next.length ? next : undefined
}

function setEsper(index: number, fields: Partial<Esper>) {
  espers.value = (espers.value ?? []).map((e, i) => (i === index ? { ...e, ...fields } : e))
}
function removeEsper(index: number) {
  const next = (espers.value ?? []).filter((_, i) => i !== index)
  espers.value = next.length ? next : undefined
}
</script>

<template>
  <section class="figures">
    <h4>{{ t('workshop.figures') }}</h4>
    <ol>
      <li v-for="(figure, index) in figures ?? []" :key="index">
        <div class="row">
          <InputText
            :model-value="figure.id"
            size="small"
            :aria-label="t('workshop.figuresEditor.id')"
            :placeholder="t('workshop.figuresEditor.id')"
            @change="(e) => setFigure(index, { id: (e.target as HTMLInputElement).value.trim() || figure.id })"
          />
          <InputText
            :model-value="pick(figure.name, language)"
            size="small"
            :aria-label="t('workshop.figuresEditor.name')"
            :placeholder="t('workshop.figuresEditor.name')"
            @change="
              (e) =>
                setFigure(index, {
                  name: { ...(figure.name ?? {}), [language]: (e.target as HTMLInputElement).value },
                })
            "
          />
          <Button
            icon="pi pi-times"
            size="small"
            text
            severity="secondary"
            :aria-label="t('workshop.figuresEditor.remove', { id: figure.id })"
            @click="removeFigure(index)"
          />
        </div>
        <div class="row">
          <InputNumber
            v-for="stat in stats"
            :key="stat"
            :model-value="typeof figure.stats?.[stat] === 'number' ? (figure.stats[stat] as number) : null"
            size="small"
            input-class="num"
            :placeholder="t(`workshop.events.stat.${stat}`)"
            :aria-label="t(`workshop.events.stat.${stat}`)"
            @update:model-value="(v) => setStat(index, stat, v)"
          />
          <Select
            :model-value="figure.side"
            :options="sides"
            option-label="label"
            option-value="value"
            size="small"
            :aria-label="t('workshop.figuresEditor.side')"
            @update:model-value="(v) => setFigure(index, { side: v })"
          />
          <label class="inline">
            <ToggleSwitch
              :model-value="figure.attackable !== false"
              @update:model-value="(v) => setFigure(index, { attackable: v ? undefined : false })"
            />
            {{ t('workshop.figuresEditor.attackable') }}
          </label>
          <label class="inline">
            <ToggleSwitch
              :model-value="figure.loot === true"
              @update:model-value="(v) => setFigure(index, { loot: v || undefined })"
            />
            {{ t('workshop.figuresEditor.loot') }}
          </label>
        </div>
      </li>
    </ol>
    <Button
      icon="pi pi-plus"
      :label="t('workshop.figuresEditor.add')"
      size="small"
      severity="secondary"
      @click="addFigure"
    />

    <h4>{{ t('workshop.encounterEspers') }}</h4>
    <ol>
      <li v-for="(esper, index) in espers ?? []" :key="index" class="row">
        <InputText
          :model-value="esper.ref ?? ''"
          size="small"
          :placeholder="t('workshop.figuresEditor.esperCard')"
          :aria-label="t('workshop.figuresEditor.esperCard')"
          @change="(e) => setEsper(index, { ref: (e.target as HTMLInputElement).value.trim() })"
        />
        <InputText
          :model-value="esper.summoner ?? ''"
          size="small"
          :placeholder="t('workshop.figuresEditor.summoner')"
          :aria-label="t('workshop.figuresEditor.summoner')"
          @change="(e) => setEsper(index, { summoner: (e.target as HTMLInputElement).value.trim() })"
        />
        <Button
          icon="pi pi-times"
          size="small"
          text
          severity="secondary"
          :aria-label="t('workshop.figuresEditor.removeEsper')"
          @click="removeEsper(index)"
        />
      </li>
    </ol>
    <Button
      icon="pi pi-plus"
      :label="t('workshop.figuresEditor.addEsper')"
      size="small"
      severity="secondary"
      @click="espers = [...(espers ?? []), { ref: '', summoner: '' }]"
    />

    <details class="more">
      <summary>{{ t('workshop.events.moreJson') }}</summary>
      <JsonField v-model="figures" :label="t('workshop.figures')" />
      <JsonField v-model="espers" :label="t('workshop.encounterEspers')" />
    </details>
  </section>
</template>

<style scoped>
.figures {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.5rem;
}
h4 {
  margin: 0.5rem 0 0;
}
ol {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: 100%;
  margin: 0;
  padding: 0;
  list-style: none;
}
li {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.row :deep(.num) {
  width: 4.5rem;
}
.inline {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
}
.more {
  width: 100%;
}
</style>
