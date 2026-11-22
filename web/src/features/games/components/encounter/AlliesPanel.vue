<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import { useDisciplines, type Combatant } from '@/features/catalog'
import { assetUrl } from '@/shared/api/assets'
import ShowToPlayersButton from '@/shared/components/ShowToPlayersButton.vue'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { AllyOption } from '../../api/allies'
import { useAllyCards } from '../../api/queries'
import type { AdventurerState, EncounterCommand, EncounterState } from '../../api/types'

/** The allies in play, and what the Assemblage Disciplines may bring now (rulebook p. 58, 75-77). */
const props = defineProps<{
  encounter: EncounterState
  party: AdventurerState[]
  cards: Map<string, Combatant>
  selected: string | undefined
  locked: boolean
}>()
const emit = defineEmits<{ command: [command: EncounterCommand]; select: [figure: string] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const allies = computed(() => (props.encounter.allies ?? []).filter((a) => !a.defeated))
const transformed = computed(() => props.party.filter((a) => a.conduitForm))
const disciplines = useDisciplines()
const exalted = (id: string) => disciplines.data.value?.find((d) => d.id === id && d.tree === 'exalted')
const cardName = (id: string) =>
  pick(props.cards.get(id)?.name ?? exalted(id)?.name, language.value) || id.split('/').pop() || id

const keyOf = (option: AllyOption) => `${option.adventurer}:${option.kind}`
const chosen = reactive<Record<string, string>>({})
const options = computed(() =>
  (props.encounter.allyOptions ?? [])
    .map((option) => {
      const form = props.party.find((a) => a.name === option.adventurer)?.conduitForm
      const ids = option.kind === 'conduit' ? option.cards.filter((id) => id !== form) : option.cards
      return { ...option, key: keyOf(option), cards: ids.map((value) => ({ value, label: cardName(value) })) }
    })
    .filter((option) => option.cards.length),
)
function run(option: (typeof options.value)[number]) {
  const picked = option.cards.find((c) => c.value === chosen[option.key]) ?? option.cards[0]
  if (!picked) return
  const { adventurer } = option
  const card = picked.value
  if (option.kind === 'earnConduit') emit('command', { type: 'earnConduit', adventurer, card })
  else if (option.kind === 'conduit') emit('command', { type: 'conduitForm', adventurer, card })
  else if (option.kind === 'exalted') emit('command', { type: 'exaltedSummon', adventurer, card })
  else emit('command', { type: 'summon', adventurer, card })
  delete chosen[option.key]
}

// By hand: an ally of the table's, a rule the app does not play
const adding = ref(false)
const catalog = useAllyCards(adding)
const spoilers = ref(false)
const card = ref<string>()
const owner = ref<string>()
const addable = computed(() =>
  (catalog.data.value ?? [])
    .filter((c) => spoilers.value || !c.hidden)
    .map((c) => ({ value: c.id, label: pick(c.name, language.value) || c.id })),
)
const owners = computed(() => props.encounter.adventurers.map((a) => a.name))
function openAdd() {
  card.value = undefined
  owner.value = undefined
  spoilers.value = false
  adding.value = true
}
function add() {
  if (!card.value) return
  emit('command', { type: 'addAlly', card: card.value, owner: owner.value ?? undefined })
  adding.value = false
}
</script>

<template>
  <section class="allies" :aria-label="t('allies.title')">
    <div class="title">
      <h3>{{ t('allies.title') }}</h3>
      <Button icon="pi pi-plus" :label="t('allies.add')" size="small" text :disabled="locked" @click="openAdd" />
    </div>
    <ul v-if="allies.length" class="list">
      <li v-for="ally in allies" :key="ally.id">
        <button
          type="button"
          class="row"
          :class="{ selected: selected === `ally:${ally.id}` }"
          :aria-pressed="selected === `ally:${ally.id}`"
          @click="emit('select', `ally:${ally.id}`)"
        >
          <img v-if="cards.get(ally.card)?.image" :src="assetUrl(cards.get(ally.card)?.image, 160)" alt="" />
          <span class="who">
            <strong>{{ pick(ally.name, language) }}</strong>
            <small v-if="ally.owner">{{ t('allies.of', { name: ally.owner }) }}</small>
          </span>
          <Tag v-if="!ally.position" :value="t('play.notPlaced')" severity="warn" />
          <span class="numbers">
            {{ ally.damage }} / {{ ally.health ?? '?' }} {{ t('play.health') }}
            <small>{{ t('play.stamina', { sp: ally.stamina, max: ally.staminaMax }) }}</small>
          </span>
        </button>
        <ShowToPlayersButton :image="cards.get(ally.card)?.image" :caption="pick(ally.name, language)" />
      </li>
    </ul>
    <ul v-if="transformed.length || options.length" class="list options">
      <li v-for="member in transformed" :key="`form:${member.name}`">
        <span class="what">{{ t('allies.inForm', { name: member.name, card: cardName(member.conduitForm!) }) }}</span>
        <Button
          :label="t('allies.leaveForm')"
          size="small"
          severity="secondary"
          outlined
          :disabled="locked || !member.stamina"
          @click="emit('command', { type: 'conduitForm', adventurer: member.name })"
        />
      </li>
      <li v-for="option in options" :key="option.key">
        <span class="what">{{ t(`allies.option.${option.kind}`, { name: option.adventurer }) }}</span>
        <Select
          v-if="option.cards.length > 1"
          :model-value="chosen[option.key] ?? option.cards[0]?.value"
          :options="option.cards"
          option-label="label"
          option-value="value"
          size="small"
          :aria-label="t(`allies.option.${option.kind}`, { name: option.adventurer })"
          @update:model-value="(value: string) => (chosen[option.key] = value)"
        />
        <span v-else>{{ option.cards[0]?.label }}</span>
        <!-- The Exalted Esper's Ability is read on its card: the GM applies it -->
        <a
          v-if="option.kind === 'exalted'"
          :href="assetUrl(exalted(chosen[option.key] ?? option.cards[0]?.value ?? '')?.image)"
          target="_blank"
          rel="noopener"
          >{{ t('allies.readCard') }}</a
        >
        <Button
          :label="t(`allies.run.${option.kind}`)"
          size="small"
          severity="secondary"
          :disabled="locked"
          @click="run(option)"
        />
      </li>
    </ul>

    <Dialog v-model:visible="adding" modal :header="t('allies.add')" :style="{ width: 'min(26rem, 95vw)' }">
      <form class="add" @submit.prevent="add">
        <p>{{ t('allies.addHint') }}</p>
        <label>
          <span>{{ t('allies.card') }}</span>
          <Select v-model="card" :options="addable" option-label="label" option-value="value" filter />
        </label>
        <label>
          <span>{{ t('allies.owner') }}</span>
          <Select v-model="owner" :options="owners" show-clear :placeholder="t('allies.noOwner')" />
        </label>
        <label class="spoilers"><ToggleSwitch v-model="spoilers" /> {{ t('allies.spoilers') }}</label>
        <Button type="submit" :label="t('allies.addSubmit')" :disabled="!card || locked" />
      </form>
    </Dialog>
  </section>
</template>

<style scoped>
.allies {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  margin-bottom: 0.5rem;
}
.title {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
h3 {
  margin: 0;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.list li {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.3rem;
}
.row {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--p-blue-400);
  border-radius: var(--p-content-border-radius);
  background: var(--p-content-background);
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}
.row.selected {
  outline: 2px solid var(--p-yellow-400);
}
.row img {
  width: 2.4rem;
  height: 2.4rem;
  object-fit: cover;
  border-radius: 0.3rem;
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
  white-space: nowrap;
}
.options .what {
  flex: 1 1 100%;
  font-size: 0.85rem;
}
.add,
.add label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.add {
  gap: 0.75rem;
}
.add p {
  margin: 0;
  color: var(--p-text-muted-color);
}
.add .spoilers {
  flex-direction: row;
  align-items: center;
}
</style>
