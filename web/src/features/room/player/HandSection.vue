<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useDisciplines, useItems } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerCommand, PlayerView } from '../api/types'
import { turnOf } from '../lib/room'

/**
 * What is in the player's own hand (play-spec J4, J4b): the SP spent, the cards, the tokens apply at once; the
 * end of the turn waits for the GM.
 */
const props = defineProps<{
  view: PlayerView
  adventurer: AdventurerState
  act: (command: PlayerCommand) => Promise<void>
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const disciplines = useDisciplines()

const held = computed(() => props.adventurer.stamina ?? 0)
const pips = computed(() => Math.max(props.adventurer.staminaMax ?? 0, held.value))
const playing = computed(
  () => !!props.view.encounter && !!turnOf(props.view.encounter, [props.adventurer.name]).playing,
)
const done = computed(() =>
  props.view.requests.some(
    (r) => r.status === 'pending' && r.command.type === 'endTurn' && r.command.adventurer === props.adventurer.name,
  ),
)

const cards = computed(() => {
  const who = props.adventurer
  const marksOf = (id: string) => ({
    exhausted: !!who.exhaustedCards?.includes(id),
    used: !!who.usedCards?.includes(id),
  })
  return [
    ...(who.equipped ?? []).map((owned) => ({
      id: owned.id,
      name: pick(items.data.value?.find((i) => i.id === owned.item)?.name, language.value) || owned.id,
      on: { exhausted: !!owned.exhausted, used: !!owned.used },
    })),
    { id: `card:${who.name}`, name: t('marks.adventurerCard'), on: marksOf(`card:${who.name}`) },
    ...(who.disciplines ?? []).map(({ id }) => ({
      id,
      name: pick(disciplines.data.value?.find((d) => d.id === id)?.name, language.value) || id,
      on: marksOf(id),
    })),
  ]
})
const tokens = computed(() =>
  [...Object.entries(props.adventurer.abilityTokens ?? {}), ['summon', props.adventurer.summonTokens ?? 0] as const]
    .filter(([, count]) => count > 0)
    .map(([id, count]) => ({ id, count })),
)

const busy = ref(false)
const failure = ref<string>()
async function send(command: PlayerCommand) {
  busy.value = true
  failure.value = undefined
  try {
    await props.act(command)
  } catch (e) {
    failure.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
/** A pip full is spent with those after it; an empty one tapped by mistake comes back. */
function tick(n: number) {
  void send({ type: 'stamina', adventurer: props.adventurer.name, amount: n <= held.value ? n - 1 : n })
}
</script>

<template>
  <section class="hand">
    <div v-if="pips" class="sp" role="group" :aria-label="t('room.player.spTick')">
      <span>{{ t('room.player.stamina') }}</span>
      <button
        v-for="n in pips"
        :key="n"
        type="button"
        class="pip"
        :class="{ full: n <= held }"
        :aria-pressed="n <= held"
        :aria-label="t('room.player.spPip', { n })"
        :disabled="busy"
        @click="tick(n)"
      />
    </div>
    <Button
      v-if="playing"
      icon="pi pi-flag"
      :label="done ? t('room.player.doneWaiting') : t('room.player.done')"
      :disabled="busy || done"
      @click="send({ type: 'endTurn', adventurer: adventurer.name })"
    />
    <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
    <details>
      <summary>{{ t('room.player.myCards') }}</summary>
      <div v-for="token in tokens" :key="token.id" class="line">
        <span class="name">{{ t(`marks.token.${token.id}`) }} ×{{ token.count }}</span>
        <Button
          :label="t('room.player.spendToken')"
          size="small"
          severity="secondary"
          :disabled="busy"
          @click="send({ type: 'spendToken', adventurer: adventurer.name, token: token.id })"
        />
      </div>
      <div v-for="card in cards" :key="card.id" class="line">
        <span class="name">{{ card.name }}</span>
        <Button
          v-for="mark in ['exhausted', 'used'] as const"
          :key="mark"
          :label="t(`marks.mark.${mark}`)"
          size="small"
          :severity="card.on[mark] ? 'warn' : 'secondary'"
          :outlined="!card.on[mark]"
          :aria-pressed="card.on[mark]"
          :disabled="busy"
          @click="send({ type: 'mark', adventurer: adventurer.name, card: card.id, effect: mark, active: !card.on[mark] })"
        />
      </div>
    </details>
  </section>
</template>

<style scoped>
.hand {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.sp {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.pip {
  width: 2.5rem;
  height: 2.5rem;
  border: 2px solid var(--seat, var(--p-primary-color));
  border-radius: 50%;
  background: transparent;
}
.pip.full {
  background: var(--seat, var(--p-primary-color));
}
summary {
  padding: 0.5rem 0;
}
.line {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.3rem 0;
}
.name {
  flex: 1;
}
</style>
