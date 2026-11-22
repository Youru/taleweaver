<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import { ChoiceEditor, useErrorText, type ChoiceChange, type PartyPreparation } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerCommand, PlayerView } from '../api/types'

/** The phone before the departure (roadmap §5.3): "Choose your adventurer", within the scenario's rules. */
const props = defineProps<{
  view: PlayerView
  preparation: PartyPreparation
  act?: (command: PlayerCommand) => Promise<void>
}>()
const emit = defineEmits<{ follow: [name: string] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const { textOf } = useErrorText()
const busy = ref(false)
const failure = ref<string | null>(null)
const custom = ref('')
/** One is enough for most: the others show on demand. */
const another = ref(false)

const mine = computed(() => props.view.adventurers)
const full = computed(() => props.preparation.choices.length >= props.preparation.max)
/** An adventurer already in the party can still be taken, while no other seat holds them. */
const offers = computed(() => {
  const members = new Map(props.view.party.map((p) => [p.name, p]))
  const named = props.preparation.adventurers.map((a) => ({ name: a.name, image: a.image }))
  for (const member of props.view.party)
    if (!named.some((n) => n.name === member.name)) named.push({ name: member.name, image: undefined })
  return named
    .filter((a) => !members.get(a.name)?.mine)
    .map((a) => {
      const member = members.get(a.name)
      return { ...a, taken: !!member?.held, open: member ? !member.held : !full.value }
    })
})
const choosing = computed(() => !!props.act && (!mine.value.length || another.value))

async function send(command: PlayerCommand) {
  if (!props.act) return false
  busy.value = true
  failure.value = null
  try {
    await props.act(command)
    return true
  } catch (e) {
    failure.value = textOf(e)
    return false
  } finally {
    busy.value = false
  }
}

async function take(name: string) {
  if (await send({ type: 'chooseAdventurer', adventurer: name })) {
    another.value = false
    custom.value = ''
    emit('follow', name)
  }
}

function change(adventurer: string, value: ChoiceChange) {
  void send({ type: 'chooseAdventurer', adventurer, ...value })
}
</script>

<template>
  <div class="choose">
    <h2>{{ mine.length ? t('preparation.phone.ready') : t('preparation.phone.choose') }}</h2>
    <p class="muted">{{ pick(view.scenarioTitle, language) }}</p>
    <p class="muted">
      {{ t('preparation.count', { n: preparation.choices.length, max: preparation.max }) }}
      <template v-if="preparation.gold">
        · {{ t('preparation.gold', { left: preparation.goldLeft, gold: preparation.gold }) }}
      </template>
    </p>
    <Message v-if="!act" severity="info" :closable="false">{{ t('preparation.phone.gmScreen') }}</Message>
    <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>

    <article v-for="member in mine" :key="member.name" class="mine">
      <div class="head">
        <h3>{{ member.name }}</h3>
        <Button
          v-if="act"
          :label="t('preparation.phone.release')"
          size="small"
          severity="secondary"
          text
          :disabled="busy"
          @click="send({ type: 'releaseAdventurer', adventurer: member.name })"
        />
      </div>
      <ChoiceEditor
        :preparation="preparation"
        :adventurer="member"
        :busy="busy || !act"
        @change="(value) => change(member.name, value)"
      />
    </article>

    <template v-if="mine.length && act">
      <p class="waiting" role="status">
        <i class="pi pi-hourglass" aria-hidden="true" /> {{ t('preparation.phone.waiting') }}
      </p>
      <Button
        v-if="!another && offers.some((a) => a.open)"
        icon="pi pi-user-plus"
        :label="t('preparation.phone.another')"
        severity="secondary"
        outlined
        @click="another = true"
      />
    </template>

    <template v-if="choosing">
      <ul class="offers">
        <li v-for="offer in offers" :key="offer.name">
          <button type="button" class="offer" :disabled="busy || !offer.open" @click="take(offer.name)">
            <img v-if="offer.image" :src="assetUrl(offer.image, 320)" alt="" />
            <strong>{{ offer.name }}</strong>
            <small v-if="offer.taken">{{ t('preparation.phone.taken') }}</small>
          </button>
        </li>
      </ul>
      <form v-if="preparation.custom && !full" class="custom" @submit.prevent="take(custom.trim())">
        <InputText
          v-model="custom"
          maxlength="40"
          :placeholder="t('preparation.customName')"
          :aria-label="t('preparation.customName')"
        />
        <Button
          type="submit"
          :label="t('preparation.customAdd')"
          severity="secondary"
          :disabled="busy || !custom.trim()"
        />
      </form>
      <p v-if="full && !offers.some((a) => a.open)" class="muted">{{ t('preparation.phone.full') }}</p>
    </template>
  </div>
</template>

<style scoped>
.choose {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 0.75rem 1rem 1.5rem;
}
h2,
h3,
p {
  margin: 0;
}
h2 {
  font-size: 1.3rem;
}
h3 {
  font-size: 1.05rem;
}
.muted,
small {
  color: var(--p-text-muted-color);
}
.mine {
  padding: 0.7rem;
  border: 2px solid var(--seat);
  border-radius: 0.6rem;
}
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.4rem;
}
.waiting {
  font-weight: 600;
}
.offers {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.6rem;
  list-style: none;
  margin: 0;
  padding: 0;
}
.offer {
  width: 100%;
  min-height: 3rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.35rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0.5rem;
  background: none;
  color: inherit;
  font: inherit;
}
.offer:disabled {
  opacity: 0.45;
}
.offer img {
  width: 100%;
  border-radius: 0.3rem;
}
.custom {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.custom :deep(.p-button) {
  min-height: 3rem;
}
</style>
