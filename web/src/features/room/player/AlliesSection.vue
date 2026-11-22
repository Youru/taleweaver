<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import { useErrorText } from '@/features/games'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { PlayerAllyOption, PlayerCommand, PlayerView } from '../api/types'

/** The player's allies (rulebook p. 58, 75-77): their card to read, and what is asked of the GM about them. */
const props = defineProps<{
  view: PlayerView
  followed: string
  act?: (command: PlayerCommand) => Promise<void>
  /** The amount on the Turn tab's counter. */
  amount: number
}>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const { textOf } = useErrorText()

const mine = computed(() => new Set(props.view.adventurers.map((a) => a.name)))
const allies = computed(() => (props.view.encounter?.allies ?? []).filter((a) => !a.defeated))
const playable = (owner: string | null | undefined) => (owner ? mine.value.has(owner) : mine.value.size > 0)
const member = computed(() => props.view.adventurers.find((a) => a.name === props.followed))
const form = computed(() => member.value?.conduitForm)
const options = computed(() =>
  (props.view.encounter?.allyOptions ?? [])
    .filter((o) => o.adventurer === props.followed)
    .map((o) => (o.kind === 'conduit' ? { ...o, cards: o.cards.filter((c) => c.id !== form.value) } : o))
    .filter((o) => o.cards.length),
)
const formCard = computed(() =>
  (props.view.encounter?.allyOptions ?? [])
    .find((o) => o.adventurer === props.followed && o.kind === 'conduit')
    ?.cards.find((c) => c.id === form.value),
)

const choosing = ref<PlayerAllyOption | null>(null)
const reading = ref<{ image: string; name: string } | null>(null)
const failure = ref<string | null>(null)
const sent = ref(false)
async function ask(command: PlayerCommand) {
  if (!props.act) return
  failure.value = null
  sent.value = false
  try {
    await props.act(command)
    choosing.value = null
    sent.value = true
  } catch (e) {
    failure.value = textOf(e)
  }
}
function choose(option: PlayerAllyOption, card: string) {
  const { adventurer } = option
  if (option.kind === 'earnConduit') void ask({ type: 'earnConduit', adventurer, card })
  else if (option.kind === 'conduit') void ask({ type: 'conduitForm', adventurer, card })
  else void ask({ type: 'summon', adventurer, card })
}
</script>

<template>
  <section v-if="allies.length || options.length || form" class="allies">
    <h2>{{ t('allies.title') }}</h2>
    <article v-for="ally in allies" :key="ally.id">
      <button
        type="button"
        class="row"
        @click="ally.image && (reading = { image: ally.image, name: pick(ally.name, language) })"
      >
        <img v-if="ally.image" :src="assetUrl(ally.image, 160)" alt="" />
        <span class="who">
          <strong>{{ pick(ally.name, language) }}</strong>
          <small v-if="ally.owner">{{ t('allies.of', { name: ally.owner }) }}</small>
        </span>
        <span class="numbers">
          {{ ally.health == null ? `−${ally.damage}` : `${ally.health - ally.damage}/${ally.health}` }}
          <small>{{ t('play.stamina', { sp: ally.stamina, max: ally.staminaMax }) }}</small>
          <small v-if="ally.force">{{ t('allies.force', { n: ally.force }) }}</small>
        </span>
      </button>
      <div v-if="act && playable(ally.owner)" class="actions">
        <Button
          :label="t('allies.phone.damage', { n: amount })"
          severity="danger"
          outlined
          @click="ask({ type: 'allyDamage', ally: `ally:${ally.id}`, amount })"
        />
        <Button
          v-if="ally.esper && ally.owner"
          :label="t('allies.dismiss')"
          severity="secondary"
          text
          @click="ask({ type: 'dismissAlly', ally: `ally:${ally.id}` })"
        />
      </div>
    </article>

    <div v-if="form" class="actions">
      <span>{{ t('allies.inForm', { name: followed, card: pick(formCard?.name, language) }) }}</span>
      <Button
        v-if="formCard?.image"
        :label="t('allies.phone.readCard')"
        severity="secondary"
        text
        @click="reading = { image: formCard!.image!, name: pick(formCard!.name, language) }"
      />
      <Button
        v-if="act"
        :label="t('allies.leaveForm')"
        severity="secondary"
        outlined
        @click="ask({ type: 'conduitForm', adventurer: followed })"
      />
    </div>
    <template v-if="act">
      <Button
        v-for="option in options"
        :key="option.kind"
        :label="t(`allies.option.${option.kind}`, { name: option.adventurer })"
        severity="secondary"
        class="option"
        @click="choosing = option"
      />
      <Message v-if="failure" severity="error" :closable="false">{{ failure }}</Message>
      <Message v-else-if="sent" severity="info" :life="2500" :closable="false">{{ t('room.player.reported') }}</Message>
    </template>

    <Dialog
      :visible="!!choosing"
      modal
      dismissable-mask
      :header="choosing ? t(`allies.option.${choosing.kind}`, { name: choosing.adventurer }) : ''"
      :style="{ width: 'min(28rem, 96vw)' }"
      @update:visible="(v: boolean) => !v && (choosing = null)"
    >
      <ul v-if="choosing" class="cards">
        <li v-for="card in choosing.cards" :key="card.id">
          <img v-if="card.image" :src="assetUrl(card.image, 640)" :alt="pick(card.name, language)" loading="lazy" />
          <Button
            :label="t('allies.phone.ask', { card: pick(card.name, language) })"
            @click="choose(choosing, card.id)"
          />
        </li>
      </ul>
    </Dialog>
    <Dialog
      :visible="!!reading"
      modal
      dismissable-mask
      :header="reading?.name"
      :style="{ width: 'min(28rem, 96vw)' }"
      @update:visible="(v: boolean) => !v && (reading = null)"
    >
      <img v-if="reading" class="full" :src="assetUrl(reading.image, 1280)" :alt="reading.name" />
    </Dialog>
  </section>
</template>

<style scoped>
.allies {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
h2 {
  margin: 0.6rem 0 0;
  font-size: 0.85rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--p-text-muted-color);
}
.row {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.5rem;
  border: 1px solid var(--p-blue-400);
  border-radius: 0.5rem;
  background: var(--p-content-background);
  color: inherit;
  font: inherit;
  text-align: left;
}
.row img {
  width: 2.5rem;
  height: 2.5rem;
  object-fit: cover;
  border-radius: 0.4rem;
}
.who {
  flex: 1;
}
small {
  display: block;
  color: var(--p-text-muted-color);
}
.numbers {
  text-align: right;
}
.actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
  margin-top: 0.3rem;
}
.actions :deep(.p-button),
.option {
  min-height: 3rem;
}
.cards {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  list-style: none;
  margin: 0;
  padding: 0;
}
.cards li {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.cards img,
.full {
  width: 100%;
  border-radius: 0.5rem;
}
</style>
