<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { useEffects, useInitiativeCards } from '@/features/catalog'
import { InitiativeTrack, initiativeTrack, type EncounterState } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerView } from '../api/types'
import { turnOf } from '../lib/room'

/**
 * The encounter as the table sees it (room-spec §5.1, Tour): whose turn it is, the initiative track, the effects
 * on the adventurer followed, the opponents on the board and the party's health.
 */
const props = defineProps<{ view: PlayerView; followed: string }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const effects = useEffects()
const initiative = useInitiativeCards()

const encounter = computed(() => props.view.encounter!)
/** The track component reads an encounter of the GM's: the phone's carries the fields it needs. */
const asEncounter = computed(() => encounter.value as unknown as EncounterState)
const mine = computed(() => props.view.adventurers.map((a) => a.name))
const turn = computed(() => turnOf(encounter.value, mine.value))
const labelOf = (card: string | undefined) =>
  initiativeTrack(asEncounter.value, initiative.data.value ?? []).find((c) => c.id === card)?.label ?? card ?? ''

const effectOf = (id: string) => effects.data.value?.find((e) => e.id === id)
const myEffects = computed(() => encounter.value.adventurerEffects[props.followed] ?? [])

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

    <InitiativeTrack v-if="encounter.round > 0" :encounter="asEncounter" large />
    <p v-else class="muted">{{ t('room.player.notStarted') }}</p>

    <h2>{{ t('room.player.effectsOf', { name: followed }) }}</h2>
    <p v-if="myEffects.length === 0" class="muted">{{ t('room.player.noEffect') }}</p>
    <ul v-else class="effects">
      <li v-for="id in myEffects" :key="id">
        <details>
          <summary :class="effectOf(id)?.polarity">{{ pick(effectOf(id)?.name, language) || id }}</summary>
          <p>{{ pick(effectOf(id)?.text, language) }}</p>
        </details>
      </li>
    </ul>

    <h2>{{ t('room.player.opponents') }}</h2>
    <p v-if="opponents.length === 0" class="muted">{{ t('room.player.noOpponent') }}</p>
    <ul v-else class="opponents">
      <li v-for="c in opponents" :key="c.instance">
        <img v-if="c.image" :src="assetUrl(c.image)" alt="" loading="lazy" />
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
