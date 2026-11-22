<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import { useEffects, useInitiativeCards } from '@/features/catalog'
import { InitiativeTrack, initiativeTrack, type EncounterState } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerCommand, PlayerView } from '../api/types'
import { turnOf } from '../lib/room'
import AlliesSection from './AlliesSection.vue'
import HandSection from './HandSection.vue'

/**
 * The encounter as the table sees it (room-spec §5.1, Tour): whose turn it is, the initiative track, the effects
 * on the adventurer followed, the opponents on the board and the party's health.
 */
const props = defineProps<{
  view: PlayerView
  followed: string
  /** Report to the GM the damage taken or an effect (room-spec §6.2): a request, never applied alone. */
  act?: (command: PlayerCommand) => Promise<void>
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const effects = useEffects()
const initiative = useInitiativeCards()

const encounter = computed(() => props.view.encounter!)
/** The track component reads an encounter of the GM's: the phone's carries the fields it needs. */
const asEncounter = computed(() => encounter.value as unknown as EncounterState)
const mine = computed(() => props.view.adventurers.map((a) => a.name))
const followedState = computed(() => props.view.adventurers.find((a) => a.name === props.followed))
const turn = computed(() => turnOf(encounter.value, mine.value))
const labelOf = (card: string | undefined) =>
  initiativeTrack(asEncounter.value, initiative.data.value ?? []).find((c) => c.id === card)?.label ?? card ?? ''

const effectOf = (id: string) => effects.data.value?.find((e) => e.id === id)
const myEffects = computed(() => encounter.value.adventurerEffects[props.followed] ?? [])

// What the player tells the GM: an amount of damage or healing, an effect gained or lost
const amount = ref(1)
const newEffect = ref<string>()
const reported = ref<string>()
const failure = ref<string>()
const effectChoices = computed(() =>
  (effects.data.value ?? [])
    .filter((e) => !myEffects.value.includes(e.id))
    .map((e) => ({ value: e.id, label: pick(e.name, language.value) || e.id })),
)
async function report(command: PlayerCommand) {
  if (!props.act) return
  failure.value = undefined
  try {
    await props.act(command)
    reported.value = t('room.player.reported')
    newEffect.value = undefined
  } catch (e) {
    failure.value = (e as Error).message
  }
}

const opponents = computed(() => encounter.value.combatants.filter((c) => !c.defeated))
</script>

<template>
  <section class="turn">
    <p class="round">
      {{ t('room.player.round', { round: encounter.round }) }} ·
      {{ t('room.player.urgency', { urgency: encounter.urgency }) }}
    </p>

    <div v-if="turn.playing" class="call mine" role="status">
      <i class="pi pi-bolt" aria-hidden="true" />
      {{ t('room.player.yourTurn', { name: turn.playing }) }}
    </div>
    <div v-else-if="turn.next" class="call soon" role="status">
      <i class="pi pi-hourglass" aria-hidden="true" />
      {{ t('room.player.getReady', { name: turn.next, after: labelOf(turn.after) }) }}
    </div>

    <HandSection v-if="act && followedState" :key="followed" :view="view" :adventurer="followedState" :act="act" />

    <InitiativeTrack v-if="encounter.round > 0" :encounter="asEncounter" large />
    <p v-else class="muted">{{ t('room.player.notStarted') }}</p>

    <h2>{{ t('room.player.effectsOf', { name: followed }) }}</h2>
    <p v-if="myEffects.length === 0" class="muted">{{ t('room.player.noEffect') }}</p>
    <ul v-else class="effects">
      <li v-for="id in myEffects" :key="id">
        <details>
          <summary :class="effectOf(id)?.polarity">{{ pick(effectOf(id)?.name, language) || id }}</summary>
          <p>{{ pick(effectOf(id)?.text, language) }}</p>
          <Button
            v-if="act"
            icon="pi pi-times"
            :label="t('room.player.effectGone')"
            size="small"
            severity="secondary"
            text
            @click="report({ type: 'effect', adventurer: followed, effect: id, active: false })"
          />
        </details>
      </li>
    </ul>

    <template v-if="act">
      <h2>{{ t('room.player.report') }}</h2>
      <div class="report">
        <div class="amount" role="group" :aria-label="t('room.player.amount')">
          <Button icon="pi pi-minus" text rounded :disabled="amount <= 1" @click="amount--" />
          <strong>{{ amount }}</strong>
          <Button icon="pi pi-plus" text rounded :disabled="amount >= 20" @click="amount++" />
        </div>
        <Button
          icon="pi pi-heart"
          :label="t('room.player.tookDamage')"
          severity="danger"
          @click="report({ type: 'damage', adventurer: followed, amount })"
        />
        <Button
          icon="pi pi-plus-circle"
          :label="t('room.player.healed')"
          severity="success"
          outlined
          @click="report({ type: 'damage', adventurer: followed, amount: -amount })"
        />
      </div>
      <div class="report">
        <Select
          v-model="newEffect"
          :options="effectChoices"
          option-label="label"
          option-value="value"
          :placeholder="t('room.player.chooseEffect')"
          class="effect-select"
        />
        <Button
          icon="pi pi-plus"
          :label="t('room.player.gotEffect')"
          :disabled="!newEffect"
          severity="secondary"
          @click="newEffect && report({ type: 'effect', adventurer: followed, effect: newEffect, active: true })"
        />
      </div>
      <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
      <Message v-else-if="reported" severity="info" :closable="false" :life="2500">{{ reported }}</Message>
    </template>

    <AlliesSection :view="view" :followed="followed" :act="act" :amount="amount" />

    <h2>{{ t('room.player.opponents') }}</h2>
    <p v-if="opponents.length === 0" class="muted">{{ t('room.player.noOpponent') }}</p>
    <ul v-else class="opponents">
      <li v-for="c in opponents" :key="c.instance">
        <img v-if="c.image" :src="assetUrl(c.image, 160)" alt="" loading="lazy" />
        <span class="name">
          {{ pick(c.name, language) || c.family }}
          <small v-if="c.number">#{{ c.number }}</small>
        </span>
        <span class="hp">
          <i class="pi pi-heart-fill" aria-hidden="true" />
          {{ c.health == null ? `−${c.damage}` : `${c.health - c.damage}/${c.health}` }}
        </span>
        <span v-if="c.effects.length" class="tags">
          <span v-for="id in c.effects" :key="id">{{ pick(effectOf(id)?.name, language) || id }}</span>
        </span>
      </li>
    </ul>

    <h2>{{ t('room.gm.party') }}</h2>
    <ul class="party">
      <li
        v-for="member in view.party.filter((p) => p.active)"
        :key="member.name"
        :class="{ defeated: member.defeated }"
      >
        <span class="name">{{ member.name }}</span>
        <span class="hp">
          <i class="pi pi-heart-fill" aria-hidden="true" />
          {{ member.defeated ? t('room.player.defeated') : `${member.health - member.damage}/${member.health}` }}
        </span>
        <span v-if="encounter.adventurerEffects[member.name]?.length" class="tags">
          <span v-for="id in encounter.adventurerEffects[member.name]" :key="id">{{
            pick(effectOf(id)?.name, language) || id
          }}</span>
        </span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.report {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}
.report :deep(.p-button) {
  min-height: 3rem;
}
.amount {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 1.2rem;
}
.effect-select {
  flex: 1;
  min-width: 10rem;
}
.turn {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  padding: 0.75rem 1rem 1rem;
}
.round {
  margin: 0;
  font-weight: 600;
}
.muted {
  color: var(--p-text-muted-color);
  margin: 0;
}
h2 {
  font-size: 0.85rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--p-text-muted-color);
  margin: 0.6rem 0 0;
}
.call {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.7rem 0.9rem;
  border-radius: 0.6rem;
  font-weight: 600;
}
.call.mine {
  background: var(--seat);
  color: #fff;
}
.call.soon {
  border: 2px dashed var(--seat);
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.effects {
  flex-direction: row;
  flex-wrap: wrap;
}
summary {
  cursor: pointer;
  padding: 0.35rem 0.7rem;
  border-radius: 999px;
  background: var(--p-content-hover-background);
  min-height: 2rem;
}
summary.negative {
  background: color-mix(in srgb, #ef4444 25%, transparent);
}
summary.positive {
  background: color-mix(in srgb, #22c55e 25%, transparent);
}
details p {
  margin: 0.4rem 0 0;
  font-size: 0.9rem;
}
.opponents li,
.party li {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 0.4rem 0.5rem;
  border-radius: 0.5rem;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
}
.opponents img {
  width: 2.5rem;
  height: 2.5rem;
  object-fit: cover;
  border-radius: 0.4rem;
}
.name {
  flex: 1;
  font-weight: 600;
}
.hp {
  white-space: nowrap;
}
.hp .pi {
  color: #ef4444;
  font-size: 0.8rem;
}
.tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  width: 100%;
}
.tags span {
  font-size: 0.75rem;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
  background: var(--p-content-hover-background);
}
.party li.defeated {
  opacity: 0.55;
}
</style>
