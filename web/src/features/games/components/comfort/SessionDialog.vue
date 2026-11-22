<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import Textarea from 'primevue/textarea'
import { useDisciplines, useItems } from '@/features/catalog'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { GameState, GameTable, OwnedItem, TableCommand, TablePlayer } from '../../api/types'
import { encountersAhead, materialOf } from '../../lib/comfort'
import { shortId } from '../../lib/ids'
import GameJournal from '../GameJournal.vue'
import MaterialList from './MaterialList.vue'

/**
 * The evening (play-spec M4 to M8): where the table was, who is here, the material, the next encounter, and
 * how to put the game away.
 */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{
  gameId: string
  state: GameState
  table: GameTable
  nodes: readonly ScenarioNode[]
  /** The player of each adventurer's phone, proposed as who plays them. */
  seats: Record<string, string>
  busy: boolean
}>()
const emit = defineEmits<{ command: [command: TableCommand] }>()
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const items = useItems()
const disciplines = useDisciplines()

const tab = ref('summary')
const campaign = computed(() => props.state.campaign)
const here = computed(() =>
  props.nodes.find((n) => n.id === (props.state.encounter?.node ?? campaign.value.currentNode)),
)
const titleOf = (node: ScenarioNode | undefined) => pick(node?.name ?? node?.title, language.value) || node?.id || ''

// M4: an evening, what marked it
const sessions = computed(() =>
  props.table.sessions.map((session, i) => ({
    value: i,
    label: new Date(session.at).toLocaleString(language.value, { dateStyle: 'medium', timeStyle: 'short' }),
  })),
)
const evening = ref(0)
const telling = new Set([
  ...['scene', 'storyRound', 'encounter', 'encounterEnd', 'bountyStart', 'bountyEnd', 'ventureForth', 'sideQuest'],
  ...['joinParty', 'leaveParty', 'adventurerDefeated', 'achievement', 'revealHiddenCard', 'corrected'],
  ...['gainGold', 'gainXp', 'gainItem', 'urgencyDefeat', 'scenarioEnd'],
])
const highlights = computed(() => {
  const from = props.table.sessions[evening.value]?.line ?? 0
  const until = props.table.sessions[evening.value + 1]?.line
  // Titles only: where the table was reads in a minute, the pages stay in the journal
  return props.state.log
    .slice(from, until)
    .filter((line) => telling.has(line.code))
    .slice(-14)
    .map(({ code, args }) => ({ code, args }))
})
const notes = computed(() => props.table.notes.filter((note) => note.kind === 'note'))
const note = ref('')
function addNote() {
  emit('command', { type: 'note', text: note.value, kind: 'note' })
  note.value = ''
}

// M17: the table's rulings, with the page of the rulebook
const rulings = computed(() => props.table.notes.filter((kept) => kept.kind === 'ruling'))
const ruling = ref('')
const rulingPage = ref<number | null>(null)
function addRuling() {
  emit('command', { type: 'note', kind: 'ruling', text: ruling.value, page: rulingPage.value ?? undefined })
  ruling.value = ''
  rulingPage.value = null
}

// M6
const players = ref<TablePlayer[]>([])
function readPlayers() {
  players.value = campaign.value.party.map(({ name }) => {
    const known = props.table.players.find((p) => p.adventurer === name)
    return {
      adventurer: name,
      player: known?.player ?? props.seats[name] ?? '',
      present: known?.present ?? true,
      tonight: known?.tonight ?? '',
    }
  })
}
watch(
  open,
  (shown) => {
    if (!shown) return
    evening.value = Math.max(0, props.table.sessions.length - 1)
    readPlayers()
  },
  { immediate: true },
)
const presentPlayers = computed(() => [
  ...new Set(players.value.filter((p) => p.present && p.player).map((p) => p.player as string)),
])

// M5
const ahead = computed(() => encountersAhead(props.nodes, here.value?.id))

// M7
const itemName = (owned: OwnedItem) =>
  pick(items.data.value?.find((i) => i.id === owned.item)?.name, language.value) ||
  [owned.tiers?.join('/'), owned.type].filter(Boolean).join(' ') ||
  owned.id
function cardsOf(list: OwnedItem[] | undefined) {
  return (list ?? [])
    .map((owned) => {
      const marks = (['flipped', 'exhausted', 'used'] as const)
        .filter((mark) => owned[mark])
        .map((mark) => t(`marks.mark.${mark}`))
      return marks.length ? `${itemName(owned)} (${marks.join(', ')})` : itemName(owned)
    })
    .join(', ')
}
const bags = computed(() =>
  campaign.value.party.map((a) => ({
    name: a.name,
    lines: [
      ['equipped', cardsOf(a.equipped)],
      ['pack', cardsOf(a.pack)],
      [
        'disciplines',
        (a.disciplines ?? [])
          .map((d) => pick(disciplines.data.value?.find((c) => c.id === d.id)?.name, language.value) || shortId(d.id))
          .join(', '),
      ],
      ['conduits', (a.conduits ?? []).map(shortId).join(', ')],
      [
        'kept',
        [
          ...(a.damage ? [t('comfort.storage.damage', { n: a.damage })] : []),
          ...(a.stamina ? [t('comfort.storage.stamina', { n: a.stamina })] : []),
          ...Object.entries(a.abilityTokens ?? {}).map(([token, n]) => `${t(`marks.token.${token}`)} ×${n}`),
          ...(a.summonTokens ? [`${t('marks.token.summon')} ×${a.summonTokens}`] : []),
          ...(a.flippedCards ?? []).map((card) => `${shortId(card)} (${t('marks.mark.flipped')})`),
          ...(a.usedCards ?? []).map((card) => `${shortId(card)} (${t('marks.mark.used')})`),
        ].join(', '),
      ],
    ].filter(([, text]) => text) as [string, string][],
  })),
)
const shared = computed(
  () =>
    [
      ['stash', cardsOf(campaign.value.stash)],
      ['unassigned', cardsOf(campaign.value.unassigned)],
      ['gold', String(campaign.value.gold)],
      ['upgrades', (campaign.value.unlockedUpgrades ?? []).map(shortId).join(', ')],
      ['revealed', campaign.value.revealedHiddenCards.join(', ')],
    ].filter(([, text]) => text) as [string, string][],
)
</script>

<template>
  <Dialog v-model:visible="open" modal :header="t('comfort.session.title')" :style="{ width: 'min(52rem, 96vw)' }">
    <Tabs v-model:value="tab" scrollable>
      <TabList>
        <Tab v-for="name in ['summary', 'players', 'material', 'next', 'rulings', 'storage']" :key="name" :value="name">
          {{ t(`comfort.session.tab.${name}`) }}
        </Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="summary">
          <p class="where">
            <strong>{{ titleOf(here) }}</strong>
            <span v-if="state.encounter && !state.encounter.ended">
              · {{ t('play.round', { round: state.encounter.round }) }} ·
              {{ t('comfort.session.urgency', { n: state.encounter.urgency }) }}
            </span>
          </p>
          <p>
            {{
              campaign.party
                .map((a) => `${a.name} ${(a.health ?? 0) - (a.damage ?? 0)}/${a.health ?? '?'}`)
                .join(' · ')
            }}
          </p>
          <p v-if="state.prompt?.question">
            {{ t('comfort.session.question', { question: pick(state.prompt.question, language) }) }}
          </p>
          <Select
            v-if="sessions.length"
            v-model="evening"
            :options="sessions"
            option-label="label"
            option-value="value"
            :aria-label="t('comfort.session.evening')"
          />
          <GameJournal v-if="highlights.length" :log="highlights" class="highlights" />
          <h4>{{ t('comfort.session.notes') }}</h4>
          <ul>
            <li v-for="kept in notes" :key="kept.id">
              <small>{{ new Date(kept.at).toLocaleDateString(language) }}</small>
              <span class="text">{{ kept.text }}</span>
              <Button
                icon="pi pi-times"
                text
                size="small"
                :aria-label="t('comfort.session.deleteNote')"
                :disabled="busy"
                @click="emit('command', { type: 'deleteNote', id: kept.id })"
              />
            </li>
          </ul>
          <Textarea v-model="note" rows="2" :placeholder="t('comfort.session.notePlaceholder')" class="wide" />
          <Button :label="t('comfort.session.addNote')" size="small" :disabled="busy || !note.trim()" @click="addNote" />
        </TabPanel>

        <TabPanel value="players">
          <p class="muted">{{ t('comfort.players.intro') }}</p>
          <div v-for="line in players" :key="line.adventurer" class="player">
            <strong>{{ line.adventurer }}</strong>
            <InputText v-model="line.player" maxlength="40" :placeholder="t('comfort.players.player')" />
            <label><Checkbox v-model="line.present" binary /> {{ t('comfort.players.present') }}</label>
            <Select
              v-if="!line.present"
              v-model="line.tonight"
              :options="presentPlayers"
              editable
              :placeholder="t('comfort.players.tonight')"
            />
          </div>
          <Button
            :label="t('comfort.players.save')"
            :disabled="busy"
            @click="emit('command', { type: 'players', players })"
          />
        </TabPanel>

        <TabPanel value="material">
          <MaterialList
            v-if="state.encounter"
            :material="materialOf(here, state.encounter)"
            :storage-key="`material:${gameId}:${here?.id}`"
          />
          <p v-else>{{ t('comfort.material.outside') }}</p>
        </TabPanel>

        <TabPanel value="next">
          <p class="muted">{{ t('comfort.next.warning') }}</p>
          <p v-if="!ahead.length">{{ t('comfort.next.none') }}</p>
          <details v-for="node in ahead" :key="node.id">
            <summary>{{ titleOf(node) }}</summary>
            <p v-if="node.lootLevel">{{ t('comfort.next.loot', { level: node.lootLevel }) }}</p>
            <p v-for="rule in node.specialRules ?? []" :key="rule.id">
              <strong>{{ pick(rule.name, language) }}</strong> {{ pick(rule.text, language) }}
            </p>
            <MaterialList :material="materialOf(node)" :storage-key="`material:${gameId}:${node.id}`" />
          </details>
        </TabPanel>

        <TabPanel value="rulings">
          <ul>
            <li v-for="kept in rulings" :key="kept.id">
              <small v-if="kept.page">{{ t('comfort.session.cited', { page: kept.page }) }}</small>
              <span class="text">{{ kept.text }}</span>
              <Button
                icon="pi pi-times"
                text
                size="small"
                :aria-label="t('comfort.session.deleteNote')"
                :disabled="busy"
                @click="emit('command', { type: 'deleteNote', id: kept.id })"
              />
            </li>
          </ul>
          <Textarea v-model="ruling" rows="2" :placeholder="t('comfort.session.ruling')" class="wide" />
          <InputNumber v-model="rulingPage" :min="1" :max="200" :placeholder="t('comfort.session.rulingPage')" />
          <Button
            :label="t('comfort.session.addRuling')"
            size="small"
            :disabled="busy || !ruling.trim()"
            @click="addRuling"
          />
        </TabPanel>

        <TabPanel value="storage">
          <section v-for="bag in bags" :key="bag.name">
            <h4>{{ t('comfort.storage.bag', { name: bag.name }) }}</h4>
            <p v-for="[kind, text] in bag.lines" :key="kind">
              <strong>{{ t(`comfort.storage.${kind}`) }}</strong> {{ text }}
            </p>
          </section>
          <h4>{{ t('comfort.storage.shared') }}</h4>
          <p v-for="[kind, text] in shared" :key="kind">
            <strong>{{ t(`comfort.storage.${kind}`) }}</strong> {{ text }}
          </p>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </Dialog>
</template>

<style scoped>
p {
  margin: 0.3rem 0;
}
h4 {
  margin: 0.8rem 0 0.2rem;
}
.where {
  font-size: 1.1rem;
}
.highlights {
  max-height: 30vh;
  margin-top: 0.4rem;
}
.muted,
small {
  color: var(--p-text-muted-color);
}
ul {
  margin: 0;
  padding: 0;
  list-style: none;
}
li,
.player {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.4rem;
}
li .text {
  flex: 1;
  white-space: pre-line;
}
.player strong {
  min-width: 8rem;
}
.wide {
  width: 100%;
}
summary {
  padding: 0.4rem 0;
  font-weight: 600;
}
</style>
