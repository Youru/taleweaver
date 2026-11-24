<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import { useItemUpgrades } from '@/features/catalog'
import type { ScenarioVariable } from '@/features/scenarios'
import { assetUrl } from '@/shared/api/assets'
import ShowToPlayersButton from '@/shared/components/ShowToPlayersButton.vue'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useHiddenCards } from '../api/queries'
import type { GameState } from '../api/types'
import { activeOptions } from '../lib/options'

/**
 * The Adventure Sheet (rulebook p. 28): the Story Flags, the Achievements, the Item Upgrades unlocked, the Hidden Cards
 * revealed, then what the game tracks for the scenario and the options.
 */
const props = defineProps<{ id: string; state: GameState; variables?: ScenarioVariable[] }>()
const open = defineModel<boolean>('open', { required: true })
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const count = computed(() => props.state.campaign.revealedHiddenCards.length)
const cards = useHiddenCards(() => props.id, count)
const upgradeCatalog = useItemUpgrades()
const options = computed(() => activeOptions(props.state.options))
const values = computed(() => props.state.campaign.variables ?? {})

/** The Story Flags of the book, marked or not: the whole list, as printed on the sheet. */
const storyFlags = computed(() =>
  (props.variables ?? [])
    .filter((v) => v.storyFlag)
    .map((v) => ({ id: v.id, name: pick(v.name, language.value) || v.id, marked: values.value[v.id] === true })),
)

/** Every other value: named ones first, then the scenario's inner bookkeeping under its id. */
const tracking = computed(() => {
  const declared = new Map((props.variables ?? []).map((v) => [v.id, v]))
  return Object.entries(values.value)
    .filter(([id]) => !declared.get(id)?.storyFlag)
    .map(([id, value]) => {
      const name = pick(declared.get(id)?.name, language.value)
      return { id, name: name || id, named: !!name, value: shown(value), flag: typeof value === 'boolean', on: value === true }
    })
    .sort((a, b) => Number(b.named) - Number(a.named) || a.name.localeCompare(b.name))
})

function shown(value: unknown): string {
  if (value === true) return t('sheet.marked')
  if (value === false) return t('sheet.unmarked')
  return Array.isArray(value) ? value.join(', ') : String(value)
}

const upgrades = computed(() => {
  const byId = new Map((upgradeCatalog.data.value ?? []).map((u) => [u.id, u]))
  return (props.state.campaign.unlockedUpgrades ?? []).map((id) => ({ id, card: byId.get(id) }))
})

const achievements = computed(() => props.state.campaign.achievements ?? [])
</script>

<template>
  <Dialog
    v-model:visible="open"
    modal
    dismissable-mask
    :header="t('sheet.title')"
    :style="{ width: 'min(64rem, 95vw)' }"
  >
    <section>
      <h3><i class="pi pi-flag" /> {{ t('sheet.storyFlags') }}</h3>
      <p v-if="!storyFlags.length" class="muted">{{ t('sheet.noStoryFlag') }}</p>
      <ul v-else class="flags">
        <li v-for="flag in storyFlags" :key="flag.id" :class="{ marked: flag.marked }">
          <i :class="flag.marked ? 'pi pi-check-square' : 'pi pi-stop'" />
          <span>{{ flag.name }}</span>
          <span class="sr-only">{{ flag.marked ? t('sheet.marked') : t('sheet.unmarked') }}</span>
        </li>
      </ul>
    </section>

    <section>
      <h3><i class="pi pi-trophy" /> {{ t('achievements.title') }}</h3>
      <p v-if="!achievements.length" class="muted">{{ t('achievements.none') }}</p>
      <ul v-else class="tiles">
        <li v-for="item in achievements" :key="`${item.scenario}/${item.node}/${item.id}`" class="tile achievement">
          <i class="pi pi-trophy" />
          <div>
            <strong>{{ pick(item.name, language) || item.id }}</strong>
            <small>{{ t('achievements.done', { name: item.adventurer }) }}</small>
          </div>
        </li>
      </ul>
    </section>

    <section>
      <h3><i class="pi pi-wrench" /> {{ t('sheet.upgrades') }}</h3>
      <p v-if="!upgrades.length" class="muted">{{ t('sheet.noUpgrade') }}</p>
      <ul v-else class="tiles">
        <li v-for="upgrade in upgrades" :key="upgrade.id" class="tile upgrade">
          <img v-if="upgrade.card?.image" :src="assetUrl(upgrade.card.image, 320)" alt="" loading="lazy" />
          <i v-else class="pi pi-wrench" />
          <div>
            <strong>{{ pick(upgrade.card?.name, language) || upgrade.id }}</strong>
            <small v-if="upgrade.card?.tags.length">{{ upgrade.card.tags.join(' · ') }}</small>
            <small v-if="upgrade.card?.cost != null">{{ t('sheet.upgradeCost', { cost: upgrade.card.cost }) }}</small>
          </div>
        </li>
      </ul>
    </section>

    <section>
      <h3><i class="pi pi-eye" /> {{ t('sheet.hiddenCards') }}</h3>
      <p v-if="!count" class="muted">{{ t('sheet.noHiddenCard') }}</p>
      <Message v-else-if="cards.isError.value" severity="error">{{ cards.error.value?.message }}</Message>
      <ul v-else class="cards">
        <li v-for="card in cards.data.value ?? []" :key="`${card.size}:${card.code}`">
          <strong>{{ t('sheet.card', { size: t(`sheet.size.${card.size}`), code: card.code }) }}</strong>
          <p v-if="!card.pictures.length" class="muted">{{ t('sheet.fromTheBox') }}</p>
          <figure v-for="picture in card.pictures" :key="picture.image">
            <a :href="assetUrl(picture.image)" target="_blank" rel="noopener">
              <img :src="assetUrl(picture.image, 640)" :alt="pick(picture.name, language)" loading="lazy" />
            </a>
            <figcaption>
              <span>{{ pick(picture.name, language) }}</span>
              <ShowToPlayersButton :image="picture.image" :caption="pick(picture.name, language)" />
            </figcaption>
          </figure>
        </li>
      </ul>
    </section>

    <Panel :header="t('sheet.tracking')" toggleable collapsed class="tracking">
      <p v-if="!tracking.length" class="muted">{{ t('sheet.noFlag') }}</p>
      <ul v-else class="values">
        <li v-for="item in tracking" :key="item.id" :class="{ inner: !item.named }">
          <span>{{ item.name }}</span>
          <i v-if="item.flag" :class="item.on ? 'pi pi-check' : 'pi pi-minus'" :aria-label="item.value" />
          <strong v-else>{{ item.value }}</strong>
        </li>
      </ul>
    </Panel>

    <section>
      <h3><i class="pi pi-sliders-h" /> {{ t('options.button') }}</h3>
      <p v-if="!options.length" class="muted">{{ t('options.none') }}</p>
      <ul v-else>
        <li v-for="key in options" :key="key">
          <strong>{{ t(`options.${key}.name`) }}</strong> : {{ t(`options.${key}.text`) }}
        </li>
      </ul>
    </section>
  </Dialog>
</template>

<style scoped>
section + section,
.tracking,
.tracking + section {
  margin-top: 1.2rem;
}
h3 {
  margin: 0 0 0.5rem;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}
.muted {
  color: var(--p-text-muted-color);
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}
.flags {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.flags li {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.35rem 0.7rem;
  border: 1px dashed var(--p-content-border-color);
  border-radius: 999px;
  color: var(--p-text-muted-color);
}
.flags li.marked {
  border-style: solid;
  border-color: var(--p-primary-color);
  background: color-mix(in srgb, var(--p-primary-color) 14%, transparent);
  color: var(--p-text-color);
  font-weight: 600;
}
.flags li.marked i {
  color: var(--p-primary-color);
}
.tiles {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
  gap: 0.6rem;
}
.tile {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  padding: 0.6rem 0.8rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0.6rem;
  background: var(--p-content-hover-background);
}
.tile > i {
  font-size: 1.5rem;
}
.tile div {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.tile small {
  color: var(--p-text-muted-color);
}
.achievement > i {
  color: #d4a72c;
}
.upgrade img {
  width: 3.5rem;
  height: 3.5rem;
  object-fit: cover;
  border-radius: 0.4rem;
}
.cards {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
  gap: 1rem;
}
figure {
  margin: 0.4rem 0 0;
}
.cards img {
  max-width: 100%;
  max-height: 22rem;
}
figcaption {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.values {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
  gap: 0.2rem 1.2rem;
}
.values li {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.15rem 0;
  border-bottom: 1px dotted var(--p-content-border-color);
}
.values li.inner span {
  font-family: monospace;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
</style>
