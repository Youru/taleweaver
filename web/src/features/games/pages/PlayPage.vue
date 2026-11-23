<script setup lang="ts">
import { computed, nextTick, provide, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import { useRouter } from 'vue-router'
import { useAiAdvice } from '@/features/ai'
import { useCombatants, useItems } from '@/features/catalog'
import { DuelDialog } from '@/features/duel'
import { RoomPanel, roomGmApi } from '@/features/room'
import { ApiError } from '@/shared/api/http'
import { isHostDevice } from '@/shared/lib/screen'
import { showToPlayersKey, type ShowToPlayers } from '@/shared/lib/showToPlayers'
import { InventoryDialog } from '@/features/inventory'
import { useScenario } from '@/features/scenarios'
import { boardLayout, TableSelection, TableView, useBoardPieces, type Selection } from '@/features/table'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useGame, useGameCommand } from '../api/queries'
import type { PreparationCommand } from '../api/preparation'
import type { Answer, Cell, EncounterCommand, ItemCommand, PartyCommand, TableCommand } from '../api/types'
import type { HeldBy } from '../lib/initiativeTrack'
import CombatantList from '../components/encounter/CombatantList.vue'
import EncounterBar from '../components/encounter/EncounterBar.vue'
import EncounterGoals from '../components/encounter/EncounterGoals.vue'
import TurnCard from '../components/encounter/TurnCard.vue'
import AbraxisPanel from '../components/AbraxisPanel.vue'
import ArrivalPanel from '../components/ArrivalPanel.vue'
import ReequipNotice from '../components/ReequipNotice.vue'
import CountersBar from '../components/encounter/CountersBar.vue'
import EncounterTrackers from '../components/encounter/EncounterTrackers.vue'
import TurnTools from '../components/encounter/TurnTools.vue'
import { withAuthor } from '../lib/play'
import AlliesPanel from '../components/encounter/AlliesPanel.vue'
import PreparationPanel from '../components/preparation/PreparationPanel.vue'
import AchievementList from '../components/AchievementList.vue'
import AdventureSheetDialog from '../components/AdventureSheetDialog.vue'
import EndingPanel from '../components/EndingPanel.vue'
import GameBar from '../components/GameBar.vue'
import GameJournal from '../components/GameJournal.vue'
import PartyHud from '../components/PartyHud.vue'
import PromptPanel from '../components/PromptPanel.vue'
import StoryRoundPanel from '../components/StoryRoundPanel.vue'
import { storeItems } from '../lib/storyRound'
import { arrivedCombatants } from '../lib/arrivals'
import { cardsInPlay } from '../lib/cardsInPlay'
import { urgencyMax } from '../lib/options'
import { useErrorText } from '../lib/useErrorText'
import { encounterOver as isEncounterOver, storyPage } from '../lib/storyPage'
import ConsumablesDialog from '../components/comfort/ConsumablesDialog.vue'
import CorrectionDialog from '../components/comfort/CorrectionDialog.vue'
import EncounterWatch from '../components/comfort/EncounterWatch.vue'
import PauseScreen from '../components/comfort/PauseScreen.vue'
import ReadingDialog from '../components/comfort/ReadingDialog.vue'
import RevertDialog from '../components/comfort/RevertDialog.vue'
import SessionDialog from '../components/comfort/SessionDialog.vue'
import SeveralDialog from '../components/comfort/SeveralDialog.vue'
import { consumablesAtRisk, waitingFor } from '../lib/comfort'

/**
 * The GM's view. In an encounter: the table in the middle, and beside it the turn being played
 * (always in view) above tabs for the combatants, the journal and the encounter's goals.
 * Between encounters: the story, read like a book, with its question.
 */
const props = defineProps<{ id: string }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const host = isHostDevice()
// A tablet sleeps and loses the hub's pushes: it reads the game again by itself
const game = useGame(() => props.id, host ? {} : { refreshEvery: 10_000 })
/**
 * An error that replaces the page: nothing to show, or no right to it any more. A lost network keeps the page, and
 * what the GM was typing.
 */
const blocked = computed(() => {
  const error = game.error.value
  if (!game.isError.value) return false
  return !game.data.value || (error instanceof ApiError && error.status < 500)
})
const unlinked = computed(() => !host && game.error.value instanceof ApiError && game.error.value.status === 403)
const screensOpen = ref(false)
const state = computed(() => game.data.value?.state)
const encounter = computed(() => state.value?.encounter)
/** Combatants that appeared during this encounter: shown to place, never kept secret. */
const arrived = computed(() => arrivedCombatants(state.value?.log))
// Between encounters: the page being read (the whole journal is in its own tab)
const storyTab = ref('page')
const page = computed(() => storyPage(state.value?.log ?? []))
/** Adventurers not on the board yet: the encounter has no starting zone (or it is too small) */
const unplaced = computed(
  () => encounter.value?.adventurers.filter((a) => !a.position && !a.defeated).map((a) => a.name) ?? [],
)
/** An end condition was reached: what follows (grade, reward, choice) is the story again, not the table. */
const encounterOver = computed(() => !!encounter.value && isEncounterOver(state.value?.log ?? []))
// During a Bounty played from a Story Round, its own scenario holds the nodes read
const scenario = useScenario(() => state.value?.bounty?.scenario ?? game.data.value?.scenario)
const campaignScenario = useScenario(() => game.data.value?.scenario)
const node = computed(() => scenario.data.value?.nodes.find((n) => n.id === encounter.value?.node))
const cards = useCombatants(() =>
  encounter.value ? cardsInPlay(encounter.value, state.value?.campaign.party ?? []) : [],
)

const command = useGameCommand(() => props.id)

// The AI helper reads the board as the table draws it.
const pieces = useBoardPieces()
const layout = computed(() =>
  encounter.value ? boardLayout(node.value, encounter.value, pieces.data.value) : undefined,
)
/** Figures on the table (placed, not under the fog). */
const shownFigures = computed(() => (layout.value ? new Set(layout.value.figures.map((f) => f.id)) : undefined))
const ai = useAiAdvice(
  () => props.id,
  layout,
  encounter,
  cards,
  () => state.value?.log ?? [],
  () => state.value?.prompt,
  () => node.value?.tokens,
)
/** "totem:blue" → "Totem bleu". */
function tokenLabel(key: string) {
  const [kind = key, color] = key.split(':')
  const name = kind.charAt(0).toUpperCase() + kind.slice(1)
  return color ? t('workshop.print.tokenLabel', { name, color: t(`workshop.map.color.${color}`, color) }) : name
}
const { textOf } = useErrorText()
const commandError = computed(() => textOf(command.error.value))

function preparationCommand(value: PreparationCommand) {
  command.mutate({ kind: 'preparation', command: value })
}

function sendAnswer(value: Answer) {
  command.mutate({ kind: 'answer', answer: value })
}

/** Explore discards the Consumables left: their list first (play-spec M18). */
const consumablesOpen = ref(false)
function answer(value: Answer) {
  const campaign = state.value?.campaign
  if (campaign && state.value?.prompt?.kind === 'storyRound' && value.option === 'explore') {
    const typeOf = (id: string) => catalogItems.data.value?.find((i) => i.id === id)?.type
    if (consumablesAtRisk(campaign, typeOf).length) {
      consumablesOpen.value = true
      return
    }
  }
  sendAnswer(value)
}

function tableCommand(value: TableCommand) {
  command.mutate({ kind: 'table', command: value })
}

/** Who did what is being reported: proposed from the turn, changed by the GM. */
const author = ref<string | null>(null)
function encounterCommand(value: EncounterCommand) {
  command.mutate({ kind: 'encounter', command: withAuthor(value, author.value) })
}
const arriving = computed(() => {
  const prompt = state.value?.prompt
  return prompt?.kind === 'arrival' ? state.value?.campaign.party.find((a) => a.name === prompt.instance) : undefined
})

/** Board reports (damage, tokens…) are refused while a question other than an AI step is pending. */
const reportsLocked = computed(
  () => command.isPending.value || (!!state.value?.prompt && state.value.prompt.kind !== 'aiCondition'),
)

const selected = ref<Selection | null>(null)
const tab = ref('combatants')
// What is picked on the table opens its actions under the combatants, scrolled into view
const selectionPanel = useTemplateRef<{ $el: HTMLElement }>('selectionPanel')
watch(selected, async (now) => {
  if (!now) return
  tab.value = 'combatants'
  await nextTick()
  selectionPanel.value?.$el.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' })
})
/** The duel window, the figure it opened with, and the target the AI picked if any. */
const duel = ref<string | null>(null)
const duelTarget = ref<string | null>(null)
const duelSpell = ref(false)
const duelStrike = ref<string>()
const dueling = ref(false)

function openDuel(figure: string, target?: string, spell = false, strike?: string) {
  duelStrike.value = strike
  duel.value = figure
  duelTarget.value = target ?? null
  duelSpell.value = spell
  dueling.value = true
}

/** "@victory" and "@defeat", the workshop's endings, in words; any other ending as it is. */
function endingLabel(ending: string | undefined) {
  if (ending === '@victory') return t('play.endVictory')
  if (ending === '@defeat') return t('play.endDefeat')
  return ending ?? ''
}

/** The party's items, in their own window. */
const inventoryOpen = ref(false)
const roomOpen = ref(false)
/** The phone holding each adventurer (the room's), shown on the initiative track. */
const heldBy = ref<Record<string, HeldBy>>({})
/** Requests of the players waiting for the GM (room-spec §6.2): a badge on the Room button. */
const pendingRequests = ref(0)
/** While the room is open, the cards of the page can be shown on the phones (room-spec §7.5). */
const roomIsOpen = ref(false)
const players = ref<{ id: string; name: string }[]>([])
const sheetOpen = ref(false)
/** The scenario being read: the Bounty's, during one. */
const readScenario = computed(() => state.value?.bounty?.scenario ?? game.data.value?.scenario ?? '')
/** Was there an encounter to try again? */
const fought = computed(() => !!state.value?.log.some((line) => line.code === 'encounter'))
provide(
  showToPlayersKey,
  computed<ShowToPlayers | null>(() =>
    roomIsOpen.value
      ? { players: players.value, show: (image, caption, player) => roomGmApi.show(props.id, image, caption, player) }
      : null,
  ),
)
const toHandOut = computed(() => state.value?.campaign.unassigned?.length ?? 0)

function itemCommand(value: ItemCommand) {
  command.mutate({ kind: 'items', command: value })
}

/** A Story Round between encounters (not while a Side Quest is played): its panel, and its Store in the inventory. */
const storyRoundNode = computed(() => {
  const round = state.value?.campaign.storyRound
  if (!round || round.phase === 'sideQuest' || encounter.value) return undefined
  return campaignScenario.data.value?.nodes.find((n) => n.id === round.node)
})
const catalogItems = useItems()
const store = computed(() => {
  const round = storyRoundNode.value
  const campaign = state.value?.campaign
  if (!round?.store || !campaign || campaign.storyRound?.phase !== 'shop') return null
  // A Unique Item bought is gone from the Store (limit 1)
  const uniques = (campaign.storeUniques?.[round.id] ?? []).filter((id) => !campaign.items.includes(id))
  return storeItems(round.store, catalogItems.data.value ?? [], uniques).map((i) => i.id)
})

function partyCommand(value: PartyCommand) {
  command.mutate({ kind: 'party', command: value })
}

// The GM's tools (play-spec lot 5)
const correctOpen = ref(false)
const severalOpen = ref(false)
const sessionOpen = ref(false)
const readingOpen = ref(false)
const revertLine = ref<number | null>(null)
const inFight = computed(() => !!encounter.value && !encounterOver.value)
// The last blow may end the encounter before the duel closes
watch(inFight, (fighting) => {
  if (!fighting) dueling.value = false
})
const tools = computed(() => [
  { label: t('comfort.tools.session'), icon: 'pi pi-calendar', command: () => (sessionOpen.value = true) },
  { label: t('comfort.tools.reading'), icon: 'pi pi-book', command: () => (readingOpen.value = true) },
  {
    label: t('comfort.tools.correct'),
    icon: 'pi pi-pencil',
    disabled: !!state.value?.preparation || state.value?.status === 'Finished',
    command: () => (correctOpen.value = true),
  },
  {
    label: t('comfort.tools.several'),
    icon: 'pi pi-clone',
    disabled: !inFight.value,
    command: () => (severalOpen.value = true),
  },
  {
    label: t('comfort.tools.pause'),
    icon: 'pi pi-pause',
    command: () => tableCommand({ type: 'pause', paused: true }),
  },
])
const seatNames = computed(() => Object.fromEntries(Object.entries(heldBy.value).map(([a, seat]) => [a, seat.name])))
/** "Waiting for" (play-spec M15): the adventurer, and who plays them tonight. */
const waitedName = computed(() =>
  !state.value || state.value.preparation || state.value.status === 'Finished' ? undefined : waitingFor(state.value),
)
const waited = computed(() => {
  const name = waitedName.value
  if (name === undefined) return null
  if (!name) return t('comfort.waitingGm')
  const at = game.data.value?.table.players.find((p) => p.adventurer === name)
  const player = (at?.present === false ? at.tonight : at?.player) || seatNames.value[name]
  return player ? `${name} (${player})` : name
})
const hud = computed(() => ({
  party: state.value?.campaign.party ?? [],
  pieces: pieces.data.value,
  gold: state.value?.campaign.gold ?? 0,
  waiting: waited.value,
  waitedName: waitedName.value,
}))
const router = useRouter()
// The second window is the computer's: on the tablet it would be one more view with the GM's rights
const tableUrl = computed(() => (host ? router.resolve({ name: 'table', params: { id: props.id } }).href : undefined))

function moveFigure(figure: string, to: Cell) {
  encounterCommand({ type: 'move', figure, to })
}
</script>

<template>
  <main class="play">
    <div v-if="game.isPending.value" class="center">
      <ProgressSpinner :aria-label="t('common.loading')" />
    </div>
    <Message v-else-if="blocked" :severity="unlinked ? 'warn' : 'error'">
      {{ unlinked ? t('screens.gmUnlinked') : t('common.error', { message: game.error.value?.message ?? '' }) }}
    </Message>
    <template v-else-if="state && game.data.value">
      <Teleport defer to="#app-bar">
        <GameBar
          :title="pick(campaignScenario.data.value?.title, language) || game.data.value.scenario"
          :bounty="
            state.bounty
              ? t('storyRound.bountyPlaying', { name: pick(scenario.data.value?.title, language) })
              : undefined
          "
          :preparing="!!state.preparation"
          :to-hand-out="toHandOut"
          :requests="pendingRequests"
          :revealed="state.campaign.revealedHiddenCards.length"
          :can-undo="game.data.value.canUndo && !command.isPending.value"
          :undo-blocked="!!game.data.value.undoBlocked"
          :tools="tools"
          @undo="command.mutate({ kind: 'undo' })"
          @inventory="inventoryOpen = true"
          @room="roomOpen = true"
          @screens="screensOpen = true"
          @sheet="sheetOpen = true"
        />
      </Teleport>
      <Message v-if="game.isError.value" severity="warn" :closable="false">{{ t('screens.offline') }}</Message>
      <PartyHud v-if="!inFight || state.preparation" v-bind="hud" show-xp />
      <Message v-if="commandError" severity="error" closable @close="command.reset()">{{ commandError }}</Message>
      <ReequipNotice
        :campaign="state.campaign"
        :busy="command.isPending.value"
        @command="encounterCommand"
        @inventory="inventoryOpen = true"
      />
      <ArrivalPanel
        v-if="arriving"
        :member="arriving"
        :gold="state.campaign.gold"
        :to-hand-out="toHandOut"
        :busy="command.isPending.value"
        @party="partyCommand"
        @inventory="inventoryOpen = true"
        @room="roomOpen = true"
      />
      <PauseScreen
        v-if="game.data.value.table.paused"
        resumable
        :busy="command.isPending.value"
        @resume="tableCommand({ type: 'pause', paused: false })"
      />
      <CorrectionDialog
        v-model:open="correctOpen"
        :state="state"
        :nodes="scenario.data.value?.nodes ?? []"
        :busy="command.isPending.value"
        :error="commandError"
        @command="encounterCommand"
      />
      <SessionDialog
        v-model:open="sessionOpen"
        :game-id="id"
        :state="state"
        :table="game.data.value.table"
        :nodes="scenario.data.value?.nodes ?? []"
        :seats="seatNames"
        :busy="command.isPending.value"
        @command="tableCommand"
      />
      <ReadingDialog v-model:open="readingOpen" :page="page" :names="state.campaign.party.map((a) => a.name)" />
      <SeveralDialog
        v-if="encounter"
        v-model:open="severalOpen"
        :encounter="encounter"
        :locked="reportsLocked"
        @command="encounterCommand"
      />
      <ConsumablesDialog
        v-model:open="consumablesOpen"
        :campaign="state.campaign"
        :busy="command.isPending.value"
        @command="itemCommand"
        @confirm="sendAnswer({ option: 'explore' })"
      />
      <RevertDialog
        v-model:line="revertLine"
        :log="state.log"
        :actions="game.data.value.actions"
        :busy="command.isPending.value"
        @confirm="(actions, outOf) => tableCommand({ type: 'revert', actions, outOf })"
      />
      <AdventureSheetDialog :id="id" v-model:open="sheetOpen" :state="state" />
      <RoomPanel
        v-model:open="roomOpen"
        v-model:screens="screensOpen"
        :game-id="id"
        :party="state.campaign.party"
        :active-group="state.campaign.activeGroup"
        :stash="[...state.campaign.stash, ...state.campaign.unassigned]"
        @seats="(seats) => (heldBy = seats)"
        @pending="(count) => (pendingRequests = count)"
        @players="(list) => (players = list)"
        @open="(open) => (roomIsOpen = open)"
      />
      <InventoryDialog
        v-model:open="inventoryOpen"
        :campaign="state.campaign"
        :encounter="encounter && !encounterOver ? encounter : undefined"
        :node="node"
        :busy="command.isPending.value"
        :error="commandError"
        :store="store"
        @command="itemCommand"
      />

      <PreparationPanel
        v-if="state.preparation"
        :preparation="state.preparation"
        :party="state.campaign.party"
        :held-by="heldBy"
        :room-open="roomIsOpen"
        :busy="command.isPending.value"
        @command="preparationCommand"
        @room="roomOpen = true"
      />
      <!-- Encounter: table in the middle, turn and tabs beside it -->
      <template v-else-if="encounter && !encounterOver">
        <EncounterBar
          :encounter="encounter"
          :node="node"
          :disabled="!!state.prompt || !!encounter.reactions?.length || command.isPending.value"
          movable
          :held-by="heldBy"
          :urgency-max="urgencyMax(state.options)"
          :busy="command.isPending.value"
          @next-turn="encounterCommand({ type: 'nextTurn' })"
          @move-card="(card, by) => encounterCommand({ type: 'moveInitiative', card, by })"
        >
          <PartyHud v-bind="hud" />
        </EncounterBar>
        <div class="encounter">
          <TableView
            v-model:selected="selected"
            class="table"
            :node="node"
            :encounter="encounter"
            :cards="cards"
            :locked="reportsLocked"
            :hint="ai.hint.value"
            :arrived="arrived"
            :party="state.campaign.party"
            :window-url="tableUrl"
            selection-outside
            @command="encounterCommand"
            @duel="openDuel"
          >
            <template #head>
              <EncounterBar :encounter="encounter" :node="node" :urgency-max="urgencyMax(state.options)" readonly />
              <CountersBar :counters="state.campaign.counters" />
              <Message v-if="commandError" severity="error" closable @close="command.reset()">{{
                commandError
              }}</Message>
            </template>
          </TableView>
          <aside class="side">
            <AbraxisPanel
              v-if="state.abraxis"
              :game="state.abraxis"
              :pending="command.isPending.value"
              @answer="answer"
            />
            <TurnCard
              v-else
              class="turn"
              :encounter="encounter"
              :log="state.log"
              :prompt="encounter.reactions?.length && state.prompt?.kind === 'aiCondition' ? undefined : state.prompt"
              :cards="cards"
              :pending="command.isPending.value"
              :suggestion="ai.condition.value"
              :advice="ai.advice.value"
              @answer="answer"
              @duel="openDuel"
              @move="moveFigure"
            />
            <EncounterWatch
              :encounter="encounter"
              :node="node"
              :campaign="state.campaign"
              :scenario="readScenario"
              :options="state.options"
              :unplaced="unplaced.length"
              :locked="reportsLocked"
              @command="encounterCommand"
            />
            <TurnTools
              v-model:author="author"
              :encounter="encounter"
              :party="state.campaign.party"
              :locked="reportsLocked"
              @command="encounterCommand"
              @duel="openDuel"
            />
            <CountersBar :counters="state.campaign.counters" />
            <Message v-for="token in ai.seen.value" :key="token.key" severity="warn" size="small" class="unplaced seen">
              <span>{{ t('table.totemSeen', { who: token.by, token: tokenLabel(token.key) }) }}</span>
              <Button
                :label="t('table.activateTotem', { token: tokenLabel(token.key) })"
                icon="pi pi-bolt"
                size="small"
                :disabled="reportsLocked"
                @click="encounterCommand({ type: 'activateToken', token: token.key })"
              />
            </Message>
            <Message v-if="unplaced.length" severity="info" size="small" class="unplaced">
              {{ t('table.placeAdventurers', { names: unplaced.join(', ') }) }}
            </Message>
            <Tabs v-model:value="tab" class="tabs">
              <TabList>
                <Tab value="combatants">{{ t('play.tabCombatants') }}</Tab>
                <Tab value="journal">{{ t('play.journal') }}</Tab>
                <Tab value="goals">{{ t('play.tabEncounter') }}</Tab>
              </TabList>
              <TabPanels>
                <TabPanel value="combatants">
                  <AlliesPanel
                    :encounter="encounter"
                    :party="state.campaign.party"
                    :cards="cards"
                    :selected="selected?.kind === 'figure' ? selected.id : undefined"
                    :locked="reportsLocked"
                    @command="encounterCommand"
                    @select="selected = { kind: 'figure', id: $event }"
                  />
                  <CombatantList
                    :encounter="encounter"
                    :cards="cards"
                    :shown="shownFigures"
                    :arrived="arrived"
                    :pieces="pieces.data.value"
                    :selected="selected?.kind === 'figure' ? selected.id : undefined"
                    @select="selected = { kind: 'figure', id: $event }"
                  />
                  <TableSelection
                    v-if="selected && layout"
                    ref="selectionPanel"
                    class="selection"
                    :selection="selected"
                    :layout="layout"
                    :encounter="encounter"
                    :node="node"
                    :cards="cards"
                    :locked="reportsLocked"
                    :party="state.campaign.party"
                    @command="encounterCommand"
                    @duel="openDuel"
                    @close="selected = null"
                  />
                </TabPanel>
                <TabPanel value="journal">
                  <GameJournal :log="state.log" revertible class="journal" @revert="revertLine = $event" />
                </TabPanel>
                <TabPanel value="goals">
                  <EncounterTrackers
                    :campaign="state.campaign"
                    :encounter="encounter"
                    :node="node"
                    :locked="reportsLocked"
                    @command="encounterCommand"
                  />
                  <EncounterGoals
                    :encounter="encounter"
                    :node="node"
                    :locked="reportsLocked"
                    :campaign="state.campaign"
                    :scenario="readScenario"
                    :options="state.options"
                    @command="encounterCommand"
                  />
                </TabPanel>
              </TabPanels>
            </Tabs>
          </aside>
        </div>
        <DuelDialog
          v-model:open="dueling"
          :game="id"
          :encounter="encounter"
          :node="node"
          :cards="cards"
          :party="state?.campaign.party"
          :from="duel"
          :to="duelTarget"
          :spell="duelSpell"
          :strike="duelStrike"
          :log="state.log"
          :error="commandError"
          :locked="reportsLocked"
          @command="encounterCommand"
        />
      </template>

      <!-- Between encounters: the story -->
      <div v-else class="story">
        <Tabs v-model:value="storyTab" class="tabs">
          <TabList>
            <Tab value="page">{{ t('play.story') }}</Tab>
            <Tab value="journal">{{ t('play.journal') }}</Tab>
          </TabList>
          <TabPanels>
            <TabPanel value="page" class="page-panel">
              <GameJournal :log="page" page class="journal" />
            </TabPanel>
            <TabPanel value="journal" class="page-panel">
              <GameJournal :log="state.log" revertible class="journal" @revert="revertLine = $event" />
            </TabPanel>
          </TabPanels>
        </Tabs>
        <StoryRoundPanel
          v-if="storyRoundNode"
          class="story-panel"
          :campaign="state.campaign"
          :node="storyRoundNode"
          :xp-leniency="state.options?.xpLeniency"
          :busy="command.isPending.value"
          @party="partyCommand"
          @item="itemCommand"
          @shop="inventoryOpen = true"
        />
        <AchievementList
          v-if="encounter && node"
          :node="node"
          :scenario="readScenario"
          :campaign="state.campaign"
          :locked="command.isPending.value"
          @complete="(achievement, adventurer) => encounterCommand({ type: 'achievement', achievement, adventurer })"
        />
        <AbraxisPanel v-if="state.abraxis" :game="state.abraxis" :pending="command.isPending.value" @answer="answer" />
        <PromptPanel
          v-else-if="state.prompt"
          :key="state.log.length"
          :prompt="state.prompt"
          :pending="command.isPending.value"
          @answer="answer"
        />
        <Message v-if="state.status === 'Finished'" severity="success">
          {{ t('play.finished', { ending: endingLabel(state.ending) }) }}
        </Message>
        <EndingPanel v-if="state.status === 'Finished'" :id="id" :fought="fought" />
      </div>
    </template>
  </main>
</template>

<style scoped>
.play {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  box-sizing: border-box;
  /* The banner of a game (App.vue) and its margin */
  height: calc(100dvh - 3.75rem - 2px);
  padding: 0 1rem 1rem;
}
.center {
  display: grid;
  place-items: center;
  flex: 1;
}
.encounter {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(22rem, 28rem);
  gap: 1rem;
  flex: 1;
  min-height: 0;
}
.table {
  min-height: 0;
}
.side {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 0;
  overflow-y: auto;
}
.turn {
  flex: none;
  max-height: 36%;
  padding: 0.75rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
/* A question is read whole */
.turn:has(.prompt) {
  max-height: 62%;
}
/* The notice keeps its height: the column would squash it to a line */
.side :deep(.unplaced) {
  flex: none;
}
.seen :deep(.p-message-text) {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
}
.side .tabs {
  min-height: 14rem;
}
.tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.tabs :deep(.p-tabpanels) {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 0.75rem 0 0;
}
.tabs :deep(.p-tablist-tab-list) {
  background: transparent;
}
.journal {
  height: 100%;
}
.selection {
  margin-top: 0.75rem;
}
.story {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
  width: min(64rem, 100%);
  margin: 0 auto;
}
/* The Story Round's panel scrolls on its own: the page of the story keeps room to be read */
.story .tabs {
  min-height: 12rem;
}
.story-panel {
  flex: 0 1 auto;
  min-height: 0;
  max-height: 50%;
  overflow-y: auto;
}
.story .tabs :deep(.p-tabpanels) {
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
/* PrimeVue's panel does not carry this page's scope attribute: reached through :deep */
.story :deep(.page-panel) {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.story .journal {
  flex: 1;
  min-height: 0;
}
/* The story sits on the site's parchment: the tabs and their panel carry no card of their own */
.story .tabs,
.story .tabs :deep(.p-tablist),
.story .tabs :deep(.p-tablist-content),
.story .tabs :deep(.p-tablist-tab-list),
.story .tabs :deep(.p-tab),
.story .tabs :deep(.p-tabpanels) {
  background: transparent;
  border-color: rgb(80 50 25 / 0.25);
}
.story .tabs :deep(.p-tabpanels) {
  padding: 0;
}
.story > :deep(.prompt) {
  background: rgb(246 238 220 / 0.6);
  border-width: 1px;
  border-color: rgb(109 26 26 / 0.4);
}
@media (max-width: 1000px) {
  .play {
    height: auto;
  }
  .encounter {
    grid-template-columns: 1fr;
  }
  .table {
    /* Boards are wider than tall: no more height than the width needs. */
    height: min(60vh, 80vw);
  }
  .turn {
    max-height: none;
  }
  .tabs :deep(.p-tabpanels) {
    max-height: 60vh;
  }
}
</style>
