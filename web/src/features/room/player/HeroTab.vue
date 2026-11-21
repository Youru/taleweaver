<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Tag from 'primevue/tag'
import { useDisciplines, type CatalogAdventurer } from '@/features/catalog'
import type { AdventurerState } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { xpLeft } from '../lib/room'

/** The Adventurer Card in large, its stats, the XP left and the disciplines learnt (tap to read the card). */
const props = defineProps<{ adventurer: AdventurerState; card: CatalogAdventurer | undefined }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const disciplines = useDisciplines()

const learnt = computed(() =>
  (props.adventurer.disciplines ?? []).map((d) => ({
    ...d,
    card: disciplines.data.value?.find((c) => c.id === d.id),
  })),
)
const open = ref<string>()
const skills = ['presence', 'lore', 'agility', 'perception', 'strength']
</script>

<template>
  <div class="hero">
    <img v-if="card?.image" class="card" :src="assetUrl(card.image)" :alt="adventurer.name" />
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
        <dd>{{ card.movement }}</dd>
      </div>
      <div>
        <dt>{{ t('room.player.xpLeft') }}</dt>
        <dd>{{ xpLeft(adventurer) }}</dd>
      </div>
    </dl>
    <dl v-if="card && Object.keys(card.skills).length" class="stats skills">
      <div v-for="skill in skills" :key="skill">
        <dt>{{ t(`room.player.skills.${skill}`) }}</dt>
        <dd>{{ card.skills[skill] ?? 0 }}</dd>
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
          <img v-if="open === d.id && d.card?.image" :src="assetUrl(d.card.image)" :alt="pick(d.card.name, language)" />
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
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
