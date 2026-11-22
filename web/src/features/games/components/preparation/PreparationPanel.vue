<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import { assetUrl } from '@/shared/api/assets'
import type { PartyPreparation, PreparationCommand } from '../../api/preparation'
import type { AdventurerState } from '../../api/types'
import type { HeldBy } from '../../lib/initiativeTrack'
import { notChosen } from '../../lib/preparation'
import { useErrorText } from '../../lib/useErrorText'
import ChoiceEditor from './ChoiceEditor.vue'

/** The departure, on the GM's screen (roadmap §5.2): who comes, with what, and the go. */
const props = defineProps<{
  preparation: PartyPreparation
  party: AdventurerState[]
  heldBy: Record<string, HeldBy>
  roomOpen: boolean
  busy: boolean
}>()
const emit = defineEmits<{ command: [command: PreparationCommand]; room: [] }>()

const { t } = useI18n()
const { ofCode } = useErrorText()
const custom = ref('')
const full = computed(() => props.preparation.choices.length >= props.preparation.max)
const offered = computed(() => notChosen(props.preparation))
const offerOf = (name: string) => props.preparation.adventurers.find((a) => a.name === name)
const missing = computed(() => ofCode(props.preparation.missing) ?? props.preparation.missing)

function addCustom() {
  const name = custom.value.trim()
  if (!name) return
  emit('command', { type: 'choose', adventurer: name })
  custom.value = ''
}
</script>

<template>
  <section class="preparation" aria-labelledby="preparation-title">
    <header>
      <div>
        <h2 id="preparation-title">{{ t('preparation.title') }}</h2>
        <p class="rules">
          <span>{{ t('preparation.count', { n: preparation.choices.length, max: preparation.max }) }}</span>
          <span v-if="preparation.min > 1">{{ t('preparation.min', { n: preparation.min }) }}</span>
          <span v-if="preparation.gold">{{
            t('preparation.gold', { left: preparation.goldLeft, gold: preparation.gold })
          }}</span>
          <span v-if="preparation.xp">{{ t('preparation.xp', { xp: preparation.xp }) }}</span>
        </p>
      </div>
      <div class="go">
        <Button
          icon="pi pi-mobile"
          :label="roomOpen ? t('preparation.showRoom') : t('preparation.openRoom')"
          severity="secondary"
          outlined
          @click="emit('room')"
        />
        <Button
          icon="pi pi-flag"
          :label="t('preparation.setOut')"
          :disabled="busy || !!preparation.missing"
          @click="emit('command', { type: 'setOut' })"
        />
      </div>
    </header>
    <Message v-if="missing" severity="info" size="small">{{ missing }}</Message>

    <ul v-if="party.length" class="party" :aria-label="t('preparation.party')">
      <li v-for="member in party" :key="member.name" class="member">
        <div class="head">
          <img v-if="offerOf(member.name)?.image" :src="assetUrl(offerOf(member.name)?.image, 160)" alt="" />
          <h3>{{ member.name }}</h3>
          <span v-if="heldBy[member.name]" class="held-by" :style="{ '--seat': heldBy[member.name]!.color }">
            {{ heldBy[member.name]!.name }}
          </span>
          <Tag v-if="offerOf(member.name)?.required" :value="t('preparation.required')" severity="secondary" />
          <Button
            v-else
            icon="pi pi-times"
            text
            rounded
            severity="secondary"
            :aria-label="t('preparation.remove', { name: member.name })"
            :disabled="busy"
            @click="emit('command', { type: 'remove', adventurer: member.name })"
          />
        </div>
        <ChoiceEditor
          :preparation="preparation"
          :adventurer="member"
          :busy="busy"
          @change="(change) => emit('command', { type: 'choose', adventurer: member.name, ...change })"
        />
      </li>
    </ul>

    <template v-if="!full">
      <h3 v-if="offered.length">{{ t('preparation.add') }}</h3>
      <ul v-if="offered.length" class="offers">
        <li v-for="offer in offered" :key="offer.id">
          <button
            type="button"
            class="offer"
            :disabled="busy"
            :aria-label="t('preparation.addOne', { name: offer.name })"
            @click="emit('command', { type: 'choose', adventurer: offer.name })"
          >
            <img v-if="offer.image" :src="assetUrl(offer.image, 320)" alt="" loading="lazy" />
            <span>{{ offer.name }}</span>
          </button>
        </li>
      </ul>
      <form v-if="preparation.custom" class="custom" @submit.prevent="addCustom">
        <InputText
          v-model="custom"
          maxlength="40"
          :placeholder="t('preparation.customName')"
          :aria-label="t('preparation.customName')"
        />
        <Button
          type="submit"
          icon="pi pi-plus"
          :label="t('preparation.customAdd')"
          severity="secondary"
          :disabled="busy || !custom.trim()"
        />
        <small>{{ t('preparation.customHint') }}</small>
      </form>
    </template>

    <label class="spoilers">
      <ToggleSwitch
        :model-value="preparation.hiddenShown"
        :disabled="busy"
        @update:model-value="(shown: boolean) => emit('command', { type: 'hidden', shown })"
      />
      <span>
        {{ t('preparation.showHidden') }}
        <small>{{ t('preparation.showHiddenHint') }}</small>
      </span>
    </label>
  </section>
</template>

<style scoped>
.preparation {
  width: min(78rem, 100%);
  margin: 0 auto;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow-y: auto;
  min-height: 0;
}
header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 0.75rem;
}
h2,
h3 {
  margin: 0;
}
h3 {
  font-size: 1rem;
}
.rules {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1rem;
  margin: 0.3rem 0 0;
  color: var(--p-text-muted-color);
}
.go,
.custom {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
}
.party {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(17rem, 100%), 1fr));
  gap: 0.75rem;
}
.member {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  min-width: 0;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
}
.head {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.head img {
  width: 3rem;
  border-radius: 0.3rem;
}
.head h3 {
  flex: 1;
}
.held-by {
  padding: 0.1rem 0.5rem;
  border: 2px solid var(--seat);
  border-radius: 999px;
  font-size: 0.8rem;
}
.offers {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(9rem, 100%), 1fr));
  gap: 0.6rem;
}
.offer {
  width: 100%;
  padding: 0.3rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
  background: none;
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.offer:hover {
  border-color: var(--p-primary-color);
}
.offer img {
  width: 100%;
  border-radius: 0.3rem;
}
small {
  color: var(--p-text-muted-color);
}
.spoilers {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  align-self: flex-start;
}
.spoilers small {
  display: block;
}
</style>
