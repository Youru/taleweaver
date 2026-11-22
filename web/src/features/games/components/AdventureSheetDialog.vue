<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import { assetUrl } from '@/shared/api/assets'
import ShowToPlayersButton from '@/shared/components/ShowToPlayersButton.vue'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useHiddenCards } from '../api/queries'
import type { GameState } from '../api/types'
import { activeOptions } from '../lib/options'

/** The Adventure Sheet: the Hidden Cards revealed, the Achievements completed, the options of the game. */
const props = defineProps<{ id: string; state: GameState }>()
const open = defineModel<boolean>('open', { required: true })
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const count = computed(() => props.state.campaign.revealedHiddenCards.length)
const cards = useHiddenCards(() => props.id, count)
const options = computed(() => activeOptions(props.state.options))
/** The Story Flags and values: a flag reads as its name, checked or not. */
const variables = computed(() =>
  Object.entries(props.state.campaign.variables ?? {}).map(([name, value]) => ({
    name,
    value:
      value === true
        ? t('sheet.marked')
        : value === false
          ? t('sheet.unmarked')
          : Array.isArray(value)
            ? value.join(', ')
            : String(value),
  })),
)
</script>

<template>
  <Dialog
    v-model:visible="open"
    modal
    dismissable-mask
    :header="t('sheet.title')"
    :style="{ width: 'min(64rem, 95vw)' }"
  >
    <h3>{{ t('sheet.hiddenCards') }}</h3>
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

    <h3>{{ t('achievements.title') }}</h3>
    <p v-if="!state.campaign.achievements?.length" class="muted">{{ t('achievements.none') }}</p>
    <ul v-else>
      <li v-for="item in state.campaign.achievements" :key="`${item.scenario}/${item.node}/${item.id}`">
        {{ pick(item.name, language) || item.id }} · {{ item.adventurer }}
      </li>
    </ul>

    <h3>{{ t('sheet.flags') }}</h3>
    <p v-if="!variables.length" class="muted">{{ t('sheet.noFlag') }}</p>
    <ul v-else>
      <li v-for="variable in variables" :key="variable.name">
        <strong>{{ variable.name }}</strong> : {{ variable.value }}
      </li>
    </ul>

    <h3>{{ t('options.button') }}</h3>
    <p v-if="!options.length" class="muted">{{ t('options.none') }}</p>
    <ul v-else>
      <li v-for="key in options" :key="key">
        <strong>{{ t(`options.${key}.name`) }}</strong> : {{ t(`options.${key}.text`) }}
      </li>
    </ul>
  </Dialog>
</template>

<style scoped>
h3 {
  margin: 1rem 0 0.4rem;
}
h3:first-child {
  margin-top: 0;
}
.muted {
  color: var(--p-text-muted-color);
}
.cards {
  list-style: none;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
  gap: 1rem;
}
figure {
  margin: 0.4rem 0 0;
}
img {
  max-width: 100%;
  max-height: 22rem;
}
figcaption {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
