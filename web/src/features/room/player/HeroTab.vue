<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import { useDisciplines, useItems, type CatalogAdventurer } from '@/features/catalog'
import { disciplineBlock, disciplineCost, type AdventurerState } from '@/features/games'
import type { PlayerCommand, PlayerRequest } from '../api/types'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { xpLeft } from '../lib/room'

/**
 * The Adventurer Card in large, its stats, the XP left and the disciplines learnt (tap to read the card). With
 * `act`, the player may ask to learn a discipline (room-spec §6.2).
 */
const props = defineProps<{
  adventurer: AdventurerState
  card: CatalogAdventurer | undefined
  requests?: PlayerRequest[]
  act?: (command: PlayerCommand) => Promise<void>
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const disciplines = useDisciplines()
const items = useItems()

/**
 * Movement and Agility with what is equipped, face up: the cards' Movement bonus, and HEAVY's -1 to both per card
 * that has it (rulebook p. 93).
 */
const equippedSides = computed(() =>
  (props.adventurer.equipped ?? []).flatMap((owned) => {
    const item = items.data.value?.find((i) => i.id === owned.item)
    if (!item) return []
    const back = owned.flipped && item.backSide
    return [
      {
        tags: back && item.backSide!.tags.length ? item.backSide!.tags : item.tags,
        stats: back ? item.backSide!.stats : item.stats,
      },
    ]
  }),
)
const heavy = computed(() => equippedSides.value.filter((s) => s.tags.includes('HEAVY')).length)
// Tatva's +1 too, unless Flipped
const disciplineMovement = computed(() =>
  (props.adventurer.disciplines ?? [])
    .filter((d) => !props.adventurer.flippedCards?.includes(d.id))
    .reduce((sum, d) => sum + (disciplines.data.value?.find((c) => c.id === d.id)?.movement ?? 0), 0),
)
const movement = computed(() =>
  props.card
    ? props.card.movement +
      equippedSides.value.reduce((sum, s) => sum + (s.stats?.movement ?? 0), 0) +
      disciplineMovement.value -
      heavy.value
    : undefined,
)
const skillValue = (name: string) => (props.card?.skills[name] ?? 0) - (name === 'agility' ? heavy.value : 0)

const learnt = computed(() =>
  (props.adventurer.disciplines ?? []).map((d) => ({
    ...d,
    card: disciplines.data.value?.find((c) => c.id === d.id),
  })),
)
const open = ref<string>()

// Learning: the cards the adventurer may take now (lower levels of the tree known), with their cost
const choosing = ref(false)
const busy = ref(false)
const failure = ref<string>()
const left = computed(() => xpLeft(props.adventurer))
const learnable = computed(() => {
  const catalog = disciplines.data.value ?? []
  return (
    catalog
      // A Familiar is chosen once the Familiar Discipline is learnt (p. 76)
      .filter((c) => (!c.story || c.tree === 'familiar') && disciplineBlock(c, props.adventurer, catalog) === null)
      .map((c) => ({ card: c, cost: disciplineCost(c, props.adventurer, catalog) }))
      .filter((d) => d.cost !== null && d.cost <= left.value)
      .sort((a, b) => (a.card.level ?? 0) - (b.card.level ?? 0) || a.card.tree.localeCompare(b.card.tree))
  )
})
const asked = computed(
  () =>
    new Set(
      (props.requests ?? [])
        .filter((r) => r.status === 'pending' && r.command.type === 'learnDiscipline')
        .map((r) => (r.command.type === 'learnDiscipline' ? r.command.discipline : '')),
    ),
)
async function learn(discipline: string) {
  if (!props.act) return
  busy.value = true
  failure.value = undefined
  try {
    await props.act({ type: 'learnDiscipline', adventurer: props.adventurer.name, discipline })
    choosing.value = false
  } catch (e) {
    failure.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
const skills = ['presence', 'lore', 'agility', 'perception', 'strength']
</script>

<template>
  <div class="hero">
    <img v-if="card?.image" class="card" :src="assetUrl(card.image, 1280)" :alt="adventurer.name" />
    <h2>{{ pick(card?.fullName, language) || adventurer.name }}</h2>
    <div class="tags">
      <Tag v-if="adventurer.defeated" severity="danger" :value="t('play.adventurerDefeatedTag')" />
      <Tag v-if="adventurer.injured" severity="warn" :value="t('storyRound.injured')" />
      <Tag v-if="adventurer.unselectable" severity="secondary" :value="t('storyRound.unselectable')" />
    </div>
    <dl class="stats">
      <div>
        <dt>{{ t('room.player.health') }}</dt>
        <dd>{{ (adventurer.health ?? 0) - (adventurer.damage ?? 0) }}/{{ adventurer.health ?? '?' }}</dd>
      </div>
      <div>
        <dt>{{ t('room.player.stamina') }}</dt>
        <dd>{{ adventurer.stamina ?? 0 }}/{{ adventurer.staminaMax ?? '?' }}</dd>
      </div>
      <div v-if="card">
        <dt>{{ t('room.player.defense') }}</dt>
        <dd>{{ card.defense }}</dd>
      </div>
      <div v-if="card">
        <dt>{{ t('room.player.movement') }}</dt>
        <dd :title="heavy ? t('inventory.warn.heavy', { count: heavy }) : undefined">{{ movement }}</dd>
      </div>
      <div>
        <dt>{{ t('room.player.xpLeft') }}</dt>
        <dd>{{ xpLeft(adventurer) }}</dd>
      </div>
    </dl>
    <dl v-if="card && Object.keys(card.skills).length" class="stats skills">
      <div v-for="skill in skills" :key="skill">
        <dt>{{ t(`room.player.skills.${skill}`) }}</dt>
        <dd>{{ skillValue(skill) }}</dd>
      </div>
    </dl>
    <p v-if="card?.ability" class="ability">
      <strong>{{ t('room.player.ability') }}</strong> {{ pick(card.ability.name, language) }}
    </p>
    <section>
      <h3>{{ t('room.player.disciplines') }}</h3>
      <p v-if="learnt.length === 0" class="muted">{{ t('room.player.noDiscipline') }}</p>
      <ul class="disciplines">
        <li v-for="d in learnt" :key="d.id">
          <button type="button" :aria-expanded="open === d.id" @click="open = open === d.id ? undefined : d.id">
            <span>{{ pick(d.card?.name, language) || d.id }}</span>
            <small>{{ t('room.player.xpCost', { xp: d.xp }) }}</small>
          </button>
          <img
            v-if="open === d.id && d.card?.image"
            :src="assetUrl(d.card.image, 640)"
            :alt="pick(d.card.name, language)"
          />
        </li>
      </ul>
      <Button
        v-if="act && learnable.length"
        icon="pi pi-plus"
        :label="t('room.request.learn.open')"
        severity="secondary"
        class="learn"
        @click="choosing = true"
      />
    </section>
    <Dialog v-model:visible="choosing" modal dismissable-mask :header="t('room.request.learn.title', { xp: left })">
      <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
      <ul class="learnable">
        <li v-for="d in learnable" :key="d.card.id">
          <img
            v-if="d.card.image"
            :src="assetUrl(d.card.image, 320)"
            :alt="pick(d.card.name, language)"
            loading="lazy"
          />
          <div>
            <strong>{{ pick(d.card.name, language) }}</strong>
            <small>{{ t('room.request.learn.cost', { xp: d.cost, level: d.card.level ?? '?' }) }}</small>
            <Button
              :label="asked.has(d.card.id) ? t('room.request.waiting') : t('room.request.learn.ask')"
              size="small"
              :disabled="busy || asked.has(d.card.id)"
              @click="learn(d.card.id)"
            />
          </div>
        </li>
      </ul>
    </Dialog>
  </div>
</template>

<style scoped>
.learn {
  margin-top: 0.5rem;
  min-height: 3rem;
}
.learnable {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.learnable li {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
}
.learnable img {
  width: 6rem;
  border-radius: 0.4rem;
}
.learnable div {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  align-items: flex-start;
}
.hero {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.card {
  width: 100%;
  border-radius: 0.75rem;
  box-shadow: 0 4px 16px rgb(0 0 0 / 0.25);
}
h2 {
  margin: 0;
  font-size: 1.25rem;
}
.tags {
  display: flex;
  gap: 0.25rem;
  flex-wrap: wrap;
}
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(5.5rem, 1fr));
  gap: 0.5rem;
  margin: 0;
}
.stats div {
  background: var(--p-content-hover-background);
  border-radius: 0.5rem;
  padding: 0.5rem;
  text-align: center;
}
.stats dt {
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
}
.stats dd {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
}
h3 {
  font-size: 1rem;
  margin: 0.5rem 0;
}
.disciplines {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.disciplines button {
  width: 100%;
  min-height: 3rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0.5rem;
  background: var(--p-content-background);
  color: inherit;
  font: inherit;
}
.disciplines img {
  width: 100%;
  margin-top: 0.5rem;
  border-radius: 0.5rem;
}
.muted {
  color: var(--p-text-muted-color);
}
</style>
