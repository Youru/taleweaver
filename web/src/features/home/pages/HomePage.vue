<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import { useConfirm } from 'primevue/useconfirm'
import { ContentPacksSection } from '@/features/contentPacks'
import { StartGameDialog, useDeleteGame, useGames, type GameView } from '@/features/games'
import { useScenarios } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'

const { t, d } = useI18n()
const router = useRouter()
const confirm = useConfirm()
const { language } = storeToRefs(usePreferences())
const scenarios = useScenarios()
const games = useGames()
const deleteGame = useDeleteGame()
const starting = ref<{ id: string; title: string } | null>(null)

function titleOf(id: string): string {
  const scenario = scenarios.data.value?.find((s) => s.id === id)
  return scenario ? pick(scenario.title, language.value) : id
}

function started(game: GameView) {
  starting.value = null
  void router.push({ name: 'play', params: { id: game.id } })
}

function remove(id: string) {
  confirm.require({
    message: t('home.deleteConfirm'),
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    rejectProps: { label: t('common.cancel'), severity: 'secondary', text: true },
    accept: () => deleteGame.mutate(id),
  })
}
</script>

<template>
  <main class="home">
    <!-- Rulebook art (assets/rulebook/art/scenes): the user's own copy, served by the local API -->
    <section class="hero">
      <div class="hero-text">
        <h1>{{ t('app.title') }}</h1>
        <p>{{ t('home.tagline') }}</p>
      </div>
    </section>
    <section :aria-labelledby="'scenarios-title'">
      <h2 id="scenarios-title">{{ t('home.scenarios') }}</h2>
      <ProgressSpinner v-if="scenarios.isPending.value" :aria-label="t('common.loading')" />
      <Message v-else-if="scenarios.isError.value" severity="error">
        {{ t('common.error', { message: scenarios.error.value?.message ?? '' }) }}
      </Message>
      <p v-else-if="!scenarios.data.value?.length">{{ t('home.noScenario') }}</p>
      <div v-else class="cards">
        <Card v-for="scenario in scenarios.data.value" :key="scenario.id">
          <template #title>{{ pick(scenario.title, language) }}</template>
          <template #subtitle>{{ scenario.kind }} · {{ scenario.packs.join(', ') }}</template>
          <template #footer>
            <Button
              :label="t('home.play')"
              :aria-label="t('home.playScenario', { title: pick(scenario.title, language) })"
              icon="pi pi-play"
              @click="starting = { id: scenario.id, title: pick(scenario.title, language) }"
            />
          </template>
        </Card>
      </div>
    </section>

    <section :aria-labelledby="'games-title'">
      <h2 id="games-title">{{ t('home.games') }}</h2>
      <ProgressSpinner v-if="games.isPending.value" :aria-label="t('common.loading')" />
      <Message v-else-if="games.isError.value" severity="error">
        {{ t('common.error', { message: games.error.value?.message ?? '' }) }}
      </Message>
      <p v-else-if="!games.data.value?.length">{{ t('home.noGame') }}</p>
      <ul v-else class="games">
        <li v-for="game in games.data.value" :key="game.id">
          <div>
            <strong>{{ titleOf(game.scenario) }}</strong>
            <span>{{ game.party.join(', ') }}</span>
            <small>{{ t('home.updated') }} {{ d(new Date(game.updatedAt), 'short') }} · {{ game.currentNode }}</small>
          </div>
          <Tag
            :value="game.status === 'Finished' ? t('home.finished') : game.preparing ? t('home.preparing') : t('home.inProgress')"
            :severity="game.status === 'Finished' ? 'success' : 'info'"
          />
          <Button
            :label="t('home.resume')"
            size="small"
            @click="router.push({ name: 'play', params: { id: game.id } })"
          />
          <Button
            icon="pi pi-trash"
            size="small"
            severity="danger"
            text
            :aria-label="t('common.delete')"
            @click="remove(game.id)"
          />
        </li>
      </ul>
    </section>

    <ContentPacksSection />

    <StartGameDialog :scenario="starting" @close="starting = null" @started="started" />
  </main>
</template>

<style scoped>
.hero {
  display: flex;
  align-items: flex-end;
  min-height: 14rem;
  margin: -0.75rem -1rem 0;
  padding: 1.5rem 2rem;
  background:
    linear-gradient(to top, rgb(36 7 7 / 0.85), rgb(36 7 7 / 0) 65%),
    #3a0d0d url('/api/assets/rulebook/art/scenes/045-1699x979-c3-5e14ec70.jpg') center 40% / cover;
  border-bottom: 2px solid #3a0d0d;
  box-shadow: 0 4px 12px rgb(0 0 0 / 0.35);
}
.hero h1 {
  margin: 0;
  font-size: 2.6rem;
  color: var(--mid-cream);
  text-shadow: 0 2px 6px rgb(0 0 0 / 0.6);
}
.hero p {
  margin: 0.25rem 0 0;
  color: rgb(243 233 216 / 0.9);
  font-family: var(--mid-book-font);
  font-style: italic;
  font-size: 1.1rem;
}
.home {
  padding: 0 1rem 1rem;
  display: grid;
  gap: 1.5rem;
  max-width: 1100px;
  margin: 0 auto;
}
.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
  gap: 1rem;
}
.games {
  list-style: none;
  padding: 0;
  margin: 0;
}
.games li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.6rem 0;
  border-bottom: 1px solid var(--p-content-border-color);
}
.games li div {
  display: flex;
  flex-direction: column;
  flex: 1;
}
.games small {
  color: var(--p-text-muted-color);
}
</style>
