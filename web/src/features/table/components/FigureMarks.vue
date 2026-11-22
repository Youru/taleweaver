<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Select from 'primevue/select'
import { useDisciplines, useItems, type Combatant } from '@/features/catalog'
import type { AdventurerState, EncounterCommand, EncounterState } from '@/features/games'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'

/**
 * What lies on a figure and its cards: Ability tokens, cards Exhausted, Used or Flipped, the Tether; for a
 * combatant, what a rule changed (stats, tags, side, sleep).
 */
const props = defineProps<{
  id: string
  encounter: EncounterState
  party: AdventurerState[] | undefined
  cards: Map<string, Combatant>
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const disciplines = useDisciplines()

const member = computed(() => props.party?.find((a) => `adventurer:${a.name}` === props.id))
const ally = computed(() => props.encounter.allies?.find((a) => `ally:${a.id}` === props.id))
const combatant = computed(() => props.encounter.combatants.find((c) => c.instance === props.id))

/** Tokens and their limits (rulebook p. 95). */
const tokenLimits: Record<string, number> = { dodge: 3, heal: 4, intervention: 1, vow: 5, energy: 99, summon: 99 }
const tokens = computed(() =>
  (member.value ? Object.keys(tokenLimits) : Object.keys(tokenLimits).filter((k) => k !== 'summon')).map((id) => ({
    id,
    most: tokenLimits[id] ?? 99,
    count:
      id === 'summon' && member.value
        ? (member.value.summonTokens ?? 0)
        : ((member.value ?? ally.value ?? combatant.value)?.abilityTokens?.[id] ?? 0),
  })),
)
function setToken(token: string, value: number) {
  emit('command', { type: 'abilityToken', figure: props.id, token, value })
}

const marks = ['exhausted', 'used', 'flipped'] as const
type Mark = (typeof marks)[number]
interface MarkedCard {
  id: string
  name: string
  on: Record<Mark, boolean>
  can: readonly Mark[]
}
const cardsHeld = computed<MarkedCard[]>(() => {
  const held = ally.value
  if (held) {
    return [
      {
        id: held.card,
        name: pick(held.name, language.value) || held.id,
        can: ['exhausted', 'used'],
        on: {
          exhausted: !!held.exhaustedCards?.includes(held.card),
          used: !!held.usedCards?.includes(held.card),
          flipped: false,
        },
      },
    ]
  }
  const who = member.value
  if (!who) return []
  const itemName = (id?: string | null) => pick(items.data.value?.find((i) => i.id === id)?.name, language.value)
  const disciplineName = (id: string) => pick(disciplines.data.value?.find((d) => d.id === id)?.name, language.value)
  const others = [
    { id: `card:${who.name}`, name: t('marks.adventurerCard') },
    ...(who.disciplines ?? []).map((d) => ({ id: d.id, name: disciplineName(d.id) || d.id })),
  ]
  return [
    ...(who.equipped ?? []).map((o) => ({
      id: o.id,
      name: itemName(o.item) || o.id,
      can: marks,
      on: { exhausted: !!o.exhausted, used: !!o.used, flipped: !!o.flipped },
    })),
    ...others.map(({ id, name }) => ({
      id,
      name,
      can: marks,
      on: {
        exhausted: !!who.exhaustedCards?.includes(id),
        used: !!who.usedCards?.includes(id),
        flipped: !!who.flippedCards?.includes(id),
      },
    })),
  ]
})
const markedCount = computed(() => cardsHeld.value.filter((c) => marks.some((m) => c.on[m])).length)

const tetherTarget = ref<string>()
watch(
  () => props.id,
  () => (tetherTarget.value = undefined),
)
const others = computed(() =>
  [
    ...props.encounter.adventurers.filter((a) => !a.defeated).map((a) => `adventurer:${a.name}`),
    ...(props.encounter.allies ?? []).filter((a) => !a.defeated).map((a) => `ally:${a.id}`),
    ...props.encounter.combatants.filter((c) => !c.defeated).map((c) => c.instance),
  ]
    .filter((id) => id !== props.id)
    .map((value) => ({ value, label: value.replace(/^(adventurer|ally):/, '') })),
)
const tethered = computed(() => props.encounter.tethers?.[props.id])

/** What the rules of the encounter changed on this figure. */
const changed = computed(() => {
  const c = combatant.value
  if (!c) return []
  const name = pick(c.name, language.value)
  return [
    name,
    ...(c.side ? [c.side === 'players' ? t('marks.withParty') : t('marks.side', { side: c.side })] : []),
    ...(c.asleep ? [t('marks.asleep')] : []),
    ...(c.untargetable ? [t('marks.untargetable')] : []),
    ...(c.summoner ? [t('marks.summonedBy', { name: c.summoner, force: c.force ?? '?' })] : []),
    ...(['defense', 'armor', 'movement'] as const).flatMap((key) =>
      c.stats?.[key] === undefined || c.stats[key] === null ? [] : [t(`marks.stat.${key}`, { n: c.stats[key] })],
    ),
    ...(c.addedTags ?? []),
    ...(c.removedTags ?? []).map((tag) => t('marks.without', { tag })),
    ...(c.skippedSteps?.length ? [t('marks.skipped', { steps: c.skippedSteps.join(', ') })] : []),
  ].filter(Boolean)
})
</script>

<template>
  <div class="marks">
    <p v-if="changed.length" class="changed">{{ changed.join(' · ') }}</p>
    <details>
      <summary>
        {{ t('marks.title') }}
        <span v-if="markedCount">({{ t('marks.count', { n: markedCount }) }})</span>
      </summary>
      <div class="tokens">
        <span v-for="token in tokens" :key="token.id" class="token">
          {{ t(`marks.token.${token.id}`) }}
          <Button
            icon="pi pi-minus"
            text
            rounded
            size="small"
            :aria-label="t('marks.less', { token: t(`marks.token.${token.id}`) })"
            :disabled="locked || !token.count"
            @click="setToken(token.id, token.count - 1)"
          />
          <strong>{{ token.count }}</strong>
          <Button
            icon="pi pi-plus"
            text
            rounded
            size="small"
            :aria-label="t('marks.more', { token: t(`marks.token.${token.id}`) })"
            :title="token.count >= token.most ? t('marks.limit', { n: token.most }) : undefined"
            :disabled="locked || token.count >= token.most"
            @click="setToken(token.id, token.count + 1)"
          />
        </span>
      </div>
      <div v-for="card in cardsHeld" :key="card.id" class="card">
        <span class="name">{{ card.name }}</span>
        <Button
          v-for="mark in card.can"
          :key="mark"
          :label="t(`marks.mark.${mark}`)"
          size="small"
          :outlined="!card.on[mark]"
          severity="secondary"
          :aria-pressed="card.on[mark]"
          :disabled="locked"
          @click="emit('command', { type: 'card', figure: id, card: card.id, mark, value: !card.on[mark] })"
        />
      </div>
      <div class="card">
        <template v-if="tethered">
          <span class="name">{{ t('marks.tethered', { name: tethered.replace(/^(adventurer|ally):/, '') }) }}</span>
          <Button :label="t('marks.untether')" size="small" text :disabled="locked" @click="emit('command', { type: 'tether', figure: id })" />
        </template>
        <template v-else>
          <Select
            v-model="tetherTarget"
            :options="others"
            option-label="label"
            option-value="value"
            size="small"
            :placeholder="t('marks.tetherTo')"
            :aria-label="t('marks.tetherTo')"
          />
          <Button
            :label="t('marks.tether')"
            size="small"
            text
            :disabled="locked || !tetherTarget"
            @click="emit('command', { type: 'tether', figure: id, target: tetherTarget })"
          />
        </template>
      </div>
    </details>
  </div>
</template>

<style scoped>
.marks {
  font-size: 0.9rem;
}
.changed {
  margin: 0.3rem 0;
  color: var(--p-text-muted-color);
}
summary {
  cursor: pointer;
}
.tokens {
  display: flex;
  flex-wrap: wrap;
  gap: 0 0.6rem;
}
.token {
  display: inline-flex;
  align-items: center;
}
.card {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.2rem;
  margin-top: 0.25rem;
}
.card :deep(.p-button-sm) {
  padding: 0.1rem 0.35rem;
}
.name {
  flex: 1 1 8rem;
}
</style>
