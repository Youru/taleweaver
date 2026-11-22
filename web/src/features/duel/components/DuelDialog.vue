<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import { isOpen, readBoard, situationOf, stepKey, strikesOfTurn } from '@/features/ai'
import { useItems, type CatalogItem, type Combatant } from '@/features/catalog'
import type { AdventurerState, EncounterCommand, EncounterState, LogEntry, Resolution } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import { BoardCanvas, boardLayout, useBoardPieces, type Selection } from '@/features/table'
import { usePreferences } from '@/shared/lib/preferences'
import { useDice } from '../api/queries'
import { partyEquipment } from '../lib/equipment'
import { distance, fighters, type AdventurerSheet, type Fighter } from '../lib/fighters'
import { commandSheet, withConduitForms } from '../lib/scaling'
import { useAdventurerSheets } from '../lib/sheets'
import { cardWeapon, equippedWeapon } from '../lib/weapons'
import AttackArena from './AttackArena.vue'
import FighterHead from './FighterHead.vue'
import SpellArena from './SpellArena.vue'

/**
 * The duel, in its own window over the game: who attacks whom, the odds if the table wants them,
 * the dice tapped as they landed, and the whole action validated in one click, which closes it.
 * Nothing else matters while it is open; closing it goes back to the table as it was.
 */
const open = defineModel<boolean>('open', { required: true })
const props = defineProps<{
  game: string
  encounter: EncounterState
  /** The encounter's scenario node, to draw the board. */
  node: ScenarioNode | undefined
  cards: Map<string, Combatant>
  /** The party, with what each adventurer has equipped: their dice, Defense and Armor come from it. */
  party?: AdventurerState[]
  /** The figure the duel opened with (combatant instance or "adventurer:<name>"). */
  from: string | null
  /** The target the AI picked, if any. */
  to?: string | null
  /** The AI casts a Spell: open on the Spell side. */
  spell?: boolean
  /** The AI Attack or Spell it was opened for. */
  strike?: string
  /** The journal: which AI actions are resolved, and when what was sent is done. */
  log: LogEntry[]
  locked: boolean
  error?: string | null
}>()
const emit = defineEmits<{ command: [command: EncounterCommand] }>()

const { t } = useI18n()
const { language, showOdds } = storeToRefs(usePreferences())
const dice = useDice()
const pieces = useBoardPieces()
const { sheets, remember } = useAdventurerSheets(() => props.game)
const items = useItems()
const itemsById = computed(() => new Map<string, CatalogItem>((items.data.value ?? []).map((i) => [i.id, i])))
const equipment = computed(() => {
  const catalog = itemsById.value
  const gear = partyEquipment(props.party, catalog, language.value)
  const result = withConduitForms(gear, props.party, props.cards, catalog, language.value)
  for (const ally of props.encounter.allies ?? []) {
    const owner = props.party?.find((a) => a.name === ally.owner)
    const card = props.cards.get(ally.card)
    // An ally whose Defense nobody gives stays the GM's to enter
    if (!card || (!owner && card.stats?.defense === '*')) continue
    const name = owner?.name
    const ownerSheet = name ? (gear[name.toLowerCase()] ?? sheets.value[name]) : undefined
    result[`ally:${ally.id}`] = commandSheet(card, owner, ownerSheet, catalog, language.value)
  }
  return result
})

// The board as the table draws it: what lies under the fog is not on the table yet.
const layout = computed(() => boardLayout(props.node, props.encounter, pieces.data.value))
/** Figures standing on a revealed tile: not those under the fog, still to place, or dropped off the tiles. */
const onTable = computed(() => {
  const tiles = layout.value.tiles.filter((tile) => !tile.hidden)
  if (!layout.value.tiles.length) return undefined // no map (scenario not loaded): every figure
  const onTile = (x: number, y: number) =>
    tiles.some((tile) => x >= tile.x && x < tile.x + tile.w && y >= tile.y && y < tile.y + tile.h)
  return new Set(layout.value.figures.filter((f) => onTile(f.x, f.y)).map((f) => f.id))
})
const all = computed(() =>
  fighters(
    props.encounter,
    props.cards,
    sheets.value,
    language.value,
    pieces.data.value,
    equipment.value,
    props.party,
  ).filter((f) => !onTable.value || onTable.value.has(f.id) || f.id === props.from),
)
const attackerId = ref<string | undefined>(props.from ?? undefined)
const targetId = ref<string>()
const attacker = computed(() => all.value.find((f) => f.id === attackerId.value))
const target = computed(() => all.value.find((f) => f.id === targetId.value))

/** Opponents first, nearest first, then the others (an ally can be targeted too). */
function targetsOf(from: Fighter | undefined) {
  if (!from) return []
  return all.value
    .filter((f) => f.id !== from.id)
    .map((f) => ({ fighter: f, range: distance(from.position, f.position), opponent: f.side !== from.side }))
    .sort(
      (a, b) =>
        Number(b.opponent) - Number(a.opponent) ||
        (a.range ?? Infinity) - (b.range ?? Infinity) ||
        a.fighter.name.localeCompare(b.fighter.name),
    )
    .map(({ fighter, range }) => ({
      id: fighter.id,
      label: range === undefined ? fighter.name : t('duel.atRange', { name: fighter.name, range }),
    }))
}
const targets = computed(() => targetsOf(attacker.value))
const attackers = computed(() => all.value.map((f) => ({ id: f.id, label: f.name })))

// Opening on a figure: it attacks its nearest opponent. A new attacker does the same.
watch(
  () => [open.value, props.from] as const,
  ([isOpen, from]) => {
    if (isOpen && from) attackerId.value = from
  },
  { immediate: true },
)
watch(
  () => [open.value, props.to] as const,
  ([isOpen, to]) => {
    if (isOpen && to && all.value.some((f) => f.id === to)) targetId.value = to
  },
  { immediate: true, flush: 'post' },
)
watch(
  [attackerId, () => all.value.length > 0],
  () => {
    if (!targets.value.some((o) => o.id === targetId.value) || attacker.value?.side === target.value?.side)
      targetId.value = targets.value[0]?.id
  },
  { immediate: true },
)

// The board, around the attacker and its target: the window hides the table, not what is attacked.
const focus = computed(() => {
  const shapes = layout.value.figures.filter((f) => f.id === attackerId.value || f.id === targetId.value)
  if (!shapes.length) return undefined
  const x = Math.min(...shapes.map((f) => f.x))
  const y = Math.min(...shapes.map((f) => f.y))
  const right = Math.max(...shapes.map((f) => f.x + f.size))
  const bottom = Math.max(...shapes.map((f) => f.y + f.size))
  // At least 8 × 5 squares, centred on the two figures.
  const w = Math.max(right - x, 8)
  const h = Math.max(bottom - y, 5)
  return { x: x - (w - (right - x)) / 2, y: y - (h - (bottom - y)) / 2, w, h }
})
const marks = computed(() => ({ attacker: attackerId.value, target: targetId.value }))

/** A figure tapped on the board becomes the target (the attacker itself is ignored). */
function pick(selection: Selection | null) {
  if (selection?.kind === 'figure' && selection.id !== attackerId.value) targetId.value = selection.id
}

function swap() {
  const [a, b] = [targetId.value, attackerId.value]
  attackerId.value = a
  targetId.value = b
}

const mode = ref<'attack' | 'spell'>('attack')
watch(open, (isOpen) => isOpen && (mode.value = props.spell ? 'spell' : 'attack'), { immediate: true })
const modes = computed(() => [
  { value: 'attack', label: t('duel.attack'), icon: 'pi pi-bolt' },
  { value: 'spell', label: t('duel.spell'), icon: 'pi pi-sparkles' },
])

// ---- The AI action being resolved
const strikes = computed(() => strikesOfTurn(props.log).filter((s) => s.figure === attackerId.value))
const strike = computed(() => {
  const open = strikes.value.filter((s) => s.spell === (mode.value === 'spell') && isOpen(s, targetId.value))
  const asked = open.find((s) => s.id === props.strike)
  if (asked) return asked
  // A Counter or a Break Attack is not what the AI was told to do
  return props.encounter.reactions?.length ? undefined : open[0]
})
const allResolved = computed(
  () => !strike.value && !props.encounter.reactions?.length && strikes.value.some((s) => s.done.length),
)

// ---- What the board says
const board = computed(() => readBoard(layout.value, props.encounter))
function weaponOf(id: string) {
  const combatant = props.encounter.combatants.find((c) => c.instance === id)
  if (combatant) return cardWeapon(props.cards.get(combatant.card))
  const ally = props.encounter.allies?.find((a) => `ally:${a.id}` === id)
  if (ally) return cardWeapon(props.cards.get(ally.card))
  const member = props.party?.find((a) => `adventurer:${a.name}` === id)
  return member?.conduitForm ? cardWeapon(props.cards.get(member.conduitForm)) : equippedWeapon(member, itemsById.value)
}
const situation = computed(() => {
  if (!attackerId.value || !targetId.value || mode.value === 'spell') return undefined
  const asked = strike.value
  const weapon = asked?.melee === undefined ? undefined : { melee: asked.melee, reach: asked.range ?? 1 }
  return situationOf(board.value, attackerId.value, targetId.value, weaponOf, weapon)
})
/** An opponent hurting a Loyal Esper: its summoner may discard Summon tokens. */
const summonTokens = computed(() => {
  const ally = props.encounter.allies?.find((a) => `ally:${a.id}` === targetId.value)
  if (!ally?.esper || attacker.value?.side === 'adventurer') return 0
  return props.party?.find((a) => a.name === ally.owner)?.summonTokens ?? 0
})

// ---- Validated: closed once the journal has it, kept open with the reason otherwise
const sentAt = ref<number | null>(null)
const failed = ref(false)
function resolve(amount: number, resolution: Resolution, tokens?: number) {
  if (!attacker.value || !target.value || sentAt.value !== null) return
  sentAt.value = props.log.length
  failed.value = false
  emit('command', {
    type: 'resolve',
    attacker: attacker.value.id,
    target: target.value.id,
    amount,
    tokens,
    resolution: { ...resolution, step: strike.value ? stepKey(strike.value, target.value.id) : undefined },
  })
}
watch(
  () => props.log,
  (log) => {
    if (sentAt.value === null) return
    if (!log.slice(sentAt.value).some((entry) => entry.args?.attacker)) return
    sentAt.value = null
    open.value = false
  },
)
watch(
  () => props.error,
  (error) => {
    if (!error || sentAt.value === null) return
    sentAt.value = null
    failed.value = true
  },
)
watch(open, () => {
  sentAt.value = null
  failed.value = false
})

function keep(fighter: Fighter, patch: Partial<AdventurerSheet>) {
  if (fighter.kind === 'adventurer') remember(fighter.name, patch)
}
</script>

<template>
  <Dialog
    v-model:visible="open"
    modal
    dismissable-mask
    :draggable="false"
    :breakpoints="{ '1000px': '100vw' }"
    class="duel-dialog"
    :pt="{ content: { class: 'duel-content' } }"
  >
    <template #header>
      <div class="bar">
        <h2>{{ t('duel.title') }}</h2>
        <SelectButton
          v-model="mode"
          :options="modes"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          :aria-label="t('duel.kind')"
        >
          <template #option="{ option }">
            <i :class="option.icon" />
            <span>{{ option.label }}</span>
          </template>
        </SelectButton>
        <label class="odds-switch">
          <ToggleSwitch v-model="showOdds" />
          {{ t('duel.showOdds') }}
        </label>
      </div>
    </template>

    <Message v-if="failed && error" severity="error" size="small">{{ error }}</Message>
    <Message v-else-if="strike" severity="info" size="small">{{ t('duel.forAi', { name: strike.figure }) }}</Message>
    <Message v-else-if="allResolved" severity="warn" size="small">{{ t('duel.allResolved') }}</Message>
    <Message v-if="dice.isError.value" severity="warn" size="small">{{ t('duel.noDice') }}</Message>
    <component
      :is="mode === 'attack' ? AttackArena : SpellArena"
      v-if="attacker && target"
      :key="`${mode}|${attacker.id}|${target.id}`"
      :attacker="attacker"
      :target="target"
      :dice="dice.data.value"
      :show-odds="showOdds"
      :locked="locked"
      :sending="sentAt !== null"
      :situation="situation"
      :summon-tokens="summonTokens"
      @resolve="resolve"
      @remember="keep"
    >
      <template #attacker>
        <FighterHead
          v-model="attackerId"
          :fighter="attacker"
          :options="attackers"
          :role="mode === 'attack' ? t('duel.attacker') : t('duel.caster')"
          side="left"
        />
      </template>
      <template #target>
        <FighterHead v-model="targetId" :fighter="target" :options="targets" :role="t('duel.target')" side="right" />
        <BoardCanvas
          class="minimap"
          :layout="layout"
          :selected="null"
          :focus="focus"
          :marks="marks"
          readonly
          :label="t('duel.board', { name: target.name })"
          @select="pick"
        />
        <p class="map-hint">{{ t('duel.boardHint') }}</p>
        <Button
          class="swap"
          icon="pi pi-arrow-right-arrow-left"
          :label="t('duel.swap')"
          size="small"
          text
          @click="swap"
        />
      </template>
    </component>
    <p v-else class="empty">{{ t('duel.noTarget') }}</p>
  </Dialog>
</template>

<style scoped>
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.6rem 1.2rem;
  flex: 1;
}
h2 {
  margin: 0;
  font-size: 1.25rem;
}
.odds-switch {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.9rem;
  margin: 0 0.75rem 0 auto;
}
.minimap {
  height: 15rem;
  flex: none;
}
.map-hint {
  margin: -0.3rem 0 0;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.swap {
  align-self: flex-end;
}
.empty {
  color: var(--p-text-muted-color);
}
</style>

<style>
.duel-dialog {
  width: min(84rem, 96vw);
  height: min(54rem, 94vh);
}
.duel-content {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
@media (max-width: 1000px) {
  .duel-dialog {
    height: 100vh;
    max-height: 100vh;
    border-radius: 0;
  }
  .duel-content {
    overflow-y: auto;
  }
}
</style>
